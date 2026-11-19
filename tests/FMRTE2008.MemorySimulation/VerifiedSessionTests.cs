using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

internal static class VerifiedSessionTests
{
    internal static async Task RunAsync()
    {
        var identity = new ExecutableIdentity(SupportedBuildCatalog.Known802Sha256, "8.0.2", 52395, "x86");
        var opener = new FakeOpener();

        await MustRejectAsync(() => VerifiedPlayerReadSession.OpenAsync(123, identity,
            new NoApprovedPlayerMaps(), opener));
        Check(opener.OpenCount == 0, "Processo foi aberto sem mapa aprovado");

        await MustRejectAsync(() => VerifiedPlayerReadSession.OpenAsync(123,
            identity with { Sha256 = new string('0', 64) }, Approved(identity), opener));
        Check(opener.OpenCount == 0, "Hash desconhecido abriu o processo");

        await MustRejectAsync(() => VerifiedPlayerReadSession.OpenAsync(123, identity,
            new FakeCatalog(new ApprovedPlayerMap(new string('0',64), "8.0.2v1", 0x1000, 256, "fixture")),
            opener));
        Check(opener.OpenCount == 0, "Mapa de hash divergente abriu o processo");

        await MustRejectAsync(() => VerifiedPlayerReadSession.OpenAsync(123, identity,
            new FakeCatalog(new ApprovedPlayerMap(identity.Sha256, "8.0.2v2", 0x1000, 256, "fixture")),
            opener));
        Check(opener.OpenCount == 0, "Variante incorreta abriu o processo");

        var memory = new SimulatedProcessMemory();
        memory.AddRegion(0x1000, new byte[] {0x00,0x20,0,0,1,0,0,0});
        memory.AddRegion(0x2000, new byte[] {0x00,0x30,0,0});
        var player = new byte[260];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(player.AsSpan(256,4), 731);
        memory.AddRegion(0x3000, player);
        opener.Session = new FakeSession(memory);
        using (var session = await VerifiedPlayerReadSession.OpenAsync(123, identity, Approved(identity), opener))
        {
            var result = await session.ReadFirstPlayerIdAsync();
            Check(result.Id == 731 && result.Index == 0, "ID fictício incorreto");
        }
        Check(opener.OpenCount == 1, "Sessão aprovada não abriu exatamente uma vez");
        Check(((FakeSession)opener.Session).Disposed, "Handle fictício não liberado");
        Console.WriteLine("OK: 6 verificações da sessão bloqueada/aprovada com fixture");
    }

    private static IApprovedPlayerMapCatalog Approved(ExecutableIdentity identity) =>
        new FakeCatalog(new ApprovedPlayerMap(identity.Sha256, "8.0.2v1", 0x1000, 256, "fixture-only"));

    private static async Task MustRejectAsync(Func<Task> call)
    {
        try { await call(); }
        catch (UnauthorizedAccessException) { return; }
        throw new Exception("Abertura deveria ser bloqueada");
    }
    private static void Check(bool valid, string message)
    { if (!valid) throw new Exception(message); }

    private sealed class FakeCatalog(ApprovedPlayerMap? map) : IApprovedPlayerMapCatalog
    {
        public Task<ApprovedPlayerMap?> FindApprovedAsync(ExecutableIdentity identity,
            CancellationToken cancellationToken) => Task.FromResult(map);
    }

    private sealed class FakeOpener : IVerifiedProcessOpener
    {
        public int OpenCount { get; private set; }
        public IProcessReadSession Session { get; set; } = null!;
        public Task<IProcessReadSession> OpenVerifiedAsync(int pid, ExecutableIdentity identity,
            CancellationToken cancellationToken)
        {
            OpenCount++;
            return Task.FromResult(Session);
        }
    }

    private sealed class FakeSession(SimulatedProcessMemory memory) : IProcessReadSession
    {
        public bool Disposed { get; private set; }
        public Task<byte[]> ReadAsync(nuint address, int length, CancellationToken cancellationToken)
        {
            if (Disposed) throw new ObjectDisposedException(nameof(FakeSession));
            return memory.ReadAsync(address, length, cancellationToken);
        }
        public void Dispose() => Disposed = true;
    }
}
