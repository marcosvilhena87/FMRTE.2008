using FMRTE2008.Domain;

namespace FMRTE2008.Application;

/// <summary>Mapa de leitura validado; só pode ser obtido de catálogo confiável.</summary>
public sealed record ApprovedPlayerMap(string Sha256, string Variant, uint HeaderAddress, int PlayerIdOffset,
    string ApprovalReference);

public interface IApprovedPlayerMapCatalog
{
    Task<ApprovedPlayerMap?> FindApprovedAsync(ExecutableIdentity identity, CancellationToken cancellationToken);
}

public interface IProcessReadSession : IProcessReader, IDisposable { }

/// <summary>Porta que abre o processo e confirma a identidade antes de devolver a sessão.</summary>
public interface IVerifiedProcessOpener
{
    Task<IProcessReadSession> OpenVerifiedAsync(int processId, ExecutableIdentity identity,
        CancellationToken cancellationToken);
}

/// <summary>
/// Política de produção: nenhum mapa do FM2008 está aprovado neste estágio.
/// O mapa 'legacy-il-candidate-summary' não pode habilitar memória real.
/// </summary>
public sealed class NoApprovedPlayerMaps : IApprovedPlayerMapCatalog
{
    public Task<ApprovedPlayerMap?> FindApprovedAsync(ExecutableIdentity identity,
        CancellationToken cancellationToken) => Task.FromResult<ApprovedPlayerMap?>(null);
}

public sealed class VerifiedPlayerReadSession : IDisposable
{
    private readonly IProcessReadSession _memory;
    private readonly ApprovedPlayerMap _map;
    private bool _disposed;
    private VerifiedPlayerReadSession(IProcessReadSession memory, ApprovedPlayerMap map)
    {
        _memory = memory;
        _map = map;
    }

    public static async Task<VerifiedPlayerReadSession> OpenAsync(
        int processId, ExecutableIdentity identity, IApprovedPlayerMapCatalog catalog,
        IVerifiedProcessOpener opener, CancellationToken cancellationToken = default)
    {
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(opener);
        cancellationToken.ThrowIfCancellationRequested();

        var recognized = SupportedBuildCatalog.Recognize(identity);
        if (!recognized.IsRecognized)
            throw new UnauthorizedAccessException("Build não reconhecida: leitura bloqueada");

        ApprovedPlayerMap? map = await catalog.FindApprovedAsync(identity, cancellationToken);
        if (map is null || !string.Equals(map.Sha256, identity.Sha256, StringComparison.OrdinalIgnoreCase) ||
            map.Variant != recognized.Variant || string.IsNullOrWhiteSpace(map.ApprovalReference) ||
            map.HeaderAddress == 0 || map.HeaderAddress > uint.MaxValue - 7 ||
            map.PlayerIdOffset < 0 || map.PlayerIdOffset > 4096)
            throw new UnauthorizedAccessException("Mapa não aprovado ou incompatível: leitura bloqueada");

        cancellationToken.ThrowIfCancellationRequested();
        // O opener deverá comparar novamente o executável do PID, não confiar apenas no parâmetro identity.
        var memory = await opener.OpenVerifiedAsync(processId, identity, cancellationToken);
        if (memory is null) throw new InvalidOperationException("O opener não forneceu sessão");
        return new VerifiedPlayerReadSession(memory, map);
    }

    public async Task<PlayerIdRecord> ReadFirstPlayerIdAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var reader = new PlayerListReader(_memory);
        PlayerListHeader header = await reader.ReadHeaderAsync(_map.HeaderAddress, cancellationToken);
        return await reader.ReadPlayerIdAsync(header, 0, _map.PlayerIdOffset, cancellationToken);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _memory.Dispose();
    }
}
