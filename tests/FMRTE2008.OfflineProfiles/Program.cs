using System.Collections;
using System.Reflection;
using FMRTE2008.Application;
using FMRTE2008.Domain;

static class OfflineChecks
{
    private static int _count;
    private static void Assert(bool pass, string message)
    {
        if (!pass) throw new InvalidOperationException("F26-R failed: " + message);
        _count++;
    }

    private static void Reject<T>(Action action, string reason) where T: Exception
    {
        try { action(); }
        catch (T) { _count++; return; }
        throw new InvalidOperationException("F26-R negative accepted: " + reason);
    }

    private static OfflineContract Contract() =>
        new("Clube Teste",new DateOnly(2008,1,1),new DateOnly(2010,1,1),100m);

    private static void Main()
    {
        // R01: closed construction: no arbitrary source, delegates, or Snapshot().
        var constructors=typeof(OfflineProfileQueryService).GetConstructors(BindingFlags.Public|BindingFlags.Instance);
        Assert(constructors.Length==1 && constructors[0].GetParameters().Length==0,
            "only parameterless query service construction");
        Assert(!typeof(OfflineProfileQueryService).GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic)
            .Any(c => c.GetParameters().Any(p => p.ParameterType.Name.Contains("Source"))),
            "no internal provider constructor");
        Assert(!typeof(OfflineProfileQueryService).GetMethods(BindingFlags.Public|BindingFlags.Instance)
            .Any(m => m.GetParameters().Any(p => p.ParameterType.Name.Contains("Source"))),
            "no public provider hook");
        var service=new OfflineProfileQueryService();
        var initial=service.Search("",pageSize:2);
        Assert(initial.TotalMatches==3,"three fixed, invented players");
        Assert(initial.Items.All(p=>p.DemonstrationId.StartsWith("DEMO-",StringComparison.Ordinal)),"demo IDs");
        Assert(initial.PageCount==2 && initial.Items.Count==2,"page bounds");
        Assert(service.Search("",page:2,pageSize:2).Items.Count==1,"second page");
        Assert(service.Search("",page:500,pageSize:2).PageNumber==2,"page clamping");
        Assert(service.Search("Alex").TotalMatches==1,"name query");
        Assert(service.Search("DEMO-002").Items[0].DisplayName=="Bruno Fictício","identifier query");
        Assert(service.Search("Atacante").TotalMatches==1,"position query");
        Assert(service.Search("SEM RESULTADO").TotalMatches==0,"no matches");
        Assert(service.Search("  alex  ").TotalMatches==1,"trim and case insensitive");
        Assert(service.Search("",pageSize:10).TotalMatches==3,"snapshot unaffected by queries");
        Reject<ArgumentOutOfRangeException>(()=>service.Search("",pageSize:0),"zero page size");
        Reject<ArgumentOutOfRangeException>(()=>service.Search("",pageSize:51),"excessive page size");
        Reject<ArgumentOutOfRangeException>(()=>service.Search("",page:0),"zero page number");
        Reject<ArgumentOutOfRangeException>(()=>service.Search(new string('x',121)),"oversized query");

        // R02: attempts to mutate original input and public collections.
        var input=new List<OfflineAttribute>{new("Passe",12)};
        var player=new OfflinePlayerProfile("DEMO-999","Pessoa Teste","Defesa",input,Contract());
        input[0]=new OfflineAttribute("Passe",1);
        input.Add(new OfflineAttribute("Técnica",20));
        Assert(player.Attributes.Count==1 && player.Attributes[0].Value==12,"defensive input copy");
        Assert(player.Attributes is IList<OfflineAttribute>,"read-only IList interface");
        Reject<NotSupportedException>(()=>((IList<OfflineAttribute>)player.Attributes)[0]=new OfflineAttribute("Passe",10),"attribute overwrite");
        Reject<NotSupportedException>(()=>((IList<OfflineAttribute>)player.Attributes).Add(new OfflineAttribute("Técnica",9)),"attribute append");
        var fixture=new DemonstrationProfileSource().Snapshot();
        Assert(fixture.Count==3 && fixture is IList<OfflinePlayerProfile>,"fixture snapshot read-only collection");
        Reject<NotSupportedException>(()=>((IList<OfflinePlayerProfile>)fixture)[0]=player,"fixture mutation");
        var result=service.Search("");
        Assert(result.Items is IList<OfflinePlayerProfile>,"page exposed as read-only collection");
        Reject<NotSupportedException>(()=>((IList<OfflinePlayerProfile>)result.Items).Clear(),"result mutation");
        Assert(service.Search("").Items[0].DemonstrationId=="DEMO-001","post-mutation snapshot stable");
        Assert(player.IsSynthetic && player.Contract.WeeklyWage==100m,"synthetic contract stable");

