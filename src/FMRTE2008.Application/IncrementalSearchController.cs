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
    private readonly object _stateGate = new();
    private bool _isFailed;
    private string? _failureReason;
    public bool IsFailed { get { lock (_stateGate) return _isFailed; } }
    public string? FailureReason { get { lock (_stateGate) return _failureReason; } }
    public int NextIndex { get { lock (_stateGate) return _cursor; } }
    public int TotalCount { get { lock (_stateGate) return _count; } }
    public bool Complete { get { lock (_stateGate) return _cursor >= _count; } }
    // Copy instead of exposing a live wrapper over a mutable collection.
    // A UI reader receives a stable snapshot even during Restart/NextAsync.
    public IReadOnlyList<MinimalPlayerIdentity> Matches
    {
        get { lock (_stateGate) return Array.AsReadOnly(_matches.ToArray()); }
    }
    public IReadOnlyList<PlayerReadFailure> Failures
    {
        get { lock (_stateGate) return Array.AsReadOnly(_failures.ToArray()); }
    }

    public IncrementalSearchController(Func<IIndexedPlayerSource> sourceFactory)
    {
        _sourceFactory = sourceFactory ?? throw new ArgumentNullException(nameof(sourceFactory));
        _scanner = CreateScanner(out _count);
    }

    private IncrementalPlayerSearch CreateScanner(out int count)
    {
        var source = _sourceFactory() ?? throw new InvalidOperationException("Fonte indisponível");
        var scanner = new IncrementalPlayerSearch(source);
        count = source.Count;
        return scanner;
    }

    /// <summary>Start a new search, without reading any record.</summary>
    public void Restart(string? query)
    {
        string term = (query ?? "").Trim();
        if (term.Length > IncrementalPlayerSearch.MaxQueryLength)
            throw new ArgumentOutOfRangeException(nameof(query));
        // Build the replacement first. A failed factory must not corrupt the
        // current state or make a pending result appear to belong to a new run.
        var replacement = CreateScanner(out int replacementCount);
        lock (_stateGate)
        {
            _generation++;
            _query = term;
            _cursor = 0;
            _matches.Clear();
            _failures.Clear();
            _isFailed = false;
            _failureReason = null;
            _scanner = replacement;
            _count = replacementCount;
        }
    }

    public async Task<IncrementalPlayerSearchResult> NextAsync(
        int batchSize = 3, CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _inFlight, 1, 0) != 0)
            throw new InvalidOperationException("Já existe uma leitura em andamento");
        try
        {
            int generation;
            IncrementalPlayerSearch scanner;
            int cursor;
            string query;
            lock (_stateGate)
            {
                if (_isFailed)
                    throw new InvalidOperationException("Sessão encerrada; reinicie a pesquisa");
                generation = _generation;
                scanner = _scanner;
                cursor = _cursor;
                query = _query;
            }
            try
            {
                var result = await scanner.ScanNextAsync(query, cursor, batchSize, cancellationToken);
                // Commit and generation check MUST be atomic with Restart.
                lock (_stateGate)
                {
                    if (generation != _generation)
                        throw new InvalidOperationException("Sessão substituída durante a leitura");
                    _matches.AddRange(result.Matches);
                    _failures.AddRange(result.Failures);
                    _cursor = result.NextIndex;
                }
                return result;
            }
            catch (OperationCanceledException)
            {
                lock (_stateGate)
                {
                    if (generation == _generation)
                        Invalidate("Pesquisa cancelada; reinicie para continuar");
                }
                throw;
            }
            catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception
                or ObjectDisposedException or InvalidOperationException or InvalidDataException)
            {
                lock (_stateGate)
                {
                    if (generation == _generation)
                        Invalidate("Fonte indisponível ou dados inconsistentes; reinicie para uma nova sessão");
                }
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
        _isFailed = true;
        _failureReason = reason;
        _cursor = 0;
        _matches.Clear();
        _failures.Clear();
        _generation++;
    }
}
