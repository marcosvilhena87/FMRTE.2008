using System.Diagnostics;
using FMRTE2008.Application;

namespace FMRTE2008.Infrastructure.Windows;

/// <summary>
/// Windows read-only connector. Production requests remain blocked by
/// NoApprovedPlayerMaps; this class does not approve any mapping.
/// </summary>
public sealed class VerifiedWindowsProcessOpener : IVerifiedProcessOpener
{
    public Task<IProcessReadSession> OpenVerifiedAsync(int processId, ExecutableIdentity identity,
        CancellationToken cancellationToken)
    {
        return VerifiedProcessOpeningGate.OpenAsync(processId, identity,
            inspect: ct =>
            {
                ct.ThrowIfCancellationRequested();
                using var process = Process.GetProcessById(processId);
                if (process.HasExited)
                    throw new InvalidOperationException("Processo encerrado");
                string path = process.MainModule?.FileName
                    ?? throw new InvalidOperationException("Executável indisponível");
                long startUtcTicks = process.StartTime.ToUniversalTime().Ticks;
                var actual = ExecutableFingerprint.InspectFile(path);
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(new ProcessOpeningEvidence(
                    process.Id, startUtcTicks, path, actual));
            },
            acquire: ct =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult<IProcessReadSession>(
                    new WindowsReadOnlyProcessSession(processId));
            },
            cancellationToken, requireHandleIdentity: true);
    }
}
