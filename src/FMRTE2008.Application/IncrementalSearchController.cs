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
        if (IsFailed)
            throw new InvalidOperationException("Sessão encerrada; reinicie a pesquisa");
        try
        {
            var result = await _scanner.ScanNextAsync(_query, _cursor, batchSize, cancellationToken);
            _matches.AddRange(result.Matches);
            _failures.AddRange(result.Failures);
            _cursor = result.NextIndex;
            return result;
        }
        catch (OperationCanceledException)
        {
            // Cancellation may interrupt a batch mid-record; discard the cursor
            // and require a fresh scan to avoid presenting a partial batch.
            IsFailed = true;
            FailureReason = "Pesquisa cancelada; reinicie para continuar";
            throw;
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception
            or ObjectDisposedException or InvalidOperationException)
        {
            IsFailed = true;
            FailureReason = "Fonte indisponível; reinicie para uma nova sessão";
            throw;
        }
    }
}
