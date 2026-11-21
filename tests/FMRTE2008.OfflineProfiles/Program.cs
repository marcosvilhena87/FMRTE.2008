using FMRTE2008.Application;
using FMRTE2008.Domain;

static class OfflineChecks
{
    private static int _count;
    static void Assert(bool ok, string reason)
    {
        if (!ok) throw new InvalidOperationException("F26 check failed: " + reason);
        _count++;
    }
    static void Reject<T>(Action operation, string name) where T : Exception
    {
        try { operation(); }
        catch (T) { _count++; return; }
        throw new InvalidOperationException("F26 negative case accepted: " + name);
    }
    static void Main()
    {
        var source = new DemonstrationProfileSource();
        Assert(!source.IsLiveProcessSource,"source is not live");
        var service = new OfflineProfileQueryService(source);
        var initial = service.Search("",pageSize:2);
        Assert(initial.TotalMatches==3,"snapshot count");
        Assert(initial.PageCount==2,"pagination");
        Assert(initial.Items.Count==2,"bounded page");
        Assert(service.Search("",page:2,pageSize:2).Items.Count==1,"second page");
        Assert(service.Search("",page:500,pageSize:2).PageNumber==2,"clamped page");
        Assert(service.Search("Alex").TotalMatches==1,"name search");
        Assert(service.Search("DEMO-002").Items[0].DisplayName=="Bruno Fictício","ID search");
        Assert(service.Search("Atacante").TotalMatches==1,"position search");
        Assert(service.Search("sem resultado").TotalMatches==0,"empty search");
        Assert(initial.Items[0].IsSynthetic,"synthetic invariant");
        Assert(initial.Items[0].Attributes[0].Value==15,"attribute carried");
        Assert(initial.Items[0].Contract.WeeklyWage==2500m,"contract carried");
        Reject<ArgumentOutOfRangeException>(()=>new OfflineAttribute("Passe",0),"low attribute");
        Reject<ArgumentOutOfRangeException>(()=>new OfflineAttribute("Passe",21),"high attribute");
        Reject<ArgumentException>(()=>new OfflineAttribute("",10),"blank attribute");
        Reject<ArgumentException>(()=>new OfflineContract("X",new DateOnly(2010,1,1),new DateOnly(2009,1,1),0),"inverted dates");
        Reject<ArgumentOutOfRangeException>(()=>new OfflineContract("X",new DateOnly(2010,1,1),new DateOnly(2011,1,1),-1),"negative wage");
        var c = new OfflineContract("Clube Teste",new DateOnly(2008,1,1),new DateOnly(2010,1,1),100m);
        Reject<ArgumentException>(()=>new OfflinePlayerProfile("REAL-99","Pessoa","Defesa",new[]{new OfflineAttribute("Passe",10)},c),"real ID");
        Reject<ArgumentException>(()=>new OfflinePlayerProfile("DEMO-10","Pessoa","Defesa",new[]{new OfflineAttribute("Passe",10),new OfflineAttribute("passe",12)},c),"duplicate attribute");
        Reject<ArgumentOutOfRangeException>(()=>service.Search("",pageSize:51),"page budget");
        Reject<ArgumentOutOfRangeException>(()=>service.Search("",page:0),"page index");
        Reject<ArgumentOutOfRangeException>(()=>service.Search(new string('x',121)),"query budget");
        Reject<UnauthorizedAccessException>(()=>new OfflineProfileQueryService(new LiveSource()),"reject live source");
        Reject<ArgumentException>(()=>new OfflineProfileQueryService(new DuplicateSource()),"reject duplicated identities");
        var snapshot=source.Snapshot();
        Assert(snapshot is not OfflinePlayerProfile[],"snapshot array not exposed");
        Assert(initial.Items is not OfflinePlayerProfile[],"page array not exposed");
        Console.WriteLine($"F26 PASS: {_count} offline and negative assertions; no process access");
    }
    private sealed class LiveSource : IOfflinePlayerProfileSource
    {
        public bool IsLiveProcessSource => true;
        public IReadOnlyList<OfflinePlayerProfile> Snapshot() => throw new InvalidOperationException("must not be reached");
    }
    private sealed class DuplicateSource : IOfflinePlayerProfileSource
    {
        public bool IsLiveProcessSource => false;
        public IReadOnlyList<OfflinePlayerProfile> Snapshot()
        {
            var p=new DemonstrationProfileSource().Snapshot()[0];
            return new[] {p,p};
        }
    }
}
