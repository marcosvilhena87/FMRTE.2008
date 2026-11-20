using FMRTE2008.Domain;

namespace FMRTE2008.Application;

/// <summary>
/// Session-scoped, bounded consistency across independently requested pages.
/// Uses only injected IProcessReader and never opens a process or approves maps.
/// Call only under VerifiedPlayerReadSession's serialized GuardedReadAsync.
/// </summary>
public sealed class CrossPageSnapshotReader(IProcessReader memory, MinimalPlayerIdentityMap map,
    uint headerAddress)
{
    public const int MaxTrackedRecords = 512;
    private PlayerListHeader? _baseline;
    private readonly Dictionary<int, MinimalPlayerIdentity> _observed = new();
    private readonly ConsistentPlayerPageReader _pages = new(memory, map);

    public async Task<IReadOnlyList<MinimalPlayerIdentity>> ReadPageAsync(
        int pageNumber, int pageSize, CancellationToken ct = default)
    {
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        ct.ThrowIfCancellationRequested();

        // The captured header belongs to this logical verified session,
        // not to an individual invocation or user-selected page size.
        PlayerListHeader initial = await ReadAndConfirmHeaderAsync(ct);
        await ConfirmObservedAsync(ct);

        IReadOnlyList<MinimalPlayerIdentity> first =
            await _pages.ReadAsync(headerAddress, pageNumber, pageSize, ct);
        foreach (var item in first)
        {
            if (_observed.TryGetValue(item.Index, out var prior) && prior != item)
                throw new InvalidDataException("F06: jogador divergiu de página anteriormente publicada");
            if (_observed.Any(prior => prior.Key != item.Index &&
                (prior.Value.Id == item.Id || prior.Value.PlayerPointer == item.PlayerPointer)))
                throw new InvalidDataException("F06: ID ou ponteiro duplicado entre páginas");
        }

        int extra = first.Count(item => !_observed.ContainsKey(item.Index));
        if (_observed.Count + extra > MaxTrackedRecords)
            throw new InvalidDataException("F06: limite seguro de registros observados excedido");

        // Prior pages may mutate while the new page is being read. Revisit all
        // previously accepted identities, including names and IDs, not only slots.
        await ConfirmObservedAsync(ct);
        // A second full read of the requested page catches changes introduced
        // while checking previously accepted pages.
        IReadOnlyList<MinimalPlayerIdentity> second =
            await _pages.ReadAsync(headerAddress, pageNumber, pageSize, ct);
        if (!first.SequenceEqual(second))
            throw new InvalidDataException("F06: página divergiu entre leituras");
        await ConfirmObservedAsync(ct);
        if (await new PlayerListReader(memory).ReadHeaderAsync(headerAddress, ct) != initial)
            throw new InvalidDataException("F06: cabeçalho mudou durante a chamada");
        ct.ThrowIfCancellationRequested();

        // Commit only on success. Returned arrays are fresh, read-only snapshots.
        // Earlier snapshots never alias this dictionary or future buffers.
        foreach (var item in second)
            _observed[item.Index] = item;
        _baseline ??= initial;
        return Array.AsReadOnly(second.ToArray());
    }

    private async Task<PlayerListHeader> ReadAndConfirmHeaderAsync(CancellationToken ct)
    {
        var current = await new PlayerListReader(memory).ReadHeaderAsync(headerAddress, ct);
        if (_baseline is not null && current != _baseline)
            throw new InvalidDataException("F06: cabeçalho/tabela/quantidade mudaram entre páginas");
        return current;
    }

    private async Task ConfirmObservedAsync(CancellationToken ct)
    {
        foreach (var prior in _observed.OrderBy(item => item.Key))
        {
            ct.ThrowIfCancellationRequested();
            await ReadAndConfirmHeaderAsync(ct);
            // A one-record F05 page checks this exact table slot and the
            // complete decoded identity, regardless of the page size requested.
            var confirmed = await _pages.ReadAsync(headerAddress, prior.Key + 1, 1, ct);
            if (confirmed.Count != 1 || confirmed[0] != prior.Value)
                throw new InvalidDataException("F06: identidade previamente observada mudou");
        }
        await ReadAndConfirmHeaderAsync(ct);
    }
}
