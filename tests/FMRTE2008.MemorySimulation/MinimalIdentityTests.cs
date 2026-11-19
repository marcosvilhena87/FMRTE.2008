using System.Buffers.Binary;
using System.Text;
using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

internal static class MinimalIdentityTests
{
    public static async Task RunAsync()
    {
        var map = MinimalPlayerIdentityMap.Research802V1;
        Check(!map.IsApprovedForOperationalUse, "Mapa experimental indevidamente aprovado");
        Check(map.ResolveHeaderAddress(0x00400000) == 0x01B1ED4C, "Endereço de cabeçalho incorreto");
        Check(map.Matches(new ExecutableIdentity(SupportedBuildCatalog.Known802Sha256, "8.0.2", 52395, "x86")),
            "Identidade legítima rejeitada");
        Check(!map.Matches(new ExecutableIdentity(new string('0',64), "8.0.2", 52395, "x86")),
            "Hash incorreto aceito");
        await Reject<OverflowException>(() => Task.FromResult(map.ResolveHeaderAddress(0xFF000000)));

        var memory = MakeFixture(commonName: "Nivaldo", surname: "Östlund");
        var header = await new PlayerListReader(memory).ReadHeaderAsync(0x1000);
        var player = await new MinimalPlayerIdentityReader(memory, map).ReadAsync(header, 0);
        Check(player.Id == 107 && player.FirstName == "Alexander" &&
              player.LastName == "Östlund" && player.CommonName == "Nivaldo",
              "Leitura de ID / UTF-16 / nome comum incorreta");

        var withoutCommon = MakeFixture(commonName: null, surname: "Maldini");
        var noCommonPlayer = await new MinimalPlayerIdentityReader(withoutCommon, map).ReadAsync(
            await new PlayerListReader(withoutCommon).ReadHeaderAsync(0x1000), 0);
        Check(noCommonPlayer.CommonName is null && noCommonPlayer.LastName == "Maldini",
            "Ponteiro nulo de nome comum não foi tratado");

        var invalidPlayer = MakeFixture(commonName: null, surname: null);
        await Reject<InvalidDataException>(async () =>
            await new MinimalPlayerIdentityReader(invalidPlayer, map).ReadAsync(
                await new PlayerListReader(invalidPlayer).ReadHeaderAsync(0x1000), 0));

        var missingRegion = new SimulatedProcessMemory();
        missingRegion.AddRegion(0x1000, Header(0x2000, 1));
        missingRegion.AddRegion(0x2000, U32(0x3000));
        await Reject<InvalidDataException>(async () =>
            await new MinimalPlayerIdentityReader(missingRegion, map).ReadAsync(
                await new PlayerListReader(missingRegion).ReadHeaderAsync(0x1000), 0));

        var overflowing = new SimulatedProcessMemory();
        overflowing.AddRegion(0x1000, Header(0x2000, 1));
        overflowing.AddRegion(0x2000, U32(0xFFFFFFF0));
        await Reject<OverflowException>(async () =>
            await new MinimalPlayerIdentityReader(overflowing, map).ReadAsync(
                await new PlayerListReader(overflowing).ReadHeaderAsync(0x1000), 0));

        var tooLong = MakeFixture(commonName: null, surname: "Maldini", unterminatedFirst: true);
        await Reject<InvalidDataException>(async () =>
            await new MinimalPlayerIdentityReader(tooLong, map).ReadAsync(
                await new PlayerListReader(tooLong).ReadHeaderAsync(0x1000), 0));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Reject<OperationCanceledException>(async () =>
            await new MinimalPlayerIdentityReader(memory, map).ReadAsync(header, 0, cancellation.Token));

        // Catálogo padrão de produção continua recusando mapeamentos.
        Check(await new NoApprovedPlayerMaps().FindApprovedAsync(
            new ExecutableIdentity(SupportedBuildCatalog.Known802Sha256, "8.0.2", 52395, "x86"),
            default) is null, "Catálogo de produção abriu o mapa experimental");

        Console.WriteLine("OK: 11 verificações do mapa mínimo e decodificador (somente memória simulada)");
    }

    private static SimulatedProcessMemory MakeFixture(string? commonName, string? surname,
        bool unterminatedFirst = false)
    {
        var mem = new SimulatedProcessMemory();
        mem.AddRegion(0x1000, Header(0x2000, 1));
        mem.AddRegion(0x2000, U32(0x3000));
        var player = new byte[292];
        BinaryPrimitives.WriteInt32LittleEndian(player.AsSpan(256, 4), 107);
        BinaryPrimitives.WriteUInt32LittleEndian(player.AsSpan(280, 4), 0x4000);
        if (surname is not null) BinaryPrimitives.WriteUInt32LittleEndian(player.AsSpan(284, 4), 0x4100);
        if (commonName is not null) BinaryPrimitives.WriteUInt32LittleEndian(player.AsSpan(288, 4), 0x4200);
        mem.AddRegion(0x3000, player);

        Name(mem, 0x4000, 0x5000, 0x6000, "Alexander", unterminatedFirst);
        if (surname is not null) Name(mem, 0x4100, 0x5100, 0x6100, surname);
        if (commonName is not null) Name(mem, 0x4200, 0x5200, 0x6200, commonName);
        return mem;
    }

    private static void Name(SimulatedProcessMemory memory, uint a, uint b, uint c, string text,
        bool unterminated = false)
    {
        var outer = new byte[40];
        BinaryPrimitives.WriteUInt32LittleEndian(outer.AsSpan(36, 4), b);
        memory.AddRegion(a, outer);
        var inner = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(inner.AsSpan(8, 4), c);
        memory.AddRegion(b, inner);
        memory.AddRegion(c, unterminated
            ? Encoding.Unicode.GetBytes(new string('X', 96))
            : Encoding.Unicode.GetBytes(text + "\0"));
    }

    private static byte[] Header(uint p, int count)
    {
        var result = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(result, p);
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(4), count);
        return result;
    }
    private static byte[] U32(uint v)
    {
        var b = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(b, v);
        return b;
    }
    private static void Check(bool ok, string why)
    {
        if (!ok) throw new Exception(why);
    }
    private static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        try { await action(); } catch (T) { return; }
        throw new Exception("Era esperada exceção " + typeof(T).Name);
    }
}
