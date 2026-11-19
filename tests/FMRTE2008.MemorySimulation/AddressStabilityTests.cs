using System.Buffers.Binary;
using System.Text;
using FMRTE2008.Application;
using FMRTE2008.Domain;

namespace FMRTE2008.MemorySimulation;

internal static class AddressStabilityTests
{
    public static async Task RunAsync()
    {
        int count = 0;
        void Check(bool valid, string error) { if (!valid) throw new Exception(error); count++; }
        var map = MinimalPlayerIdentityMap.Research802V1;
        uint baseA = 0x00400000, baseB = 0x08000000;
        Check(map.ResolveHeaderAddress(baseA) == 0x01B1ED4C, "Base preferencial");
        Check(map.ResolveHeaderAddress(baseB) == 0x0971ED4C, "Base simulada realocada");
        Check(map.ResolveHeaderAddress(baseA) != map.ResolveHeaderAddress(baseB), "Header fixo incorreto");
        await Reject<ArgumentOutOfRangeException>(() => Task.FromResult(map.ResolveHeaderAddress(0)));
        count++;
        await Reject<OverflowException>(() => Task.FromResult(map.ResolveHeaderAddress(0xFF000000)));
        count++;

        var a = Fixture(baseA);
        var b = Fixture(baseB);
        var stableA = await new StablePlayerIdentitySnapshotReader(a, map).ReadStableAsync(baseA, 0);
        var stableB = await new StablePlayerIdentitySnapshotReader(b, map).ReadStableAsync(baseB, 0);
        Check(stableA.Id == 107 && stableA.FirstName == "Alexander" &&
            stableA.LastName == "Östlund", "Identidade na base original");
        Check(stableB.Id == stableA.Id && stableB.LastName == stableA.LastName,
            "Identidade em base simulada realocada");
        await Reject<InvalidDataException>(() =>
            new StablePlayerIdentitySnapshotReader(b, map).ReadStableAsync(baseA, 0));
        count++;
        await Reject<ArgumentOutOfRangeException>(() =>
            new StablePlayerIdentitySnapshotReader(a, map).ReadStableAsync(baseA, 1));
        count++;

        // Null pointer, missing region, truncated read, and an overflowing x86 pointer
        // must fail safely without producing an identity.
        foreach (uint badPointer in new uint[] {0, 0xDEADBEEF, 0xFFFFFFF0})
        {
            var corrupt = Fixture(baseA, playerPointer: badPointer);
            if (badPointer == 0xFFFFFFF0)
                await Reject<OverflowException>(() =>
                    new StablePlayerIdentitySnapshotReader(corrupt, map).ReadStableAsync(baseA, 0));
            else
                await Reject<InvalidDataException>(() =>
                    new StablePlayerIdentitySnapshotReader(corrupt, map).ReadStableAsync(baseA, 0));
            count++;
        }
        var partial = new FaultReader(Fixture(baseA)) { PartialAddress = 0x3000 + 256 };
        await Reject<InvalidDataException>(() =>
            new StablePlayerIdentitySnapshotReader(partial, map).ReadStableAsync(baseA, 0));
        count++;

        // Header flips between the beginning and end of one record.
        var headerChange = new FaultReader(Fixture(baseA))
        {
            ChangeHeaderOnSecondRead = map.ResolveHeaderAddress(baseA)
        };
        await Reject<InvalidDataException>(() =>
            new StablePlayerIdentitySnapshotReader(headerChange, map).ReadStableAsync(baseA, 0));
        count++;

        // Pointer flips while decoding the same record (2 reads before end check).
        var ptrChange = new FaultReader(Fixture(baseA))
        {
            ChangePointerOnThirdRead = 0x2000
        };
        await Reject<InvalidDataException>(() =>
            new StablePlayerIdentitySnapshotReader(ptrChange, map).ReadStableAsync(baseA, 0));
        count++;

        var closing = new FaultReader(Fixture(baseA)) { FailAfterReads = 4 };
        await Reject<IOException>(() =>
            new StablePlayerIdentitySnapshotReader(closing, map).ReadStableAsync(baseA, 0));
        count++;
        // A dead session is not retried; use a new reader and new source.
        var restored = await new StablePlayerIdentitySnapshotReader(Fixture(baseB), map)
            .ReadStableAsync(baseB, 0);
        Check(restored.Id == 107, "Nova sessão não conseguiu recuperar");
        Check(closing.ReadCount == 5, "Tentativa indevida de reler sessão encerrada");

        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        await Reject<OperationCanceledException>(() =>
            new StablePlayerIdentitySnapshotReader(Fixture(baseA), map)
                .ReadStableAsync(baseA, 0, cancel.Token));
        count++;
        Check(!map.IsApprovedForOperationalUse, "Leitor de pesquisa aprovou mapa");
        Check(await new NoApprovedPlayerMaps().FindApprovedAsync(
            new ExecutableIdentity(SupportedBuildCatalog.Known802Sha256,"8.0.2",52395,"x86"),
            default) is null, "Catálogo de produção liberado");
        Console.WriteLine($"OK: {count} verificações de endereçamento, ponteiros, estabilidade e recuperação simulados");
    }

