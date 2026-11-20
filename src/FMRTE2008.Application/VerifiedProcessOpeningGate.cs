namespace FMRTE2008.Application;

/// <summary>Evidence from one process instance; a PID by itself is never a stable identity.</summary>
public sealed record ProcessOpeningEvidence(
    int ProcessId, long StartTimeUtcTicks, string ExecutablePath, ExecutableIdentity Executable);

/// <summary>
/// Testable fail-closed opening sequence. Production uses only the Windows read-only
/// opener after the NoApprovedPlayerMaps gate; injected delegates permit offline tests.
/// </summary>
public static class VerifiedProcessOpeningGate
{
    public static async Task<IProcessReadSession> OpenAsync(
        int processId, ExecutableIdentity expected,
        Func<CancellationToken, Task<ProcessOpeningEvidence>> inspect,
        Func<CancellationToken, Task<IProcessReadSession>> acquire,
        CancellationToken cancellationToken = default)
    {
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(inspect);
        ArgumentNullException.ThrowIfNull(acquire);
        cancellationToken.ThrowIfCancellationRequested();

        var before = await inspect(cancellationToken);
        Validate(before, processId, expected);
        cancellationToken.ThrowIfCancellationRequested();

        // Only acquire a handle after the first identity check. Any exception
        // after acquisition closes the handle before returning to the caller.
        IProcessReadSession? session = null;
        try
        {
            session = await acquire(cancellationToken)
                ?? throw new InvalidOperationException("Abertura não forneceu sessão");
            cancellationToken.ThrowIfCancellationRequested();
            var after = await inspect(cancellationToken);
            Validate(after, processId, expected);
            if (before != after)
                throw new InvalidOperationException("Instância do processo mudou durante a abertura");
            cancellationToken.ThrowIfCancellationRequested();
            return session;
        }
        catch
        {
            session?.Dispose();
            throw;
        }
    }

    private static void Validate(ProcessOpeningEvidence? evidence, int pid, ExecutableIdentity expected)
    {
        if (evidence is null || evidence.ProcessId != pid || evidence.StartTimeUtcTicks <= 0 ||
            string.IsNullOrWhiteSpace(evidence.ExecutablePath) ||
            !string.Equals(Path.GetFileName(evidence.ExecutablePath), "fm.exe",
                StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("PID ou identidade da instância inválidos");

        var actual = evidence.Executable;
        if (actual is null ||
            !string.Equals(actual.Sha256, expected.Sha256, StringComparison.OrdinalIgnoreCase) ||
            actual.Architecture != expected.Architecture ||
            actual.FileVersion != expected.FileVersion ||
            actual.FilePrivatePart != expected.FilePrivatePart ||
            !SupportedBuildCatalog.Recognize(actual).IsRecognized)
            throw new UnauthorizedAccessException("Hash ou versão do executável não corresponde à identidade");
    }
}
