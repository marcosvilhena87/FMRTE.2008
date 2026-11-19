using System.Globalization;

namespace FMRTE2008.Application;

/// <summary>Reader for a bounded index range. Not connected to live processes by default.</summary>
public interface IIndexedPlayerSource
{
    int Count { get; }
    bool IsLiveProcessSource { get; }
    Task<MinimalPlayerIdentity> ReadAtAsync(int index, CancellationToken cancellationToken);
}

public sealed record PlayerReadFailure(int Index, string Reason);
public sealed record IncrementalPlayerSearchResult(
    IReadOnlyList<MinimalPlayerIdentity> Matches,
    IReadOnlyList<PlayerReadFailure> Failures,
    int NextIndex, int TotalCount, bool Complete, int Scanned);

/// <summary>
/// Resumable search; at most MaxBatchSize records examined per call.
/// Invalid entries are reported and skipped, while cancellation and process loss propagate.
/// No process handles are created here, and production sources are explicitly rejected.
/// </summary>
public sealed class IncrementalPlayerSearch
{
    public const int MaxBatchSize = 100;
    public const int MaxQueryLength = 128;
    private readonly IIndexedPlayerSource _source;

    public IncrementalPlayerSearch(IIndexedPlayerSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.IsLiveProcessSource)
            throw new UnauthorizedAccessException("Pesquisa em processo real bloqueada até aprovação formal");
        if (source.Count < 0 || source.Count > PlayerListReader.MaxFixtureEntries)
            throw new ArgumentOutOfRangeException(nameof(source), "Contagem inválida");
        _source = source;
    }

    public async Task<IncrementalPlayerSearchResult> ScanNextAsync(
        string? query, int nextIndex = 0, int batchSize = 20, CancellationToken cancellationToken = default)
    {
        if (nextIndex < 0 || nextIndex > _source.Count)
            throw new ArgumentOutOfRangeException(nameof(nextIndex));
        if (batchSize is < 1 or > MaxBatchSize)
            throw new ArgumentOutOfRangeException(nameof(batchSize));
        string term = (query ?? "").Trim();
        if (term.Length > MaxQueryLength)
            throw new ArgumentOutOfRangeException(nameof(query));

        int end = (int)Math.Min((long)_source.Count, (long)nextIndex + batchSize);
        var matches = new List<MinimalPlayerIdentity>();
        var failures = new List<PlayerReadFailure>();
        int cursor = nextIndex;
        for (; cursor < end; cursor++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                MinimalPlayerIdentity player = await _source.ReadAtAsync(cursor, cancellationToken);
                if (player.Index != cursor || player.Id <= 0)
                    throw new InvalidDataException("Índice ou ID inconsistente");
                if (Matches(player, term)) matches.Add(player);
            }
            catch (Exception ex) when (ex is InvalidDataException or OverflowException or ArgumentOutOfRangeException)
            {
                failures.Add(new PlayerReadFailure(cursor, ex.GetType().Name));
            }
            // IOException, Win32Exception, ObjectDisposedException and cancellations
            // are fatal: stop scanning and allow the UI to release the session.
        }
        return new IncrementalPlayerSearchResult(matches, failures, cursor,
            _source.Count, cursor == _source.Count, cursor - nextIndex);
    }

    private static bool Matches(MinimalPlayerIdentity p, string query)
    {
        if (query.Length == 0) return true;
        if (int.TryParse(query, NumberStyles.None, CultureInfo.InvariantCulture, out int id) && p.Id == id)
            return true;
        return Contains(p.FirstName, query) || Contains(p.LastName, query) ||
            Contains(p.CommonName, query) || Contains(p.FirstName + " " + p.LastName, query);
    }

    private static bool Contains(string? value, string term) =>
        value is not null && CultureInfo.InvariantCulture.CompareInfo.IndexOf(
            value, term, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}

/// <summary>Fixed fixture provider for safety tests. No operating-system access.</summary>
public sealed class DemonstrationIndexedPlayerSource : IIndexedPlayerSource
{
    private readonly IReadOnlyList<MinimalPlayerIdentity> _items =
        new DemonstrationPlayerSource().ReadSnapshot();
    public int Count => _items.Count;
    public bool IsLiveProcessSource => false;
    public Task<MinimalPlayerIdentity> ReadAtAsync(int index, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
        return Task.FromResult(_items[index]);
    }
}
