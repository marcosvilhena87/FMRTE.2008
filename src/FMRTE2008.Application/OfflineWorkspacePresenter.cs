using FMRTE2008.Domain;

namespace FMRTE2008.Application;

/// <summary>Immutable presentation snapshot for the strictly fictitious F28 workspace.</summary>
public sealed record OfflineWorkspaceState(
    OfflineRosterState Roster,
    OfflineClub? SelectedClub,
    OfflineContract? SelectedContract,
    IReadOnlyList<OfflineExpandedPlayer> ClubPlayers,
    IReadOnlyList<OfflineAttribute> Technical,
    IReadOnlyList<OfflineAttribute> Mental,
    IReadOnlyList<OfflineAttribute> Physical)
{
    public bool IsSynthetic => true;
}

/// <summary>
/// Coordinates player and club views exclusively from the closed F27 fixture roster.
/// No source injection, process IDs, memory offsets, save paths, file I/O or writes.
/// </summary>
public sealed class OfflineWorkspacePresenter
{
    private readonly OfflineRosterPresenter _roster = new();

    public OfflineWorkspacePresenter()
    {
        _roster.Search("");
    }

    public OfflineWorkspaceState Current() => Describe(_roster.Current());

    public OfflineWorkspaceState Search(string? query) => Describe(_roster.Search(query));

    public OfflineWorkspaceState SelectPlayer(string? demonstrationId) =>
        Describe(_roster.Select(demonstrationId));

    public OfflineWorkspaceState NextPlayer() => Describe(_roster.Next());

    public OfflineWorkspaceState PreviousPlayer() => Describe(_roster.Previous());

    public OfflineWorkspaceState ShowClub(string? clubId)
    {
        if (clubId is null) return Describe(_roster.Select(null));
        var current = _roster.Current();
        var club = current.VisiblePlayers.Select(x => x.Club)
            .FirstOrDefault(x => string.Equals(x.DemonstrationId, clubId, StringComparison.Ordinal));
        // Only current, previously visible fictional clubs may be opened.
        if (club is null) return Describe(_roster.Select(null));
        return Describe(_roster.FilterByClubId(club.DemonstrationId));
    }

    private static OfflineWorkspaceState Describe(OfflineRosterState state)
    {
        var chosen = state.Selected;
        var clubPlayers = chosen is null ? Array.Empty<OfflineExpandedPlayer>() :
            state.VisiblePlayers.Where(x => x.Club.DemonstrationId == chosen.Club.DemonstrationId).ToArray();
        return new OfflineWorkspaceState(
            state,
            chosen?.Club,
            chosen?.Profile.Contract,
            Array.AsReadOnly(clubPlayers),
            chosen?.AttributesFor(OfflineAttributeCategory.Technical) ?? Array.Empty<OfflineAttribute>(),
            chosen?.AttributesFor(OfflineAttributeCategory.Mental) ?? Array.Empty<OfflineAttribute>(),
            chosen?.AttributesFor(OfflineAttributeCategory.Physical) ?? Array.Empty<OfflineAttribute>());
    }
}
