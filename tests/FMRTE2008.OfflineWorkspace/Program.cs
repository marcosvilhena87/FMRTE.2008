using System.Reflection;
using FMRTE2008.Application;
using FMRTE2008.Domain;

static class F28Tests
{
    static int checks;
    static void Check(bool yes, string reason)
    {
        if (!yes) throw new Exception("F28 failed: " + reason);
        checks++;
    }
    static void Reject<T>(Action f, string why) where T:Exception
    {
        try { f(); } catch(T) { checks++; return; }
        throw new Exception("F28 invalid action accepted: " + why);
    }

    static void Main()
    {
        var presenter = new OfflineWorkspacePresenter();
        var init = presenter.Current();
        Check(init.IsSynthetic && init.Roster.Selected is not null, "initial selected fixture");
        Check(init.Roster.VisiblePlayers.Count == 3, "three invented players");
        Check(init.SelectedClub?.DemonstrationId == "CLUB-001", "initial club match");
        Check(init.SelectedContract?.Club == init.SelectedClub?.Name, "club contract consistent");
        Check(init.Technical.Count + init.Mental.Count + init.Physical.Count ==
              init.Roster.Selected?.Profile.Attributes.Count, "all attributes classified");
        Check(init.Technical.All(a => OfflineAttributeCategoryCatalog.Resolve(a.Name)==OfflineAttributeCategory.Technical), "technical detail");
        Check(init.Mental.All(a => OfflineAttributeCategoryCatalog.Resolve(a.Name)==OfflineAttributeCategory.Mental), "mental detail");
        Check(init.Physical.All(a => OfflineAttributeCategoryCatalog.Resolve(a.Name)==OfflineAttributeCategory.Physical), "physical detail");
        Check(init.ClubPlayers.Count == 1 &&
              init.ClubPlayers.All(p => p.Club.DemonstrationId == init.SelectedClub?.DemonstrationId), "club members isolated");
        Check(init.Roster.SelectedIndex == 0 && !init.Roster.HasPrevious && init.Roster.HasNext, "first boundary");
        var next = presenter.NextPlayer();
        Check(next.Roster.Selected?.Profile.DemonstrationId == "DEMO-002", "next player");
        Check(next.SelectedClub?.DemonstrationId == "CLUB-002" && next.SelectedContract?.WeeklyWage == 1700m, "synchronized detail");
        Check(presenter.NextPlayer().Roster.Selected?.Profile.DemonstrationId == "DEMO-003", "last player");
        Check(!presenter.NextPlayer().Roster.HasNext, "last boundary");
        Check(presenter.PreviousPlayer().Roster.Selected?.Profile.DemonstrationId == "DEMO-002", "backward navigation");
        var filtered = presenter.Search("Alex");
        Check(filtered.Roster.VisiblePlayers.Count == 1 &&
              filtered.Roster.Selected?.Profile.DemonstrationId == "DEMO-001", "search filtered selection");
        Check(!filtered.Roster.HasPrevious && !filtered.Roster.HasNext, "single result boundaries");
        Check(presenter.SelectPlayer("DEMO-002").Roster.Selected is null, "outside filter refused");
        Check(presenter.Current().SelectedClub is null && presenter.Current().ClubPlayers.Count == 0, "cleared selection clears club");
        Check(presenter.NextPlayer().Roster.Selected is null, "navigation cannot revive cleared selection");
        Check(presenter.SelectPlayer("DEMO-001").SelectedClub?.DemonstrationId == "CLUB-001", "restore selection");
        Check(presenter.ShowClub("CLUB-001").Roster.VisiblePlayers.Count == 1, "club workspace navigation");
        // R01: exact club IDs must not match prefixes or similar name fragments.
        var closedRoster = new OfflineRosterPresenter();
        Check(closedRoster.FilterByClubId("CLUB-002").VisiblePlayers.Count == 1 &&
              closedRoster.Current().Selected?.Club.DemonstrationId == "CLUB-002",
              "exact club-ID selection");
        Check(closedRoster.FilterByClubId("CLUB-00").VisiblePlayers.Count == 0,
              "partial club-ID rejected");
        Check(closedRoster.FilterByClubId("CLUB-002 ").VisiblePlayers.Count == 0,
              "club-ID whitespace rejected");
        Check(closedRoster.FilterByClubId("CLUB-999").VisiblePlayers.Count == 0,
              "unknown club-ID rejected");
        Reject<ArgumentNullException>(() => closedRoster.FilterByClubId(null!),
              "null club-ID rejected");
        Check(closedRoster.Search("CLUB-003").VisiblePlayers.Count == 1 &&
              closedRoster.Current().Selected?.Club.DemonstrationId == "CLUB-003",
              "querying the displayed club ID reproduces exact club selection");
        Check(presenter.ShowClub("CLUB-001").Roster.VisiblePlayers.All(p =>
              p.Club.DemonstrationId == "CLUB-001"), "ShowClub never includes other clubs");

        Check(presenter.ShowClub("CLUB-999").Roster.Selected is null, "forged club denied");
        Check(presenter.ShowClub(null).SelectedClub is null, "null club denied");
        var empty = presenter.Search("não existe");
        Check(empty.Roster.VisiblePlayers.Count == 0 && empty.Roster.Selected is null, "empty search");
        Check(empty.SelectedClub is null && empty.SelectedContract is null &&
              empty.ClubPlayers.Count == 0 && empty.Technical.Count == 0 &&
              empty.Mental.Count == 0 && empty.Physical.Count == 0, "no stale details");
        Check(!empty.Roster.HasNext && !empty.Roster.HasPrevious, "empty navigation guards");
        Check(presenter.ShowClub("CLUB-001").Roster.Selected is null, "hidden club cannot open");
        Check(presenter.Search("clube exemplo c").SelectedClub?.DemonstrationId == "CLUB-003", "case insensitive club search");
        Check(presenter.Search("  DEMO-002  ").Roster.Selected?.Profile.DemonstrationId == "DEMO-002", "trimmed ID search");
        var expectedContract = presenter.Current().SelectedContract;
        Check(expectedContract is not null &&
              expectedContract.Club == "Clube Exemplo B" &&
              expectedContract.StartsOn == new DateOnly(2008,1,1) &&
              expectedContract.EndsOn == new DateOnly(2011,12,31) &&
              expectedContract.WeeklyWage == 1700m, "full contract terms for DEMO-002");
        Check(presenter.Search("").Roster.VisiblePlayers.Count == 3, "reset to all");
        var before = presenter.Current();
        Reject<ArgumentOutOfRangeException>(()=>presenter.Search(new string('x',121)), "long query");
        Check(presenter.Current().Roster.Selected?.Profile.DemonstrationId ==
              before.Roster.Selected?.Profile.DemonstrationId, "invalid query preserves state");
        var expose = presenter.Current();
        Check(expose.Roster.VisiblePlayers is IList<OfflineExpandedPlayer>, "roster readonly interface");
        Reject<NotSupportedException>(()=>((IList<OfflineExpandedPlayer>)expose.Roster.VisiblePlayers).Clear(), "roster mutation");
        Reject<NotSupportedException>(()=>((IList<OfflineExpandedPlayer>)expose.ClubPlayers).Clear(), "club players mutation");
        Reject<NotSupportedException>(()=>((IList<OfflineAttribute>)expose.Technical).Clear(), "attribute mutation");
        Check(typeof(OfflineWorkspacePresenter).GetConstructors().All(c => c.GetParameters().Length==0), "no externally supplied source");
        Check(!typeof(OfflineWorkspacePresenter).GetMethods(BindingFlags.Public|BindingFlags.Instance)
            .Any(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Source"))), "no source injection");
        Console.WriteLine($"F28 PASS: {checks} deterministic presenter, navigation, club and safety assertions");
    }
}
