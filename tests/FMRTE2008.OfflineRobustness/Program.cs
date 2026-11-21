using System.Reflection;
using FMRTE2008.Application;
using FMRTE2008.Domain;

static class F29Checks
{
    static int passed;
    static void Check(bool valid, string description)
    {
        if (!valid) throw new Exception("F29 regression: " + description);
        passed++;
    }
    static void Reject<T>(Action act, string description) where T:Exception
    {
        try { act(); } catch(T) { passed++; return; }
        throw new Exception("F29 invalid case accepted: " + description);
    }

    static void Main()
    {
        var baseline = new OfflineWorkspacePresenter();
        Check(baseline.Current().Roster.VisiblePlayers.Count == 3, "F28 baseline unchanged");
        var roster = new OfflineRosterPresenter(OfflineRosterScenario.CollisionStress);
        var workspace = new OfflineWorkspacePresenter(OfflineRosterScenario.CollisionStress);
        var all = workspace.Current();
        Check(all.Roster.VisiblePlayers.Count == 6, "exact six synthetic fixtures");
        Check(all.Roster.VisiblePlayers.Select(x=>x.Profile.DemonstrationId).Distinct().Count()==6, "unique player IDs");
        Check(all.Roster.VisiblePlayers.All(x=>x.IsSynthetic && x.Profile.IsSynthetic), "no live profiles");
        Check(all.Roster.VisiblePlayers.Count(x=>x.Club.DemonstrationId=="CLUB-001")==2, "shared club A");
        Check(all.Roster.VisiblePlayers.Count(x=>x.Club.DemonstrationId=="CLUB-002")==2, "shared club B");
        Check(all.Roster.VisiblePlayers.Count(x=>x.Profile.DisplayName=="Alex Exemplo")==3, "intentional name collision");
        Check(all.Roster.VisiblePlayers.Single(x=>x.Profile.DemonstrationId=="DEMO-006").Club.DemonstrationId=="CLUB-004",
              "ID text inside player name belongs to another club");
        Check(all.Roster.VisiblePlayers.All(x=>x.Profile.Contract.Club==x.Club.Name), "all synthetic contracts match clubs");
        Check(all.Roster.VisiblePlayers.All(x=>x.CategorizedAttributes.All(a=>
              a.Category==OfflineAttributeCategoryCatalog.Resolve(a.Attribute.Name))), "closed category classification");

        var apparent = workspace.Search("CLUB-001");
        Check(apparent.Roster.VisiblePlayers.Count==3, "general text search intentionally finds ID inside name");
        Check(apparent.Roster.VisiblePlayers.Any(x=>x.Profile.DemonstrationId=="DEMO-006"), "collision included in text search");
        var exact = workspace.ShowClub("CLUB-001");
        Check(exact.Roster.VisiblePlayers.Count==2, "exact club filter excludes text collisions");
        Check(exact.Roster.VisiblePlayers.All(x=>x.Club.DemonstrationId=="CLUB-001"), "no other club leaks");
        Check(exact.Roster.SelectedIndex==0 && exact.Roster.HasNext && !exact.Roster.HasPrevious,
              "shared club starts with first member");
        Check(exact.ClubPlayers.Count==2, "shared club member count correct");
        var second=workspace.NextPlayer();
        Check(second.Roster.Selected?.Profile.DemonstrationId=="DEMO-004", "next selects second same-club member");
        Check(second.SelectedClub?.DemonstrationId=="CLUB-001" && second.ClubPlayers.Count==2,
              "club preserved on same-team navigation");
        Check(!second.Roster.HasNext && second.Roster.HasPrevious, "second member is end");
        Check(workspace.NextPlayer().Roster.SelectedIndex==1, "next clamps final member");
        Check(workspace.PreviousPlayer().Roster.Selected?.Profile.DemonstrationId=="DEMO-001", "previous navigates within club");
        Check(workspace.SelectPlayer("DEMO-006").Roster.Selected is null, "out-of-club selection refused");
        Check(workspace.Current().SelectedClub is null && workspace.Current().SelectedContract is null,
              "forged selection clears identity and contract");
        Check(workspace.NextPlayer().Roster.Selected is null, "no accidental selection revival");

        var duplicateNames=workspace.Search("Alex Exemplo");
        Check(duplicateNames.Roster.VisiblePlayers.Count==3, "all same-name players visible");
        Check(duplicateNames.Roster.VisiblePlayers.Select(x=>x.Profile.DemonstrationId).Distinct().Count()==3,
              "IDs distinguish duplicate names");
        Check(workspace.SelectPlayer("DEMO-005").SelectedClub?.DemonstrationId=="CLUB-002",
              "selection resolves correct ID despite duplicate name");
        var groupB=workspace.ShowClub("CLUB-002");
        Check(groupB.Roster.VisiblePlayers.Count==2 &&
              groupB.Roster.VisiblePlayers.All(x=>x.Club.DemonstrationId=="CLUB-002"),
              "club transition includes all members outside previous name filter");
        Check(workspace.SelectPlayer("DEMO-004").Roster.Selected is null,"other club excluded after transition");
        Check(workspace.Search("CLUB-004").Roster.VisiblePlayers.Count==1,"similar club name not mistaken for club ID");
        Check(workspace.ShowClub("CLUB-001").Roster.Selected is null, "hidden club denied");
        Check(workspace.Search("CLUB-004").SelectedClub?.DemonstrationId=="CLUB-004", "recovery from rejected club");
        Check(workspace.ShowClub("CLUB-004").Roster.VisiblePlayers.Count==1, "exact similar-name club");
        Check(workspace.Search("Clube Exemplo A").Roster.VisiblePlayers.Count==3,
              "substring name search sees A and A II");
        Check(workspace.ShowClub("CLUB-004").Roster.VisiblePlayers.Count==1,
              "exact ID beats near-identical club names");
        Check(workspace.Search("never exists").Roster.Selected is null, "empty search clears selection");
        Check(workspace.Current().ClubPlayers.Count==0 && workspace.Current().Technical.Count==0 &&
              workspace.Current().Mental.Count==0 && workspace.Current().Physical.Count==0,
              "all details removed after empty search");
        Check(workspace.PreviousPlayer().Roster.Selected is null,"previous remains inert after failure");
        Check(workspace.Search("").Roster.VisiblePlayers.Count==6,"reset recovers complete stress roster");
        var previous=workspace.Current();
        Reject<ArgumentOutOfRangeException>(()=>workspace.Search(new string('z',121)),"oversize search");
        Check(workspace.Current().Roster.Selected?.Profile.DemonstrationId==
              previous.Roster.Selected?.Profile.DemonstrationId,"invalid search leaves prior state");
        Reject<ArgumentOutOfRangeException>(()=>new OfflineRosterPresenter((OfflineRosterScenario)99),
              "enum scenario fails closed");
        Reject<ArgumentOutOfRangeException>(()=>new OfflineWorkspacePresenter((OfflineRosterScenario)99),
              "workspace enum scenario fails closed");
        Check(typeof(OfflineRosterPresenter).GetConstructors().All(c=>c.GetParameters().Length==0),
              "public roster API cannot inject fixtures");
        Check(typeof(OfflineWorkspacePresenter).GetConstructors().All(c=>c.GetParameters().Length==0),
              "public workspace API cannot inject fixtures");
        Check(all.Roster.VisiblePlayers is IList<OfflineExpandedPlayer> &&
              all.ClubPlayers is IList<OfflineExpandedPlayer>, "immutable interfaces");
        Reject<NotSupportedException>(()=>((IList<OfflineExpandedPlayer>)all.Roster.VisiblePlayers).Clear(),
              "visible array readonly");
        Reject<NotSupportedException>(()=>((IList<OfflineExpandedPlayer>)all.ClubPlayers).Clear(),
              "club member array readonly");

        Check(roster.Search("Alex").VisiblePlayers.Count==3, "roster search isolated");
        Check(roster.FilterByClubId("CLUB-001").VisiblePlayers.Count==2, "roster exact club-ID filter");
        Check(roster.Select("DEMO-006").Selected is null, "roster excludes misleading name");
        Console.WriteLine($"F29 PASS: {passed} deterministic collision, shared-club, navigation and recovery checks");
    }
}
