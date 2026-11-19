using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

internal static class DistributedSamplingTests
{
    public static void Run()
    {
        int passed = 0;
        void Check(bool ok, string issue) { if (!ok) throw new Exception(issue); passed++; }
        var first = DistributedPlayerSampling.BuildIndices(6697, 9);
        Check(first.Length == 9, "Tamanho do primeiro save");
        Check(first[0] == 0 && first[^1] == 6696, "Primeiro save sem extremos");
        Check(first.SequenceEqual(first.OrderBy(x => x).Distinct()), "Índices sem ordenação ou duplicados");
        var second = DistributedPlayerSampling.BuildIndices(5154, 9);
        Check(second[0] == 0 && second[^1] == 5153, "Segundo save sem extremos");
        Check(!first.SequenceEqual(second), "Plano não mudou com a contagem da partida");
        Check(DistributedPlayerSampling.BuildIndices(2, 2).SequenceEqual(new[] {0, 1}),
            "Quantidade mínima");
        Check(DistributedPlayerSampling.BuildIndices(100000, 20).Distinct().Count() == 20,
            "Quantidade máxima defensiva");
        Reject(() => DistributedPlayerSampling.BuildIndices(1, 2));
        passed++;
        Reject(() => DistributedPlayerSampling.BuildIndices(3, 4));
        passed++;
        Reject(() => DistributedPlayerSampling.BuildIndices(101, 21));
        passed++;
        Reject(() => DistributedPlayerSampling.BuildIndices(100001, 20));
        passed++;
        Console.WriteLine($"OK: {passed} verificações dos índices distribuídos determinísticos");
    }

    private static void Reject(Action action)
    {
        try { action(); } catch (ArgumentOutOfRangeException) { return; }
        throw new Exception("Era esperado ArgumentOutOfRangeException");
    }
}
