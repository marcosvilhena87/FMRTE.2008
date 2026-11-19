using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

internal static class IncrementalSearchTests
{
    public static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool result, string message)
        {
            if (!result) throw new Exception(message);
            checks++;
        }

        var source = new DemonstrationIndexedPlayerSource();
        var search = new IncrementalPlayerSearch(source);
        var first = await search.ScanNextAsync("", 0, 2);
        Check(first.Scanned == 2 && first.NextIndex == 2 && !first.Complete &&
            first.Matches.Count == 2, "Primeiro lote limitado");

        var second = await search.ScanNextAsync("", first.NextIndex, 2);
        Check(second.NextIndex == 4 && second.Matches[0].Id == 45,
            "Cursor da segunda página");
        var last = await search.ScanNextAsync("", 4, 2);
        Check(last.Complete && last.NextIndex == 6 && last.Matches.Count == 2,
            "Fim da busca");
        Check((await search.ScanNextAsync("", 6, 2)).Scanned == 0,
            "Consulta após fim");
        var accent = await search.ScanNextAsync("ostlund", 4, 2);
        Check(accent.Matches.Count == 1 && accent.Matches[0].Id == 107,
            "Busca sem diacríticos");
        var id = await search.ScanNextAsync("54", 2, 2);
        Check(id.Matches.Count == 1 && id.Matches[0].Id == 54,
            "Busca por ID exato");
        var name = await search.ScanNextAsync("Paolo Maldini", 2, 2);
        Check(name.Matches.Count == 1 && name.Matches[0].Id == 45,
            "Nome completo");
        Check((await search.ScanNextAsync("nenhum", 0, 6)).Matches.Count == 0,
            "Nenhum resultado");
        await Reject<ArgumentOutOfRangeException>(() => search.ScanNextAsync("", 0, 101));
        checks++;
        await Reject<ArgumentOutOfRangeException>(() => search.ScanNextAsync("", 7));
        checks++;
        await Reject<ArgumentOutOfRangeException>(() => search.ScanNextAsync(new string('x',129)));
        checks++;
        await Reject<UnauthorizedAccessException>(() => Task.FromResult(new IncrementalPlayerSearch(
            new FakeSource(2,true))));
        checks++;

        var broken = new FakeSource(5,false) { BadIndex = 1 };
        var scan = new IncrementalPlayerSearch(broken);
        var result = await scan.ScanNextAsync("", 0, 3);
        Check(result.Scanned == 3 && result.Failures.Count == 1 &&
            result.Failures[0].Index == 1 && result.Matches.Count == 2 &&
            result.NextIndex == 3, "Falha de registro isolada");
        Check(broken.ReadCount == 3, "Leitura além do limite solicitado");

        var stopped = new FakeSource(4,false) { LostProcessIndex = 1 };
        await Reject<IOException>(() => new IncrementalPlayerSearch(stopped).ScanNextAsync("",0,4));
        checks++;
        Check(stopped.ReadCount == 2, "Erro fatal não interrompeu busca");

        var disposed = new FakeSource(4,false) { DisposedIndex = 1 };
        await Reject<ObjectDisposedException>(() => new IncrementalPlayerSearch(disposed).ScanNextAsync("",0,4));
        checks++;
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Reject<OperationCanceledException>(() => search.ScanNextAsync("",0,2,cts.Token));
        checks++;

        Check(!MinimalPlayerIdentityMap.Research802V1.IsApprovedForOperationalUse,
            "Mapa operacional indevidamente liberado");
        Console.WriteLine($"OK: {checks} verificações de pesquisa incremental e tratamento de falhas");
    }

    private static async Task Reject<T>(Func<Task> action) where T : Exception
    {
        try { await action(); }
        catch (T) { return; }
        throw new Exception("Era esperada " + typeof(T).Name);
    }

    private sealed class FakeSource(int count, bool isLive) : IIndexedPlayerSource
    {
        public int Count => count;
        public bool IsLiveProcessSource => isLive;
        public int? BadIndex { get; init; }
        public int? LostProcessIndex { get; init; }
        public int? DisposedIndex { get; init; }
        public int ReadCount { get; private set; }
        public Task<MinimalPlayerIdentity> ReadAtAsync(int index, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            ReadCount++;
            if (index == BadIndex) throw new InvalidDataException("Ponteiro inválido");
            if (index == LostProcessIndex) throw new IOException("Processo encerrou");
            if (index == DisposedIndex) throw new ObjectDisposedException("Sessão");
            return Task.FromResult(new MinimalPlayerIdentity(index,0,index + 1,
                "Jogador", "Teste", null));
        }
    }
}
