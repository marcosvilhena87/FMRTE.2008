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
    private bool _invalidated;
    private readonly SemaphoreSlim _readGate = new(1, 1);
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
        return await GuardedReadAsync(async ct =>
        {
            var reader = new PlayerListReader(_memory);
            PlayerListHeader header = await reader.ReadHeaderAsync(_map.HeaderAddress, ct);
            return await reader.ReadPlayerIdAsync(header, 0, _map.PlayerIdOffset, ct);
        }, cancellationToken);
    }

    /// <summary>
    /// Página de identificação pelo mapa mínimo. Só existe após OpenAsync e catálogo aprovado.
    /// Lê apenas os itens pedidos, sem carregar a lista inteira e sem escrever na memória.
    /// </summary>
    public async Task<IReadOnlyList<MinimalPlayerIdentity>> ReadIdentityPageAsync(
        int pageNumber, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (_map.PlayerIdOffset != 256 || _map.Variant != "8.0.2v1" ||
            !string.Equals(_map.Sha256, MinimalPlayerIdentityMap.Research802V1.ExecutableSha256,
                StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Mapa aprovado não corresponde aos offsets mínimos de identificação");

        return await GuardedReadAsync(async ct =>
        {
            var tableReader = new PlayerListReader(_memory);
            var header = await tableReader.ReadHeaderAsync(_map.HeaderAddress, ct);
            long start = ((long)pageNumber - 1L) * pageSize;
            if (start >= header.Count) return (IReadOnlyList<MinimalPlayerIdentity>)Array.Empty<MinimalPlayerIdentity>();
            int end = (int)Math.Min(header.Count, start + pageSize);
            var decoder = new MinimalPlayerIdentityReader(_memory, MinimalPlayerIdentityMap.Research802V1);
            var result = new List<MinimalPlayerIdentity>(end - (int)start);
            for (int index = (int)start; index < end; index++)
            {
                ct.ThrowIfCancellationRequested();
                result.Add(await decoder.ReadAsync(header, index, ct));
            }
            return (IReadOnlyList<MinimalPlayerIdentity>)result.AsReadOnly();
        }, cancellationToken);
    }

    /// <summary>
    /// Fail closed after a truncated/invalid read, loss of the process or interrupted
    /// operation. No subsequent read may reuse the handle; caller must open a new
    /// verified session. This does not approve any real mapping.
    /// </summary>
    private async Task<T> GuardedReadAsync<T>(Func<CancellationToken, Task<T>> read,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed || _invalidated, this);
        // Reject concurrent session use rather than interleaving page/identity reads.
        if (!await _readGate.WaitAsync(0, cancellationToken))
            throw new InvalidOperationException("Outra leitura da sessão está em andamento");
        try
        {
            ObjectDisposedException.ThrowIf(_disposed || _invalidated, this);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                T result = await read(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                ObjectDisposedException.ThrowIf(_disposed || _invalidated, this);
                return result;
            }
            catch (Exception error) when (error is IOException or OverflowException
                or OperationCanceledException or ObjectDisposedException
                or System.ComponentModel.Win32Exception)
            {
                _invalidated = true;
                _memory.Dispose();
                throw;
            }
        }
        finally
        {
            _readGate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _memory.Dispose();
    }
}
