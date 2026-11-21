using System.Collections.Generic;
using System.Reflection;
using FMRTE2008.Application;
using FMRTE2008.Domain;

static class Tests
{
    private static int count;
    private static void Check(bool ok,string why)
    {
        if(!ok)throw new Exception("F27 failed: "+why);
        count++;
    }
    private static void Reject<T>(Action f,string why) where T:Exception
    {
        try{f();}catch(T){count++;return;}
        throw new Exception("F27 rejected case accepted: "+why);
    }
    private static void Main()
    {
        var roster=new OfflineExpandedRoster().Snapshot();
        Check(roster.Count==3,"three invented players");
        Check(roster.Select(p=>p.Profile.DemonstrationId).Distinct().Count()==3,"unique demo ids");
        Check(roster.Select(p=>p.Club.DemonstrationId).Distinct().Count()==3,"unique invented clubs");
        Check(roster.All(p=>p.IsSynthetic&&p.Profile.IsSynthetic),"all synthetic");
        Check(roster.All(p=>p.Club.Name==p.Profile.Contract.Club),"club matches contract");
        Check(roster.All(p=>p.CategorizedAttributes.Count==p.Profile.Attributes.Count),"classification complete");
        Check(roster.All(p=>p.CategorizedAttributes.All(x=>Enum.IsDefined(x.Category))),"valid categories");
        Check(roster.Any(p=>p.AttributesFor(OfflineAttributeCategory.Technical).Count>0),"technical group");
        Check(roster.Any(p=>p.AttributesFor(OfflineAttributeCategory.Mental).Count>0),"mental group");
        Check(roster.Any(p=>p.AttributesFor(OfflineAttributeCategory.Physical).Count>0),"physical group");
        Check(roster.All(p=>p.AttributesFor(OfflineAttributeCategory.Technical)
            .Concat(p.AttributesFor(OfflineAttributeCategory.Mental))
            .Concat(p.AttributesFor(OfflineAttributeCategory.Physical)).Count()==p.Profile.Attributes.Count),
            "partition without loss");
        Check(roster is IList<OfflineExpandedPlayer>,"exposed readonly list interface");
        Reject<NotSupportedException>(()=>((IList<OfflineExpandedPlayer>)roster).Clear(),"roster readonly");
        var attributes=roster[0].CategorizedAttributes;
        Reject<NotSupportedException>(()=>((IList<OfflineCategorizedAttribute>)attributes).Clear(),"category readonly");
        var grouped=roster[0].AttributesFor(OfflineAttributeCategory.Technical);
        Reject<NotSupportedException>(()=>((IList<OfflineAttribute>)grouped).Clear(),"group readonly");
        Reject<ArgumentException>(()=>new OfflineClub("REAL-001","X","Y","Z"),"real club ID");
        Reject<ArgumentException>(()=>new OfflineClub("CLUB-","X","Y","Z"),"missing club suffix");
        Reject<ArgumentException>(()=>new OfflineClub("CLUB-12A","X","Y","Z"),"non-ascii club ID");
        Reject<ArgumentException>(()=>new OfflineClub("CLUB-１２３","X","Y","Z"),"Unicode club ID");
        Reject<ArgumentException>(()=>new OfflineClub("CLUB-1234","X","Y","Z"),"overlong club ID");
        Reject<ArgumentOutOfRangeException>(()=>new OfflineCategorizedAttribute(
            new OfflineAttribute("X",10),(OfflineAttributeCategory)100),"category out of range");
        Reject<ArgumentException>(()=>new OfflineExpandedPlayer(roster[0].Profile,roster[1].Club,
            roster[0].CategorizedAttributes),"mismatched contract and club");
        Reject<ArgumentException>(()=>new OfflineExpandedPlayer(roster[0].Profile,roster[0].Club,
            roster[0].CategorizedAttributes.Skip(1)),"missing category entry");
        Reject<ArgumentException>(()=>new OfflineExpandedPlayer(roster[0].Profile,roster[0].Club,
            roster[0].CategorizedAttributes.Append(roster[0].CategorizedAttributes[0])),"duplicate category entry");

        Check(typeof(OfflineExpandedRoster).GetConstructors().All(c=>c.GetParameters().Length==0),
            "no externally supplied roster");
        Check(typeof(OfflineRosterPresenter).GetConstructors().All(c=>c.GetParameters().Length==0),
            "no externally supplied presenter source");
        var presenter=new OfflineRosterPresenter();
        Check(presenter.Current().Selected is null,"starts without selection");
        var current=presenter.Search("");
        Check(current.VisiblePlayers.Count==3&&current.SelectedIndex==0,"initial selection");
        Check(current.Selected?.Profile.DemonstrationId=="DEMO-001","first player selected");
        Check(!current.HasPrevious&&current.HasNext,"navigation boundary first");
        current=presenter.Previous();
        Check(current.SelectedIndex==0,"previous clamps first");
        current=presenter.Next();
        Check(current.Selected?.Profile.DemonstrationId=="DEMO-002","navigate next");
        current=presenter.Next();
        Check(current.Selected?.Profile.DemonstrationId=="DEMO-003","navigate last");
        Check(!current.HasNext&&current.HasPrevious,"last boundary");
        Check(presenter.Next().SelectedIndex==2,"next clamps last");
        Check(presenter.Previous().Selected?.Profile.DemonstrationId=="DEMO-002","navigate previous");
        current=presenter.Search("Clube Exemplo B");
        Check(current.VisiblePlayers.Count==1&&current.Selected?.Profile.DemonstrationId=="DEMO-002","filter by club");
        Check(!current.HasNext&&!current.HasPrevious,"single result boundary");
        Check(presenter.Select("DEMO-001").Selected is null,"reject outside filter");
        Check(presenter.Next().Selected is null,"no advancement from cleared selection");
        Check(presenter.Select("DEMO-002").SelectedIndex==0,"restore valid selection");
        current=presenter.Search("no results");
        Check(current.VisiblePlayers.Count==0&&current.Selected is null,"empty result");
        Check(!current.HasPrevious&&!current.HasNext,"empty navigation disabled");
        Check(presenter.Select("DEMO-002").Selected is null,"stale selection excluded");
        Check(presenter.Next().Selected is null&&presenter.Previous().Selected is null,"empty navigation inert");
        Check(presenter.Search("atacante").Selected?.Profile.DemonstrationId=="DEMO-003","case insensitive position");
        Check(presenter.Search("  DEMO-001  ").Selected?.Profile.DemonstrationId=="DEMO-001","trim and ID");
        Check(presenter.Search("").VisiblePlayers.Count==3,"reset filter");
        Reject<ArgumentOutOfRangeException>(()=>presenter.Search(new string('x',121)),"query length");
        Check(presenter.Current().VisiblePlayers.Count==3,"invalid search does not destroy state");
        Console.WriteLine($"F27 PASS: {count} deterministic offline checks; no process access");
    }
}
