using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

internal static class PlayerQueryTests
{
    public static void Run()
    {
        var service = new PlayerQueryService(new DemonstrationPlayerSource());
        var first = service.Search("", 1, 3);
        Check(first.Items.Count == 3 && first.TotalMatches == 6 &&
            first.PageCount == 2 && first.PageNumber == 1, "Primeira página incorreta");
        var second = service.Search("", 2, 3);
        Check(second.Items.Count == 3 && second.PageNumber == 2 &&
            second.Items[0].Id == 54, "Segunda página incorreta");
        var overflow = service.Search("", 999, 3);
        Check(overflow.PageNumber == 2 && overflow.Items.Count == 3, "Página extrema não limitada");
        var name = service.Search("maldini", 1, 3);
        Check(name.TotalMatches == 1 && name.Items[0].Id == 45, "Pesquisa por sobrenome falhou");
        var accent = service.Search("ostlund", 1, 3);
        Check(accent.TotalMatches == 1 && accent.Items[0].Id == 107, "Pesquisa ignorando acentos falhou");
        var common = service.Search("Nivaldo", 1, 3);
        Check(common.TotalMatches == 1 && common.Items[0].Id == 21, "Pesquisa por nome comum falhou");
        var fullName = service.Search("Paolo Maldini", 1, 3);
        Check(fullName.TotalMatches == 1 && fullName.Items[0].Id == 45, "Pesquisa por nome completo falhou");
        var id = service.Search("54", 1, 3);
        Check(id.TotalMatches == 1 && id.Items[0].Id == 54, "Pesquisa por ID exato falhou");
        var noMatch = service.Search("XXXXXXX", 1, 3);
        Check(noMatch.TotalMatches == 0 && noMatch.Items.Count == 0 &&
            noMatch.PageCount == 1, "Busca vazia incorreta");
        Reject<ArgumentOutOfRangeException>(() => service.Search("", 0));
        Reject<ArgumentOutOfRangeException>(() => service.Search("", 1, 101));
        Reject<ArgumentOutOfRangeException>(() => service.Search(new string('X', 129), 1));
        Reject<UnauthorizedAccessException>(() => new PlayerQueryService(new FakeLiveSource()));
        Check(!service.IsLiveProcessSource, "Fonte demonstrativa não é inerte");
        Console.WriteLine("OK: 14 verificações de consulta/paginação e bloqueio da fonte real");
    }

    private static void Check(bool ok, string why)
    {
        if (!ok) throw new Exception(why);
    }
    private static void Reject<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new Exception("Era esperada a exceção " + typeof(T).Name);
    }
    private sealed class FakeLiveSource : IPlayerIdentitySource
    {
        public bool IsLiveProcessSource => true;
        public IReadOnlyList<MinimalPlayerIdentity> ReadSnapshot() =>
            throw new Exception("Fonte real não poderia ser consultada");
    }
}
