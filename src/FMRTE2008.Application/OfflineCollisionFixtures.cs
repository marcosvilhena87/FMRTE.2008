using FMRTE2008.Domain;

namespace FMRTE2008.Application;

/// <summary>Closed scenarios; no caller-supplied source, process, path, or network data.</summary>
public enum OfflineRosterScenario { Baseline = 0, CollisionStress = 1 }

/// <summary>Deterministic, entirely invented adversarial fixtures for F29.</summary>
internal static class OfflineCollisionFixtures
{
    internal static IReadOnlyList<OfflineExpandedPlayer> Create()
    {
        var initial = new OfflineExpandedRoster().Snapshot();
        var a = initial[0];
        var b = initial[1];
        var c = initial[2];
        var nearNameClub = new OfflineClub("CLUB-004", "Clube Exemplo A II",
            "Cidade Inventada D", "País Fictício");
        OfflineExpandedPlayer Clone(string id, string name, OfflineExpandedPlayer sample,
            OfflineClub club)
        {
            var p = new OfflinePlayerProfile(id, name, sample.Profile.Position,
                sample.Profile.Attributes,
                new OfflineContract(club.Name, sample.Profile.Contract.StartsOn,
                    sample.Profile.Contract.EndsOn, sample.Profile.Contract.WeeklyWage));
            return new OfflineExpandedPlayer(p, club,
                OfflineAttributeCategoryCatalog.Categorize(p.Attributes));
        }

        return Array.AsReadOnly(new[]
        {
            a, b, c,
            Clone("DEMO-004", "Alex Exemplo", a, a.Club),
            Clone("DEMO-005", "Alex Exemplo", b, b.Club),
            Clone("DEMO-006", "CLUB-001 Jogador", c, nearNameClub)
        });
    }
}
