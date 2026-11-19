using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

internal static class IncrementalControllerTests
{
    internal static async Task RunAsync()
    {
        int checks = 0;
        void Check(bool ok, string message)
        {
            if (!ok) throw new Exception(message);
            checks++;
        }
        var controller = new IncrementalSearchController(() => new DemonstrationIndexedPlayerSource());
        controller.Restart("maldini");
        Check(controller.NextIndex == 0 && controller.Matches.Count == 0, "Reset não reiniciou cursor");
        var batch = await controller.NextAsync(2);
        Check(batch.Scanned == 2 && controller.NextIndex == 2 && controller.Matches.Count == 0,
            "Primeiro lote incorreto");
        var second = await controller.NextAsync(2);
        Check(second.NextIndex == 4 && controller.Matches.Count == 1 &&
            controller.Matches[0].Id == 45, "Retomada após lote incorreta");
        controller.Restart("ostlund");
        Check(controller.NextIndex == 0 && controller.Matches.Count == 0, "Pesquisa nova herdou resultados");
        await controller.NextAsync(6);
        Check(controller.Complete && controller.Matches.Count == 1 &&
            controller.Matches[0].Id == 107, "Pesquisa completa após reinício");

        int creates = 0;
        var failed = new IncrementalSearchController(() =>
        {
            creates++;
            return new FailingSource(3, creates == 1);
        });
        await Reject<IOException>(() => failed.NextAsync(3));
        Check(failed.IsFailed && failed.NextIndex == 0 && failed.Matches.Count == 0,
            "Falha fatal não bloqueou cursor");
        await Reject<InvalidOperationException>(() => failed.NextAsync(3));
        Check(creates == 1, "Busca tentou criar fonte automaticamente após falha");
        failed.Restart("");
        Check(creates == 2 && !failed.IsFailed && failed.NextIndex == 0,
            "Recuperação não abriu nova fonte");
        var recovered = await failed.NextAsync(3);
        Check(recovered.Complete && recovered.Matches.Count == 3,
            "Nova sessão simulada não recuperou busca");

        var cancellable = new IncrementalSearchController(() => new DemonstrationIndexedPlayerSource());
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Reject<OperationCanceledException>(() => cancellable.NextAsync(2, cts.Token));
        Check(cancellable.IsFailed, "Cancelamento não invalidou a sessão lógica");
        cancellable.Restart("");
        Check(!cancellable.IsFailed && cancellable.NextIndex == 0,
            "Cancelamento não permitiu recuperação explícita");
        await cancellable.NextAsync(2);
        Check(cancellable.Matches.Count == 2, "Busca não retomou com fonte nova");

        await Reject<UnauthorizedAccessException>(() => Task.FromResult(
            new IncrementalSearchController(() => new LiveSource())));
        checks++;
        Console.WriteLine($"OK: {checks} verificações de encerramento, recuperação e bloqueio operacional");
    }

    private static async Task Reject<T>(Func<Task> call) where T : Exception
    {
        try { await call(); }
        catch (T) { return; }
        throw new Exception("Era esperada a exceção " + typeof(T).Name);
    }

    private sealed class FailingSource(int count, bool fail) : IIndexedPlayerSource
    {
        public int Count => count;
        public bool IsLiveProcessSource => false;
        public Task<MinimalPlayerIdentity> ReadAtAsync(int index, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (fail && index == 1) throw new IOException("Sessão simulada perdida");
            return Task.FromResult(new MinimalPlayerIdentity(index, 0, index + 1,
                "Jogador", "Teste", null));
        }
    }

    private sealed class LiveSource : IIndexedPlayerSource
    {
        public int Count => 1;
        public bool IsLiveProcessSource => true;
        public Task<MinimalPlayerIdentity> ReadAtAsync(int index, CancellationToken ct) =>
            throw new Exception("A fonte real nunca deve ser lida");
    }
}
