using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FMRTE2008.MemorySimulation;

/// <summary>F23.1 is documentary only: no game access, no historic attribution or approval.</summary>
internal static class F23B03ProvenanceGapTests
{
    private static readonly string[] Missing = ["executor","observedAt","environment",
        "collectionMethod","externalCorroboration"];

    internal static void Run()
    {
        string root = FindRoot();
        string directory = Path.Combine(root,"tests","FMRTE2008.MemorySimulation");
        string path=Path.Combine(directory,"F23B03ProvenanceGaps.json");
        byte[] bytes=File.ReadAllBytes(path);
        byte[] f21bytes=File.ReadAllBytes(Path.Combine(directory,"F21B03Evidence.json"));
        var manifest=JsonNode.Parse(bytes) ?? throw new InvalidDataException("F23 null manifest");
        var f21=JsonNode.Parse(f21bytes) ?? throw new InvalidDataException("F23 null F21");
        Validate(manifest,f21);
        var cases=new List<object>();
        string? outdir=null;
        if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true")
        {
            string temp=Environment.GetEnvironmentVariable("RUNNER_TEMP")
                ?? throw new InvalidDataException("F23 missing runner temp");
            outdir=Path.Combine(temp,"f20-evidence","f23-schema-cases");
            Directory.CreateDirectory(outdir);
        }
        void Reject(string caseId,Action<JsonNode> modify)
        {
            var copy=manifest.DeepClone();
            modify(copy);
            if(outdir is not null)
                File.WriteAllText(Path.Combine(outdir,caseId+".json"),copy.ToJsonString());
            try { Validate(copy,f21); }
            catch(InvalidDataException ex)
            {
                cases.Add(new {caseId,expectedOutcome="InvalidDataException",
                    actualOutcome=ex.GetType().Name,result="rejected",reason=ex.Message});
                Console.WriteLine("OK F23 NEG "+caseId);
                return;
            }
            throw new Exception("F23 mutation accepted: "+caseId);
        }
        Reject("invent-executor",x=>x["observations"]![0]!["executor"]="GitHub Actions");
        Reject("invent-observed-at",x=>x["observations"]![1]!["observedAt"]="2026-11-18T12:00:00Z");
        Reject("invent-environment",x=>x["observations"]![2]!["environment"]="Windows");
        Reject("invent-collection-method",x=>x["observations"]![3]!["collectionMethod"]="CI");
        Reject("invent-corroboration",x=>x["observations"]![4]!["externalCorroboration"]="verified");
        Reject("promote-provenance",x=>x["observations"]![5]!["provenanceStatus"]="VERIFIED");
        Reject("claim-source-audited",x=>x["observations"]![6]!["sourceEvidenceStatus"]="independently-audited");
        Reject("invent-source-blob",x=>x["observations"]![0]!["sourceBlobSha"]=new string('f',40));
        Reject("invent-source-path",x=>x["observations"]![1]!["sourcePath"]="invented.md");
        Reject("invent-result",x=>x["observations"]![2]!["reportedResult"]="independently confirmed");
        Reject("duplicate-observation",x=>x["observations"]![1]!["observationId"]="R01");
        Reject("missing-observation",x=>x["observations"]!.AsArray().RemoveAt(6));
        Reject("swap-observations",x=>
        {
            var a=x["observations"]!.AsArray();
            var one=a[0]!.DeepClone();a[0]=a[1]!.DeepClone();a[1]=one;
        });
        Reject("new-root-field",x=>x["autoApprove"]=true);
        Reject("new-observation-field",x=>x["observations"]![0]!["ciExecutor"]="runner");
        Reject("approve-map",x=>x["decision"]!["approved"]=true);
        Reject("operational-map",x=>x["decision"]!["operational"]=true);
        Reject("open-A12",x=>x["decision"]!["gateA12"]="approved");
        Reject("close-B03",x=>x["decision"]!["b03"]="VERIFIED");
        Reject("fake-human-approval",x=>x["decision"]!["humanApproval"]="CI");
        Reject("wrong-F21-source",x=>x["source"]!["commit"]=new string('0',40));
        Reject("wrong-inventory-reference",x=>x["source"]!["f21InventoryPath"]="other.json");
        if(cases.Count!=22) throw new InvalidDataException("F23 negative count mismatch");
        if(outdir is not null)
        {
            string evidence=Directory.GetParent(outdir)!.FullName;
            File.Copy(path,Path.Combine(evidence,"F23B03ProvenanceGaps.json"),true);
            File.WriteAllText(Path.Combine(evidence,"F23B03ProvenanceValidation.json"),
                JsonSerializer.Serialize(new {schemaVersion=1,kind="f23-b03-provenance-offline-validation",
                  manifestSha256=Sha(bytes),f21Sha256=Sha(f21bytes),observationCount=7,
                  negativeMutationsRejected=cases.Count,negativeCases=cases,
                  approved=false,operational=false,gateA12="GATE-BLOQUEADO",
                  b03="PENDING-DECLARED",historicalObservationAttested=false},
                  new JsonSerializerOptions{WriteIndented=true}));
        }
        Console.WriteLine($"OK F23 B03: R01-R07=7 PENDING-DECLARED, negatives={cases.Count}, sha256={Sha(bytes)}; no historic attestation or map approval");
    }

