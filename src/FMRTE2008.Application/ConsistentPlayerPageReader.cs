using FMRTE2008.Domain;

namespace FMRTE2008.Application;

/// <summary>
/// Bounded, fail-closed page reader over an injected, read-only source.
/// Samples all page slots before decoding, rechecks each record, then verifies
/// the complete page again before publishing any results.
/// Not an atomic snapshot: ABA and mutations outside observed slots remain possible.
/// </summary>
public sealed class ConsistentPlayerPageReader(IProcessReader memory, MinimalPlayerIdentityMap map)
{
    public async Task<IReadOnlyList<MinimalPlayerIdentity>> ReadAsync(
        uint headerAddress, int pageNumber, int pageSize, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(map);
        if (pageNumber < 1) throw new ArgumentOutOfRangeException(nameof(pageNumber));
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize));
        ct.ThrowIfCancellationRequested();

        var reader = new PlayerListReader(memory);
        var pointers = new PointerTableReader(memory);
        var header = await reader.ReadHeaderAsync(headerAddress, ct);
        long start = ((long)pageNumber - 1L) * pageSize;
        if (start >= header.Count)
        {
            await ConfirmHeaderAsync();
            return Array.Empty<MinimalPlayerIdentity>();
        }

        int end = (int)Math.Min((long)header.Count, start + pageSize);
        var expected = new uint[end - (int)start];
        for (int i = (int)start; i < end; i++)
        {
            ct.ThrowIfCancellationRequested();
            expected[i - (int)start] = await pointers.ReadPointerAtIndexAsync(
                header.TableAddress, i, header.Count, ct);
        }
        await ConfirmHeaderAsync();

        var decoder = new MinimalPlayerIdentityReader(memory, map);
        var records = new List<MinimalPlayerIdentity>(expected.Length);
        var seenIds = new HashSet<int>();
        var seenPointers = new HashSet<uint>();
        for (int i = (int)start; i < end; i++)
        {
            ct.ThrowIfCancellationRequested();
            await ConfirmHeaderAsync();
            var item = await decoder.ReadAsync(header, i, ct);
            uint original = expected[i - (int)start];
            if (item.PlayerPointer != original ||
                !seenIds.Add(item.Id) || !seenPointers.Add(item.PlayerPointer))
                throw new InvalidDataException("F05: registro alterado ou duplicado na página");
            await ConfirmHeaderAsync();
            if (await pointers.ReadPointerAtIndexAsync(header.TableAddress, i, header.Count, ct) != original)
                throw new InvalidDataException("F05: ponteiro modificado durante decodificação");
            records.Add(item);
        }

        // Re-read ALL captured slots: a previously accepted slot might have
        // changed while a later record was being decoded.
        await ConfirmHeaderAsync();
        for (int i = (int)start; i < end; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (await pointers.ReadPointerAtIndexAsync(header.TableAddress, i, header.Count, ct) !=
                expected[i - (int)start])
                throw new InvalidDataException("F05: página alterada após leitura dos registros");
        }
        await ConfirmHeaderAsync();
        // A change to an earlier slot while a later slot was being checked
        // in the forward sweep can be observed by an additional reverse sweep.
        // Finite sweeps are not an atomic snapshot and cannot eliminate ABA.
        for (int i = end - 1; i >= (int)start; i--)
        {
            ct.ThrowIfCancellationRequested();
            if (await pointers.ReadPointerAtIndexAsync(header.TableAddress, i, header.Count, ct) !=
                expected[i - (int)start])
                throw new InvalidDataException("F05: ponteiro divergiu durante a conferência final");
        }
        await ConfirmHeaderAsync();
        ct.ThrowIfCancellationRequested();
        return records.AsReadOnly();

        async Task ConfirmHeaderAsync()
        {
            if (await reader.ReadHeaderAsync(headerAddress, ct) != header)
                throw new InvalidDataException("F05: cabeçalho/tabela/quantidade divergente");
        }
    }
}
