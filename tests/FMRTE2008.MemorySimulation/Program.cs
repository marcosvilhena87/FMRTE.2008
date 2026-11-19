using System.Buffers.Binary;
using FMRTE2008.Application;
using FMRTE2008.MemorySimulation;

int passed = 0;

await Test("duas entradas e IDs da fixture", async () =>
{
    var (mem, hdr) = MakeFixture();
    var decoder = new PlayerListReader(mem);
    var header = await decoder.ReadHeaderAsync(hdr);
    Equal(0x2000u, header.TableAddress);
    Equal(2, header.Count);
    Equal(101, (await decoder.ReadPlayerIdAsync(header, 0, 256)).Id);
    Equal(202, (await decoder.ReadPlayerIdAsync(header, 1, 256)).Id);
});
await Test("índice negativo", async () =>
{
    var (mem, hdr) = MakeFixture();
    var reader = new PlayerListReader(mem);
    var header = await reader.ReadHeaderAsync(hdr);
    await Throws<ArgumentOutOfRangeException>(() => reader.ReadPlayerIdAsync(header, -1, 256));
});
await Test("índice igual à quantidade", async () =>
{
    var (mem, hdr) = MakeFixture();
    var reader = new PlayerListReader(mem);
    var header = await reader.ReadHeaderAsync(hdr);
    await Throws<ArgumentOutOfRangeException>(() => reader.ReadPlayerIdAsync(header, 2, 256));
});
await Test("tabela nula", async () =>
{
    var mem = new SimulatedProcessMemory();
    mem.AddRegion(0x1000, Header(0, 2));
    await Throws<InvalidDataException>(() => new PlayerListReader(mem).ReadHeaderAsync(0x1000));
});
await Test("quantidade negativa", async () =>
{
    var mem = new SimulatedProcessMemory();
    mem.AddRegion(0x1000, Header(0x2000, -1));
    await Throws<InvalidDataException>(() => new PlayerListReader(mem).ReadHeaderAsync(0x1000));
});
await Test("quantidade excessiva", async () =>
{
    var mem = new SimulatedProcessMemory();
    mem.AddRegion(0x1000, Header(0x2000, PlayerListReader.MaxFixtureEntries + 1));
    await Throws<InvalidDataException>(() => new PlayerListReader(mem).ReadHeaderAsync(0x1000));
});
await Test("header parcial", async () =>
{
    var mem = new SimulatedProcessMemory();
    mem.AddRegion(0x1000, new byte[4]);
    await Throws<InvalidDataException>(() => new PlayerListReader(mem).ReadHeaderAsync(0x1000));
});
await Test("ponteiro de jogador nulo", async () =>
{
    var mem = new SimulatedProcessMemory();
    mem.AddRegion(0x1000, Header(0x2000, 1));
    mem.AddRegion(0x2000, UInt(0));
    var reader = new PlayerListReader(mem);
    await Throws<InvalidDataException>(async () => await reader.ReadPlayerIdAsync(
        await reader.ReadHeaderAsync(0x1000), 0, 256));
});
await Test("endereço do ID fora da memória", async () =>
{
    var (mem, hdr) = MakeFixture();
    var reader = new PlayerListReader(mem);
    await Throws<InvalidDataException>(async () => await reader.ReadPlayerIdAsync(
        await reader.ReadHeaderAsync(hdr), 0, 300));
});
await Test("overflow x86 ao calcular o ID", async () =>
{
    var mem = new SimulatedProcessMemory();
    mem.AddRegion(0x1000, Header(0x2000, 1));
    mem.AddRegion(0x2000, UInt(0xFFFFFFFE));
    var reader = new PlayerListReader(mem);
    await Throws<OverflowException>(async () => await reader.ReadPlayerIdAsync(
        await reader.ReadHeaderAsync(0x1000), 0, 256));
});
await Test("header com overflow da tabela", async () =>
{
    var mem = new SimulatedProcessMemory();
    mem.AddRegion(0x1000, Header(0xFFFFFFFC, 3));
    await Throws<OverflowException>(() => new PlayerListReader(mem).ReadHeaderAsync(0x1000));
});
await Test("leitura cancelada", async () =>
{
    var (mem, hdr) = MakeFixture();
    using var cts = new CancellationTokenSource();
    cts.Cancel();
    await Throws<OperationCanceledException>(() => new PlayerListReader(mem).ReadHeaderAsync(hdr, cts.Token));
});
await Test("região esparsa não mapeada", async () =>
{
    var mem = new SimulatedProcessMemory();
    await Throws<InvalidDataException>(() => mem.ReadAsync((nuint)0x5000, 4, default));
});
await Test("rejeita sobreposição de regiões", () =>
{
    var mem = new SimulatedProcessMemory();
    mem.AddRegion(0x1000, new byte[8]);
    ThrowsSync<InvalidOperationException>(() => mem.AddRegion(0x1004, new byte[8]));
    return Task.CompletedTask;
});
BuildRecognitionTests.Run();
await VerifiedSessionTests.RunAsync();
await MinimalIdentityTests.RunAsync();
PlayerQueryTests.Run();
await ApprovedIdentityPageTests.RunAsync();
await IncrementalSearchTests.RunAsync();
await IncrementalControllerTests.RunAsync();
Console.WriteLine($"PASSARAM: {passed} testes de simulação + 4 testes de identidade + 6 de sessão + 11 verificações de identificação + 14 verificações de pesquisa/paginação + 8 verificações de página aprovada em fixture + busca incremental/falhas + encerramento/recuperação de sessão (sem processo real).");

async Task Test(string name, Func<Task> action)
{
    await action();
    passed++;
    Console.WriteLine("OK: " + name);
}
static void Equal<T>(T expected, T actual) where T : IEquatable<T>
{
    if (!expected.Equals(actual)) throw new Exception($"Esperado {expected}, obtido {actual}");
}
static async Task Throws<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception($"Era esperada a exceção {typeof(T).Name}");
}
static void ThrowsSync<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new Exception($"Era esperada a exceção {typeof(T).Name}");
}
static (SimulatedProcessMemory, uint) MakeFixture()
{
    var mem = new SimulatedProcessMemory();
    mem.AddRegion(0x1000, Header(0x2000, 2));
    var pointers = new byte[8];
    BinaryPrimitives.WriteUInt32LittleEndian(pointers.AsSpan(0, 4), 0x3000);
    BinaryPrimitives.WriteUInt32LittleEndian(pointers.AsSpan(4, 4), 0x4000);
    mem.AddRegion(0x2000, pointers);
    var playerA = new byte[260];
    var playerB = new byte[260];
    BinaryPrimitives.WriteInt32LittleEndian(playerA.AsSpan(256, 4), 101);
    BinaryPrimitives.WriteInt32LittleEndian(playerB.AsSpan(256, 4), 202);
    mem.AddRegion(0x3000, playerA);
    mem.AddRegion(0x4000, playerB);
    return (mem, 0x1000);
}
static byte[] Header(uint pointer, int count)
{
    var buf = new byte[8];
    BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0, 4), pointer);
    BinaryPrimitives.WriteInt32LittleEndian(buf.AsSpan(4, 4), count);
    return buf;
}
static byte[] UInt(uint value)
{
    var buf = new byte[4];
    BinaryPrimitives.WriteUInt32LittleEndian(buf, value);
    return buf;
}
