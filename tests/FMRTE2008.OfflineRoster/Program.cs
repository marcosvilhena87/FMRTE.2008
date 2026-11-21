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
        // F27-R01/R02: independently enforce name/category semantics, not just counts.
        Check(OfflineAttributeCategoryCatalog.Resolve("Passe")==OfflineAttributeCategory.Technical,
            "Passe is always technical");
        Check(OfflineAttributeCategoryCatalog.Resolve("Visão")==OfflineAttributeCategory.Mental,
            "Visão is always mental");
        Check(OfflineAttributeCategoryCatalog.Resolve("Força")==OfflineAttributeCategory.Physical,
            "Força is always physical");
        Check(roster.All(p=>p.CategorizedAttributes.All(a=>
            a.Category==OfflineAttributeCategoryCatalog.Resolve(a.Attribute.Name))),
            "all actual roster entries agree with closed name catalog");
        var passes=roster.SelectMany(p=>p.CategorizedAttributes)
            .Where(a=>a.Attribute.Name=="Passe").ToArray();
        Check(passes.Length==3&&passes.All(a=>a.Category==OfflineAttributeCategory.Technical),
            "same attribute category remains stable across players");

        var sourceAttributes=roster[0].Profile.Attributes;
        var reordered=sourceAttributes.Reverse().ToArray();
        var classifiedReordered=OfflineAttributeCategoryCatalog.Categorize(reordered);
        Check(classifiedReordered.Select(x=>x.Attribute.Name).SequenceEqual(reordered.Select(x=>x.Name)),
            "reordering preserves attribute order");
        Check(classifiedReordered.All(a=>
            a.Category==OfflineAttributeCategoryCatalog.Resolve(a.Attribute.Name)),
            "reordering never changes semantic category");
        var validReordered=new OfflineExpandedPlayer(roster[0].Profile,roster[0].Club,classifiedReordered);
        Check(validReordered.AttributesFor(OfflineAttributeCategory.Mental).Single().Name=="Visão",
            "mental category preserved after reordering");

        var swapped=roster[0].CategorizedAttributes.ToArray();
        swapped[0]=new OfflineCategorizedAttribute(swapped[0].Attribute,OfflineAttributeCategory.Mental);
        Reject<ArgumentException>(()=>new OfflineExpandedPlayer(
            roster[0].Profile,roster[0].Club,swapped),
            "domain rejects wrong category even if names/counts match");
        var changed=new [] { new OfflineAttribute("Atributo Desconhecido",10) };
        Reject<ArgumentException>(()=>OfflineAttributeCategoryCatalog.Categorize(changed),
            "unrecognized name fail closed");
        Reject<ArgumentException>(()=>OfflineAttributeCategoryCatalog.Resolve("passe"),
            "unexpected casing not silently mapped");
        Reject<ArgumentException>(()=>OfflineAttributeCategoryCatalog.Resolve("Passe "),
            "whitespace-altered names rejected");
        Reject<ArgumentException>(()=>OfflineAttributeCategoryCatalog.Categorize(
            new[]{new OfflineAttribute("Passe",10),new OfflineAttribute("passe",11)}),
            "case-insensitive duplicates blocked");
        Reject<ArgumentNullException>(()=>OfflineAttributeCategoryCatalog.Categorize(null!),
            "null list rejected");
        Check(OfflineAttributeCategoryCatalog.Categorize(sourceAttributes)
            is IList<OfflineCategorizedAttribute>,"returned collection supports readonly contract");
        Reject<NotSupportedException>(()=>((IList<OfflineCategorizedAttribute>)
            OfflineAttributeCategoryCatalog.Categorize(sourceAttributes)).Clear(),
            "classified category list cannot be mutated");
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
