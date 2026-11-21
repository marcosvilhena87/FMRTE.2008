using FMRTE2008.Domain;

namespace FMRTE2008.Application;

/// <summary>Closed fixture catalog: cannot accept a source or connect to an FM process.</summary>
public sealed class OfflineExpandedRoster
{
    private readonly IReadOnlyList<OfflineExpandedPlayer> _players;

    public OfflineExpandedRoster()
    {
        var baseProfiles = new DemonstrationProfileSource().Snapshot();
        if (baseProfiles.Count != 3) throw new InvalidOperationException("Unexpected demonstration fixture set.");
        var clubs = new[]
        {
            new OfflineClub("CLUB-001","Clube Exemplo A","Cidade Inventada A","País Fictício"),
            new OfflineClub("CLUB-002","Clube Exemplo B","Cidade Inventada B","País Fictício"),
            new OfflineClub("CLUB-003","Clube Exemplo C","Cidade Inventada C","País Fictício")
        };
        var players = new OfflineExpandedPlayer[baseProfiles.Count];
        for (var i = 0; i < players.Length; i++)
        {
            var profile = baseProfiles[i];
            if (!profile.IsSynthetic)
                throw new InvalidOperationException("Invalid fixture grouping.");
            players[i] = new OfflineExpandedPlayer(profile,clubs[i],
                OfflineAttributeCategoryCatalog.Categorize(profile.Attributes));
        }
        _players = Array.AsReadOnly(players);
    }

    public IReadOnlyList<OfflineExpandedPlayer> Snapshot() => _players;
}

public sealed record OfflineRosterState(
    IReadOnlyList<OfflineExpandedPlayer> VisiblePlayers,
    OfflineExpandedPlayer? Selected,
    int SelectedIndex,
    bool HasPrevious,
    bool HasNext);

/// <summary>Navigation stays in an isolated, immutable synthetic snapshot.</summary>
public sealed class OfflineRosterPresenter
{
    private readonly IReadOnlyList<OfflineExpandedPlayer> _all;
    private IReadOnlyList<OfflineExpandedPlayer> _visible = Array.Empty<OfflineExpandedPlayer>();
    private int _index = -1;

    public OfflineRosterPresenter() : this(OfflineRosterScenario.Baseline) { }

    public OfflineRosterPresenter(OfflineRosterScenario scenario)
    {
        _all = scenario switch
        {
            OfflineRosterScenario.Baseline => new OfflineExpandedRoster().Snapshot(),
            OfflineRosterScenario.CollisionStress => OfflineCollisionFixtures.Create(),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        if (_all.Select(p => p.Profile.DemonstrationId).Distinct(StringComparer.Ordinal).Count() != _all.Count)
            throw new InvalidOperationException("Duplicate synthetic player IDs.");
        if (_all.Any(p => !p.IsSynthetic || !p.Profile.IsSynthetic))
            throw new InvalidOperationException("Non-synthetic profile rejected.");
    }

    public OfflineRosterState Search(string? query)
    {
        query = (query ?? "").Trim();
        if (query.Length > 120) throw new ArgumentOutOfRangeException(nameof(query));
        _visible = Array.AsReadOnly(_all.Where(p =>
            p.Profile.DisplayName.Contains(query,StringComparison.OrdinalIgnoreCase) ||
            p.Profile.DemonstrationId.Contains(query,StringComparison.OrdinalIgnoreCase) ||
            p.Profile.Position.Contains(query,StringComparison.OrdinalIgnoreCase) ||
            p.Club.Name.Contains(query,StringComparison.OrdinalIgnoreCase) ||
            p.Club.DemonstrationId.Contains(query,StringComparison.OrdinalIgnoreCase)).ToArray());
        _index = _visible.Count > 0 ? 0 : -1;
        return Current();
    }

    /// <summary>Closed exact-ID club filtering, independent of other searchable text.</summary>
    public OfflineRosterState FilterByClubId(string clubId)
    {
        ArgumentNullException.ThrowIfNull(clubId);
        _visible = Array.AsReadOnly(_all.Where(p =>
            string.Equals(p.Club.DemonstrationId, clubId, StringComparison.Ordinal)).ToArray());
        _index = _visible.Count > 0 ? 0 : -1;
        return Current();
    }

    public OfflineRosterState Select(string? demonstrationId)
    {
        _index = demonstrationId is null ? -1 :
            Enumerable.Range(0,_visible.Count).FirstOrDefault(i =>
                string.Equals(_visible[i].Profile.DemonstrationId,demonstrationId,StringComparison.Ordinal),-1);
        return Current();
    }

    public OfflineRosterState Next()
    {
        if (_index >= 0 && _index + 1 < _visible.Count) _index++;
        return Current();
    }

    public OfflineRosterState Previous()
    {
        if (_index > 0) _index--;
        return Current();
    }

    public OfflineRosterState Current() =>
        new(_visible,_index < 0 ? null : _visible[_index],_index,
            _index > 0,_index >= 0 && _index + 1 < _visible.Count);
}
