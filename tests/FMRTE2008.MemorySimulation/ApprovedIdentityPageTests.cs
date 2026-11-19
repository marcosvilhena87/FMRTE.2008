using System.Buffers.Binary;
using System.Text;
using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

internal static class ApprovedIdentityPageTests
{
    public static async Task RunAsync()
    {
        var id = new ExecutableIdentity(SupportedBuildCatalog.Known802Sha256,"8.0.2",52395,"x86");
        var fake = new TestOpener();
        await Reject<UnauthorizedAccessException>(() =>
            VerifiedPlayerReadSession.OpenAsync(123,id,new NoApprovedPlayerMaps(),fake));
        Check(fake.Opens == 0, "O processo nao deveria ser aberto");
        fake.Memory = MakeMemory();
        using (var session = await VerifiedPlayerReadSession.OpenAsync(123,id,
            new FakeApproval(id.Sha256),fake))
        {
            var first = await session.ReadIdentityPageAsync(1,1);
            Check(first.Count == 1 && first[0].Id == 20 &&
                first[0].FirstName == "Ivica" && first[0].LastName == "Dragutinovic" &&
                first[0].CommonName == null, "Pagina 1");
            var second = await session.ReadIdentityPageAsync(2,1);
            Check(second.Count == 1 && second[0].Id == 107 &&
                second[0].LastName == "Östlund" && second[0].CommonName == "Alex", "Pagina 2");
            Check((await session.ReadIdentityPageAsync(3,1)).Count == 0, "Pagina vazia");
            await Reject<ArgumentOutOfRangeException>(() => session.ReadIdentityPageAsync(0));
            await Reject<ArgumentOutOfRangeException>(() => session.ReadIdentityPageAsync(1,101));
            Check(fake.Opens == 1, "Uma unica abertura simulada");
        }
        Check(fake.Disposed, "Sessao nao foi liberada");
        Console.WriteLine("OK: 8 verificacoes da sessao de paginação aprovada apenas em fixture");
    }

    static SimulatedProcessMemory MakeMemory()
    {
        var m = new SimulatedProcessMemory();
        var h = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(h,0x2000);
        BinaryPrimitives.WriteInt32LittleEndian(h.AsSpan(4),2);
        m.AddRegion(0x1000,h);
        var p = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(p,0x3000);
        BinaryPrimitives.WriteUInt32LittleEndian(p.AsSpan(4),0x3200);
        m.AddRegion(0x2000,p);
        Player(m,0x3000,20,"Ivica","Dragutinovic",null,0x4000);
        Player(m,0x3200,107,"Alexander","Östlund","Alex",0x5000);
        return m;
    }

    static void Player(SimulatedProcessMemory m,uint addr,int id,
        string first,string last,string? common,uint root)
    {
        var b = new byte[292];
        BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(256),id);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(280),root);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(284),root+0x100);
        if(common is not null) BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(288),root+0x200);
        m.AddRegion(addr,b);
        Name(m,root,first);
        Name(m,root+0x100,last);
        if(common is not null) Name(m,root+0x200,common);
    }

    static void Name(SimulatedProcessMemory m,uint addr,string name)
    {
        var b = new byte[40];
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(36),addr+0x40);
        m.AddRegion(addr,b);
        var inner = new byte[12];
        BinaryPrimitives.WriteUInt32LittleEndian(inner.AsSpan(8),addr+0x60);
        m.AddRegion(addr+0x40,inner);
        m.AddRegion(addr+0x60,Encoding.Unicode.GetBytes(name+"\0"));
    }
    static void Check(bool ok,string error)
    { if(!ok) throw new Exception(error); }
    static async Task Reject<T>(Func<Task> action) where T:Exception
    { try { await action(); } catch(T) { return; } throw new Exception(typeof(T).Name); }

    sealed class FakeApproval(string hash):IApprovedPlayerMapCatalog
    {
        public Task<ApprovedPlayerMap?> FindApprovedAsync(ExecutableIdentity identity,CancellationToken ct) =>
            Task.FromResult<ApprovedPlayerMap?>(new(hash,"8.0.2v1",0x1000,256,"fixture-only"));
    }
    sealed class TestOpener:IVerifiedProcessOpener
    {
        public int Opens {get;private set;}
        public bool Disposed {get;private set;}
        public SimulatedProcessMemory Memory {get;set;} = null!;
        public Task<IProcessReadSession> OpenVerifiedAsync(int pid,ExecutableIdentity id,CancellationToken ct)
        { Opens++; return Task.FromResult<IProcessReadSession>(new Session(Memory,()=>Disposed=true)); }
    }
    sealed class Session(SimulatedProcessMemory memory,Action onDispose):IProcessReadSession
    {
        public Task<byte[]> ReadAsync(nuint addr,int length,CancellationToken ct) =>
            memory.ReadAsync(addr,length,ct);
        public void Dispose()=>onDispose();
    }
}