        Reject<ArgumentOutOfRangeException>(()=>new OfflineAttribute("Passe",0),"attribute below 1");
        Reject<ArgumentOutOfRangeException>(()=>new OfflineAttribute("Passe",21),"attribute above 20");
        Reject<ArgumentException>(()=>new OfflineAttribute("",10),"empty attribute");
        Reject<ArgumentException>(()=>new OfflineContract("X",new DateOnly(2010,1,1),new DateOnly(2009,1,1),0),"inverted contract");
        Reject<ArgumentOutOfRangeException>(()=>new OfflineContract("X",new DateOnly(2010,1,1),new DateOnly(2011,1,1),-1),"negative wages");
        Reject<ArgumentException>(()=>new OfflinePlayerProfile("REAL-999","Pessoa","Defesa",new[]{new OfflineAttribute("Passe",10)},Contract()),"real ID");
        Reject<ArgumentException>(()=>new OfflinePlayerProfile("DEMO-","Pessoa","Defesa",new[]{new OfflineAttribute("Passe",10)},Contract()),"empty demo suffix");
        Reject<ArgumentException>(()=>new OfflinePlayerProfile("DEMO-   ","Pessoa","Defesa",new[]{new OfflineAttribute("Passe",10)},Contract()),"spaces in demo ID");
        Reject<ArgumentException>(()=>new OfflinePlayerProfile("DEMO-12A","Pessoa","Defesa",new[]{new OfflineAttribute("Passe",10)},Contract()),"non numeric ID");
        Reject<ArgumentException>(()=>new OfflinePlayerProfile("DEMO-１２３","Pessoa","Defesa",new[]{new OfflineAttribute("Passe",10)},Contract()),"unicode digits");
        Reject<ArgumentException>(()=>new OfflinePlayerProfile("DEMO-1234","Pessoa","Defesa",new[]{new OfflineAttribute("Passe",10)},Contract()),"overlong ID");
        Reject<ArgumentException>(()=>new OfflinePlayerProfile("DEMO-200","Pessoa","Defesa",new[]{new OfflineAttribute("Passe",10),new OfflineAttribute("passe",12)},Contract()),"duplicate attribute");

        // R03: presentation state, selection clearing, and cross-search isolation.
        var presenter=new OfflineProfilesPresenter();
        Assert(presenter.Select("DEMO-001") is null,"no selection before search");
        var three=presenter.Search("");
        Assert(three.TotalMatches==3,"presenter initial display");
        Assert(presenter.Select("DEMO-001")?.DisplayName=="Alex Exemplo","valid selection");
        Assert(presenter.Select(null) is null,"clear selection");
        Assert(presenter.Select("DEMO-999") is null,"unknown selection");
        Assert(presenter.Search("nada").Items.Count==0,"empty result");
        Assert(presenter.Select("DEMO-001") is null,"stale selection blocked");
        Assert(presenter.Search("Bruno").TotalMatches==1,"selection after new query");
        Assert(presenter.Select("DEMO-002")?.Contract.Club=="Clube Exemplo B","contract corresponds to selection");
        Assert(presenter.Select("DEMO-001") is null,"other-page selection blocked");
        Assert(presenter.Search("").TotalMatches==3,"reset search");

        Console.WriteLine($"F26-R PASS: {_count} domain, search, immutability, provenance and presenter assertions; no process access");
    }
}