    private static SimulatedProcessMemory Fixture(uint moduleBase, uint playerPointer = 0x3000)
    {
        var map = MinimalPlayerIdentityMap.Research802V1;
        var memory = new SimulatedProcessMemory();
        memory.AddRegion(map.ResolveHeaderAddress(moduleBase), Header(0x2000, 1));
        memory.AddRegion(0x2000, U32(playerPointer));
        if (playerPointer != 0x3000) return memory;
        var p = new byte[292];
        BinaryPrimitives.WriteInt32LittleEndian(p.AsSpan(256), 107);
        BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(280), 0x4000);
        BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(284), 0x4100);
        memory.AddRegion(0x3000, p);
        Name(memory, 0x4000, "Alexander");
        Name(memory, 0x4100, "Östlund");
        return memory;
    }

    private static void Name(SimulatedProcessMemory memory, uint address, string value)
    {
        var outer = new byte[40];
        BinaryPrimitives.WriteUInt32LittleEndian(outer.AsSpan(36), address + 0x40);
        memory.AddRegion(address, outer);
        var inner = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(inner.AsSpan(8), address + 0x60);
        memory.AddRegion(address + 0x40, inner);
        memory.AddRegion(address + 0x60, Encoding.Unicode.GetBytes(value + "\0"));
    }

    private static byte[] Header(uint address, int n)
    {
        var b = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(b, address);
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(4), n);
        return b;
    }
    private static byte[] U32(uint value)
    {
        var b = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, value);
        return b;
    }
    private static async Task Reject<T>(Func<Task> operation) where T : Exception
    {
        try { await operation(); } catch (T) { return; }
        throw new Exception("Era esperada exceção " + typeof(T).Name);
    }

    private sealed class FaultReader(IProcessReader inner) : IProcessReader
    {
        public uint? PartialAddress { get; init; }
        public uint? ChangeHeaderOnSecondRead { get; init; }
        public uint? ChangePointerOnThirdRead { get; init; }
        public int? FailAfterReads { get; init; }
        public int ReadCount { get; private set; }
        private int _headerReads, _pointerReads;
        public async Task<byte[]> ReadAsync(nuint address, int length, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ReadCount++;
            if (FailAfterReads is int limit && ReadCount > limit)
                throw new IOException("Sessão simulada interrompida");
            uint at = (uint)address;
            if (PartialAddress == at)
                return new byte[length - 1];
            var data = await inner.ReadAsync(address, length, ct);
            if (ChangeHeaderOnSecondRead == at && ++_headerReads == 2)
            {
                var copy = (byte[])data.Clone();
                BinaryPrimitives.WriteInt32LittleEndian(copy.AsSpan(4), 2);
                return copy;
            }
            if (ChangePointerOnThirdRead == at && ++_pointerReads == 3)
            {
                var copy = (byte[])data.Clone();
                BinaryPrimitives.WriteUInt32LittleEndian(copy, 0x3100);
                return copy;
            }
            return data;
        }
    }
}
