using FMRTE2008.Domain;

namespace FMRTE2008.Application;

/// <summary>
/// Experimental defensive reader over an injected IProcessReader only.
/// Never opens a process, cannot grant map approval, and has no write operations.
/// Confirms the header and player pointer are unchanged across a single identity read.
/// </summary>
public sealed class StablePlayerIdentitySnapshotReader(IProcessReader memory, MinimalPlayerIdentityMap map)
{
    public async Task<MinimalPlayerIdentity> ReadStableAsync(
        uint moduleBase, int index, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(map);
        cancellationToken.ThrowIfCancellationRequested();
        uint headerAddress = map.ResolveHeaderAddress(moduleBase);
        var reader = new PlayerListReader(memory);
        var first = await reader.ReadHeaderAsync(headerAddress, cancellationToken);
        if (index < 0 || index >= first.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        uint originalPointer = await new PointerTableReader(memory).ReadPointerAtIndexAsync(
            first.TableAddress, index, first.Count, cancellationToken);
        var identity = await new MinimalPlayerIdentityReader(memory, map).ReadAsync(
            first, index, cancellationToken);
        var second = await reader.ReadHeaderAsync(headerAddress, cancellationToken);
        if (first != second)
            throw new InvalidDataException("Cabeçalho alterado durante leitura: resultado descartado");
        uint currentPointer = await new PointerTableReader(memory).ReadPointerAtIndexAsync(
            second.TableAddress, index, second.Count, cancellationToken);
        if (originalPointer != currentPointer || identity.PlayerPointer != originalPointer)
            throw new InvalidDataException("Ponteiro do jogador alterado durante leitura: resultado descartado");
        return identity;
    }
}
