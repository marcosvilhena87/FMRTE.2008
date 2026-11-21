using FMRTE2008.Domain;

namespace FMRTE2008.Application;

/// <summary>UI-independent, fixture-only presenter. Never exposes a provider hook.</summary>
public sealed class OfflineProfilesPresenter
{
    private readonly OfflineProfileQueryService _queries = new();
    private OfflineProfilePage _current = new(Array.Empty<OfflinePlayerProfile>(),0,1,1);

    public OfflineProfilePage Search(string? query)
    {
        _current = _queries.Search(query,1,10);
        return _current;
    }

    // Only profiles visible in the current search result can be selected.
    public OfflinePlayerProfile? Select(string? demonstrationId) =>
        demonstrationId is null ? null :
        _current.Items.FirstOrDefault(p =>
            string.Equals(p.DemonstrationId,demonstrationId,StringComparison.Ordinal));
}
