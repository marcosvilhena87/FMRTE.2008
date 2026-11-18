using FMRTE2008.Application;
using FMRTE2008.Domain;

await CheckAsync("ponteiro little-endian", async () =>
{
    var reader = new PointerTableReader(new FakeReader(new byte[] { 0x78, 0x56, 0x34, 0x12 }));
    uint result = await reader.ReadPointerAtIndexAsync(0x1000, 2, 5);
    if (result != 0x12345678) throw new Exception($"Resultado {result:X8}");
    if (FakeReader.LastAddress != 0x1008) throw new Exception("Índice * 4 incorreto");
});
await CheckAsync("índice inválido", async () =>
{
    var reader = new PointerTableReader(new FakeReader(new byte[4]));
    await AssertThrowsAsync<ArgumentOutOfRangeException>(() => reader.ReadPointerAtIndexAsync(0x1000, 5, 5));
});
await CheckAsync("overflow de endereço", async () =>
{
    var reader = new PointerTableReader(new FakeReader(new byte[4]));
    await AssertThrowsAsync<OverflowException>(() => reader.ReadPointerAtIndexAsync(0xFFFFFFFC, 1, 3));
});
await CheckAsync("leitura parcial", async () =>
{
    var reader = new PointerTableReader(new FakeReader(new byte[2]));
    await AssertThrowsAsync<InvalidDataException>(() => reader.ReadPointerAtIndexAsync(0x1000, 0, 1));
});
await CheckAsync("ponteiro nulo", async () =>
{
    var reader = new PointerTableReader(new FakeReader(new byte[4]));
    await AssertThrowsAsync<InvalidDataException>(() => reader.ReadPointerAtIndexAsync(0x1000, 0, 1));
});
Console.WriteLine("5 testes concluídos.");

static async Task CheckAsync(string name, Func<Task> action)
{
    await action();
    Console.WriteLine($"OK: {name}");
}

static async Task AssertThrowsAsync<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception($"Exceção {typeof(T).Name} era esperada");
}

sealed class FakeReader(byte[] data) : IProcessReader
{
    public static nuint LastAddress { get; private set; }
    public Task<byte[]> ReadAsync(nuint address, int length, CancellationToken cancellationToken)
    {
        LastAddress = address;
        return Task.FromResult(data);
    }
}