    private static void Validate(JsonNode doc,JsonNode f21)
    {
        try
        {
            Keys(doc,"schemaVersion","kind","source","decision","observations");
            if(doc["schemaVersion"]!.GetValue<int>()!=1 ||
                S(doc,"kind")!="f23-b03-provenance-gaps-offline")
                throw new InvalidDataException("F23 incorrect schema identity");
            var source=doc["source"]!;
            Keys(source,"repository","commit","f21InventoryPath");
            if(S(source,"repository")!=S(f21,"sourceRepo") ||
                S(source,"commit")!=S(f21,"sourceCommit") ||
                S(source,"f21InventoryPath")!="tests/FMRTE2008.MemorySimulation/F21B03Evidence.json")
                throw new InvalidDataException("F23 source mismatch");
            var decision=doc["decision"]!;
            Keys(decision,"approved","operational","gateA12","b03","humanApproval");
            if(decision["approved"]!.GetValue<bool>() ||
                decision["operational"]!.GetValue<bool>() ||
                S(decision,"gateA12")!="GATE-BLOQUEADO" ||
                S(decision,"b03")!="PENDING-DECLARED" ||
                decision["humanApproval"] is not null)
                throw new InvalidDataException("F23 forbidden approval");
            var observations=doc["observations"]!.AsArray();
            var originals=f21["observations"]!.AsArray();
            if(observations.Count!=7 || originals.Count!=7)
                throw new InvalidDataException("F23 incomplete inventory");
            for(int i=0;i<7;i++)
            {
                var item=observations[i] ?? throw new InvalidDataException("F23 null observation");
                var original=originals[i] ?? throw new InvalidDataException("F23 missing F21 observation");
                Keys(item,"observationId","sourcePath","sourceBlobSha","reportedResult",
                     "sourceEvidenceStatus","provenanceStatus","executor","observedAt",
                     "environment","collectionMethod","externalCorroboration");
                if(S(item,"observationId")!="R0"+(i+1) ||
                    S(item,"observationId")!=S(original,"observationId") ||
                    S(item,"sourcePath")!=S(original,"sourcePath") ||
                    S(item,"sourceBlobSha")!=S(original,"sourceBlobSha") ||
                    S(item,"reportedResult")!=S(original,"reportedResult") ||
                    S(item,"sourceEvidenceStatus")!="user-reported-not-independently-audited" ||
                    S(item,"sourceEvidenceStatus")!=S(original,"sourceEvidenceStatus") ||
                    S(item,"provenanceStatus")!="PENDING-DECLARED" ||
                    S(item,"provenanceStatus")!=S(original,"provenanceStatus") ||
                    Missing.Any(k=>item[k] is not null || original[k] is not null))
                    throw new InvalidDataException("F23 incorrect observation origin/attribution");
            }
        }
        catch(Exception ex) when(ex is InvalidOperationException or ArgumentException or
            KeyNotFoundException or JsonException or NullReferenceException)
        { throw new InvalidDataException("F23 closed schema rejection",ex); }
    }
    private static string Sha(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string S(JsonNode n,string key)=>
        n[key]?.GetValue<string>() ?? throw new InvalidDataException("F23 missing "+key);
    private static void Keys(JsonNode n,params string[] keys)
    {
        var obj=n.AsObject();
        if(obj.Count!=keys.Length || keys.Any(k=>!obj.ContainsKey(k)) ||
            obj.Any(p=>!keys.Contains(p.Key,StringComparer.Ordinal)))
            throw new InvalidDataException("F23 closed schema keys");
    }
    private static string FindRoot()
    {
        DirectoryInfo? d=new(AppContext.BaseDirectory);
        for(int i=0;i<12 && d is not null;i++,d=d.Parent)
            if(File.Exists(Path.Combine(d.FullName,"tests","FMRTE2008.MemorySimulation","F21B03Evidence.json")))
                return d.FullName;
        throw new DirectoryNotFoundException("F23 checkout not found");
    }
}
