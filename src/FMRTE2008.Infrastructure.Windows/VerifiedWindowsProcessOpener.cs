using System.Diagnostics;
using FMRTE2008.Application;

namespace FMRTE2008.Infrastructure.Windows;

/// <summary>
/// Conector Windows read-only. Só é chamado após a aprovação do mapa pela Application.
/// Revalida o executável associado ao PID antes e depois da abertura.
/// </summary>
public sealed class VerifiedWindowsProcessOpener : IVerifiedProcessOpener
{
    public Task<IProcessReadSession> OpenVerifiedAsync(int processId, ExecutableIdentity identity,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
        using var process = Process.GetProcessById(processId);
        if (process.HasExited) throw new InvalidOperationException("Processo encerrado");
        string path = process.MainModule?.FileName ?? throw new InvalidOperationException("Executável indisponível");
        if (!string.Equals(Path.GetFileName(path), "fm.exe", StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("O PID não corresponde a fm.exe");
        Verify(path, identity);
        IProcessReadSession session = new WindowsReadOnlyProcessSession(processId);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (process.HasExited ||
                !string.Equals(process.MainModule?.FileName, path, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Processo alterado durante abertura");
            Verify(path, identity);
            return Task.FromResult(session);
        }
        catch
        {
            session.Dispose();
            throw;
        }
    }

    private static void Verify(string path, ExecutableIdentity expected)
    {
        var actual = ExecutableFingerprint.InspectFile(path);
        if (!string.Equals(actual.Sha256, expected.Sha256, StringComparison.OrdinalIgnoreCase) ||
            actual.Architecture != expected.Architecture ||
            actual.FileVersion != expected.FileVersion ||
            actual.FilePrivatePart != expected.FilePrivatePart ||
            !SupportedBuildCatalog.Recognize(actual).IsRecognized)
            throw new UnauthorizedAccessException("Hash ou versão do processo diverge da identidade aprovada");
    }
}
