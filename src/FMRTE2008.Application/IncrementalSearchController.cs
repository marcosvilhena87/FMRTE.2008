namespace FMRTE2008.Application;

/// <summary>
/// Orchestrates small incremental scans for the UI. A fatal read failure closes
/// the current logical session; recovery creates a NEW source, never resumes
/// a possibly stale cursor. Production/live sources are refused by the scanner.
/// </summary>
public sealed class IncrementalSearchController
{
    private readonly Func<IIndexedPlayerSource> _sourceFactory;
    private IncrementalPlayerSearch _scanner;
    private readonly List<MinimalPlayerIdentity> _matches = [];
    private readonly List<PlayerReadFailure> _failures = [];
    private int _cursor;
    private int _count;
    private string _query = "";
    private int _generation;
    private int _inFlight;
    public bool IsFailed { get; private set; }
    public string? FailureReason { get; private set; }
    public int NextIndex => _cursor;
    public int TotalCount => _count;
    public bool Complete => _cursor >= _count;
    public IReadOnlyList<MinimalPlayerIdentity> Matches => _matches.AsReadOnly();
    public IReadOnlyList<PlayerReadFailure> Failures => _failures.AsReadOnly();

    public IncrementalSearchController(Func<IIndexedPlayerSource> sourceFactory)
    {
        _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
        _scanner = CreateScanner();
    }

    private IncrementalPlayerSearch CreateScanner()
    {
        var source = _sourceFactory() ?? throw new InvalidOperationException("Fonte indisponível");
        var scanner = new IncrementalPlayerSearch(source);
        _count = source.Count;
        return scanner;
    }

    /// <summary>Start a new search, without reading any record.</summary>
    public void Restart(string? query)
    {
        string term = (query ?? "").Trim();
        if (term.Length > IncrementalPlayerSearch.MaxQueryLength)
            throw new ArgumentOutOfRangeException(nameof(query));
        // Invalidate every scan started under the previous logical session.
        Interlocked.Increment(ref _generation);
        _query = term;
        _cursor = 0;
        _matches.Clear();
        _failures.Clear();
        IsFailed = false;
        FailureReason = null;
        _scanner = CreateScanner();
    }

    public async Task<IncrementalPlayerSearchResult> NextAsync(
        int batchSize = 3, CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0)
            throw new InvalidOperationException("Já existe uma leitura em andamento");
        try
        {
            if (IsFailed)
                throw new InvalidOperationException("Sessão encerrada; reinicie a pesquisa");

            int generation = Volatile.Read(ref _generation);
            var scanner = _scanner;
            int cursor = _cursor;
            string query = _query;
            try
            {
                var result = await scanner.ScanNextAsync(query, cursor, batchSize, cancellationToken);
                // Restart may have happened while the awaited source was reading.
                // Results and cursor from an older epoch must never be published.
                if (generation != Volatile.Read(ref _generation))
                    throw new InvalidOperationException("Sessão substituída durante a leitura");
                _matches.AddRange(result.Matches);
                _failures.AddRange(result.Failures);
                _cursor = result.NextIndex;
                return result;
            }
            catch (OperationCanceledException)
            {
                if (generation == Volatile.Read(ref _generation))
                    Invalidate("Pesquisa cancelada; reinicie para continuar");
                throw;
            }
            catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception
                or ObjectDisposedException or InvalidOperationException or InvalidDataException)
            {
                if (generation == Volatile.Read(ref _generation))
                    Invalidate("Fonte indisponível ou dados inconsistentes; reinicie para uma nova sessão");
                throw;
            }
        }
        finally
        {
            Volatile.Write(ref _inFlight, 0);
        }
    }

    private void Invalidate(string reason)
    {
        IsFailed = true;
        FailureReason = reason;
        _cursor = 0;
        _matches.Clear();
        _failures.Clear();
        Interlocked.Increment(ref _generation);
    }
}
