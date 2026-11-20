using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FMRTE2008.MemorySimulation;

/// <summary>F23.3 records NO decision. A green CI run is never a human approval.</summary>
internal static class F23HumanDecisionNotMadeTests
{
    internal static void Run()
    {
        var folder=FindFolder();
        byte[] bytes=File.ReadAllBytes(Path.Combine(folder,"F23HumanDecisionNotMade.json"));
        byte[] rbytes=File.ReadAllBytes(Path.Combine(folder,"F23ResidualRisks.json"));
        byte[] gbytes=File.ReadAllBytes(Path.Combine(folder,"F22ApprovalGates.json"));
        byte[] bbytes=File.ReadAllBytes(Path.Combine(folder,"F23B03ProvenanceGaps.json"));
        var doc=JsonNode.Parse(bytes)!;var risks=JsonNode.Parse(rbytes)!;
        var gates=JsonNode.Parse(gbytes)!;var b03=JsonNode.Parse(bbytes)!;
        Validate(doc,risks,gates,b03);
        string? outdir=null;
        if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true")
        {
            var runner=Environment.GetEnvironmentVariable("RUNNER_TEMP")
                ?? throw new InvalidDataException("F23.3 runner temp missing");
            outdir=Path.Combine(runner,"f20-evidence","f23-human-cases");
            Directory.CreateDirectory(outdir);
        }
        var negatives=new List<object>();
        void Reject(string name,Action<JsonNode> change)
        {
            var candidate=doc.DeepClone();change(candidate);
            if(outdir is not null)
                File.WriteAllText(Path.Combine(outdir,name+".json"),candidate.ToJsonString());
            try {Validate(candidate,risks,gates,b03);}
            catch(InvalidDataException e)
            {
                negatives.Add(new {caseId=name,expectedOutcome="InvalidDataException",
                    actualOutcome=e.GetType().Name,result="rejected",reason=e.Message});
                Console.WriteLine("OK F23.3 NEG "+name);
                return;
            }
            throw new Exception("F23.3 negative accepted "+name);
        }
        Reject("fabricated-reviewer",x=>x["humanReview"]!["reviewer"]="CI");
        Reject("fabricated-review-date",x=>x["humanReview"]!["reviewedAt"]="2026-11-20");
        Reject("fabricated-authority",x=>x["humanReview"]!["authorityReference"]="actions");
        Reject("decision-approved",x=>x["humanReview"]!["decision"]="APPROVED");
        Reject("fabricated-decision-reference",x=>x["humanReview"]!["decisionReference"]="PR24");
        Reject("unblock-A12",x=>x["gate"]!["status"]="APPROVED");
        Reject("swap-gate",x=>x["gate"]!["gateId"]="G4");
        Reject("swap-criterion",x=>x["gate"]!["criterion"]="A11");
        Reject("fabricate-human-signoff",x=>x["gate"]!["humanSignoff"]=true);
        Reject("close-B03",x=>x["evidence"]!["b03"]="VERIFIED");
        Reject("attest-historical",x=>x["evidence"]!["historicalProvenanceAttested"]=true);
        Reject("CI-as-human",x=>x["evidence"]!["ciIsHumanApproval"]=true);
        Reject("Ready-as-human",x=>x["evidence"]!["prReadyIsHumanApproval"]=true);
        Reject("risk-accepted",x=>x["riskDecisions"]![0]!["accepted"]=true);
        Reject("risk-acceptance-reference",x=>x["riskDecisions"]![1]!["acceptanceReference"]="CI");
        Reject("duplicate-risk",x=>x["riskDecisions"]![1]!["riskId"]="RA-01");
        Reject("remove-risk",x=>x["riskDecisions"]!.AsArray().RemoveAt(7));
        Reject("swap-risk-order",x=>{var a=x["riskDecisions"]!.AsArray();var z=a[0]!.DeepClone();a[0]=a[1]!.DeepClone();a[1]=z;});
        Reject("approve-map",x=>x["outcome"]!["approved"]=true);
        Reject("enable-operational",x=>x["outcome"]!["operational"]=true);
        Reject("approved-map-count",x=>x["outcome"]!["approvedPlayerMaps"]=1);
        Reject("wrong-risk-source",x=>x["source"]!["riskRegistryPath"]="fake.json");
        Reject("wrong-gate-source",x=>x["source"]!["gateMatrixPath"]="fake.json");
        Reject("wrong-B03-source",x=>x["source"]!["provenanceManifestPath"]="fake.json");
        Reject("wrong-mapping-commit",x=>x["source"]!["mappingCommit"]=new string('0',40));
        Reject("unexpected-root-key",x=>x["autoApprove"]=true);
        Reject("unexpected-review-key",x=>x["humanReview"]!["runnerIdentity"]="CI");
        Reject("unexpected-risk-key",x=>x["riskDecisions"]![0]!["ciApproved"]=true);
        if(negatives.Count!=28)throw new InvalidDataException("F23.3 negative count mismatch");
        int sourceNegatives=0;
        void RejectSource(string id,JsonNode original,Action<JsonNode> change,int which)
        {
            var clone=original.DeepClone();change(clone);
            try{
                if(which==0)Validate(doc,clone,gates,b03);
                else if(which==1)Validate(doc,risks,clone,b03);
                else Validate(doc,risks,gates,clone);
            }catch(InvalidDataException){
                sourceNegatives++;Console.WriteLine("OK F23.3 LINK NEG "+id);return;
            }
            throw new Exception("F23.3 source mutation accepted "+id);
        }
        RejectSource("risk-accepted-in-source",risks,x=>x["risks"]![0]!["riskAccepted"]=true,0);
        RejectSource("risk-identity-in-source",risks,x=>x["risks"]![0]!["riskId"]="RA-08",0);
        RejectSource("A12-approved-in-source",gates,x=>x["decision"]!["gateA12"]="approved",1);
        RejectSource("G5-criterion-in-source",gates,x=>x["gates"]![4]!["criteria"]![0]="A11",1);
        RejectSource("B03-verified-in-source",b03,x=>x["decision"]!["b03"]="VERIFIED",2);
        if(sourceNegatives!=5)throw new InvalidDataException("F23.3 source tests mismatch");
        if(outdir is not null)
        {
            string evidence=Directory.GetParent(outdir)!.FullName;
            File.WriteAllBytes(Path.Combine(evidence,"F23HumanDecisionNotMade.json"),bytes);
            File.WriteAllText(Path.Combine(evidence,"F23HumanDecisionValidation.json"),
                JsonSerializer.Serialize(new{schemaVersion=1,kind="f23-human-decision-not-made-validation",
                    decisionSha256=Sha(bytes),riskSha256=Sha(rbytes),
                    gateSha256=Sha(gbytes),b03Sha256=Sha(bbytes),
                    rejectedMutations=negatives.Count,negativeCases=negatives,
                    sourceMutationsRejected=sourceNegatives,decision="NOT_MADE",
                    reviewer=(string?)null,reviewedAt=(string?)null,approved=false,operational=false,
                    humanApproval=false,riskAccepted=false,gateA12="GATE-BLOQUEADO",b03="PENDING-DECLARED"
                },new JsonSerializerOptions{WriteIndented=true}));
        }
        Console.WriteLine($"OK F23.3: decision NOT_MADE, risks=8 unaccepted, negatives={negatives.Count}, sources={sourceNegatives}, sha256={Sha(bytes)}");
    }
    private static void Validate(JsonNode d,JsonNode risks,JsonNode gates,JsonNode b03)
    {
        try
        {
            Keys(d,"schemaVersion","kind","source","humanReview","gate","evidence","riskDecisions","outcome");
            if(d["schemaVersion"]!.GetValue<int>()!=1 ||
                S(d,"kind")!="f23-human-decision-not-made-offline")
                throw new InvalidDataException("F23.3 wrong identity");
            var source=d["source"]!;
            Keys(source,"riskRegistryPath","gateMatrixPath","provenanceManifestPath","mappingRepository","mappingCommit");
            if(S(source,"riskRegistryPath")!="tests/FMRTE2008.MemorySimulation/F23ResidualRisks.json"||
                S(source,"gateMatrixPath")!="tests/FMRTE2008.MemorySimulation/F22ApprovalGates.json"||
                S(source,"provenanceManifestPath")!="tests/FMRTE2008.MemorySimulation/F23B03ProvenanceGaps.json"||
                S(source,"mappingRepository")!=S(risks["source"]!,"mappingRepo")||
                S(source,"mappingCommit")!=S(risks["source"]!,"mappingCommit")||
                S(source,"mappingRepository")!=S(gates["mappingSource"]!,"repository")||
                S(source,"mappingCommit")!=S(gates["mappingSource"]!,"commit")||
                S(source,"mappingRepository")!=S(b03["source"]!,"repository")||
                S(source,"mappingCommit")!=S(b03["source"]!,"commit"))
                throw new InvalidDataException("F23.3 source mismatch");
            var human=d["humanReview"]!;
            Keys(human,"reviewer","reviewedAt","authorityReference","decision","decisionReference");
            if(human["reviewer"] is not null||human["reviewedAt"] is not null||
                human["authorityReference"] is not null||human["decisionReference"] is not null||
                S(human,"decision")!="NOT_MADE")throw new InvalidDataException("F23.3 forged human decision");
            var gate=d["gate"]!;
            Keys(gate,"gateId","criterion","status","humanSignoff");
            var g5=gates["gates"]!.AsArray();
            if(g5.Count!=5||S(g5[4]!,"gateId")!="G5"||
                g5[4]!["criteria"]!.AsArray().Count!=1||
                g5[4]!["criteria"]![0]!.GetValue<string>()!="A12"||
                S(gate,"gateId")!="G5"||S(gate,"criterion")!="A12"||
                S(gate,"status")!="GATE-BLOQUEADO"||gate["humanSignoff"]!.GetValue<bool>())
                throw new InvalidDataException("F23.3 G5/A12 not blocked");
            var evidence=d["evidence"]!;
            Keys(evidence,"b03","historicalProvenanceAttested","ciIsHumanApproval","prReadyIsHumanApproval");
            if(S(evidence,"b03")!="PENDING-DECLARED"||
                evidence["historicalProvenanceAttested"]!.GetValue<bool>()||
                evidence["ciIsHumanApproval"]!.GetValue<bool>()||
                evidence["prReadyIsHumanApproval"]!.GetValue<bool>()||
                S(b03["decision"]!,"b03")!="PENDING-DECLARED")
                throw new InvalidDataException("F23.3 CI/historical promotion");
            var r=risks["risks"]!.AsArray();var decisions=d["riskDecisions"]!.AsArray();
            if(r.Count!=8||decisions.Count!=8)throw new InvalidDataException("F23.3 incomplete risk decisions");
            for(int i=0;i<8;i++)
            {
                var item=decisions[i]!;Keys(item,"riskId","accepted","acceptanceReference");
                if(S(item,"riskId")!="RA-"+(i+1).ToString("00")||
                    S(item,"riskId")!=S(r[i]!,"riskId")||
                    item["accepted"]!.GetValue<bool>()||item["acceptanceReference"] is not null||
                    r[i]!["riskAccepted"]!.GetValue<bool>()||r[i]!["riskAcceptanceReference"] is not null)
                    throw new InvalidDataException("F23.3 accepted/mismatched risk");
            }
            var outcome=d["outcome"]!;Keys(outcome,"approved","operational","approvedPlayerMaps");
            if(outcome["approved"]!.GetValue<bool>()||outcome["operational"]!.GetValue<bool>()||
                outcome["approvedPlayerMaps"]!.GetValue<int>()!=0||
                risks["decision"]!["approved"]!.GetValue<bool>()||
                risks["decision"]!["operational"]!.GetValue<bool>()||
                S(risks["decision"]!,"decisionStatus")!="NOT_MADE"||
                S(risks["decision"]!,"gateA12")!="GATE-BLOQUEADO"||
                S(gates["decision"]!,"gateA12")!="GATE-BLOQUEADO")
                throw new InvalidDataException("F23.3 operational approval prohibited");
        }
        catch(Exception ex) when(ex is InvalidOperationException or ArgumentException or JsonException or NullReferenceException or KeyNotFoundException)
        {throw new InvalidDataException("F23.3 closed schema rejection",ex);}
    }
    private static string S(JsonNode n,string k)=>n[k]?.GetValue<string>()??throw new InvalidDataException("F23.3 missing "+k);
    private static void Keys(JsonNode n,params string[] keys)
    {
        var obj=n.AsObject();
        if(obj.Count!=keys.Length||keys.Any(k=>!obj.ContainsKey(k))||
            obj.Any(p=>!keys.Contains(p.Key,StringComparer.Ordinal)))
            throw new InvalidDataException("F23.3 unexpected keys");
    }
    private static string Sha(byte[] b)=>Convert.ToHexString(SHA256.HashData(b)).ToLowerInvariant();
    private static string FindFolder()
    {
        DirectoryInfo? d=new(AppContext.BaseDirectory);
        for(int i=0;i<12&&d is not null;i++,d=d.Parent)
        {
            string path=Path.Combine(d.FullName,"tests","FMRTE2008.MemorySimulation");
            if(File.Exists(Path.Combine(path,"F23ResidualRisks.json")))return path;
        }
        throw new DirectoryNotFoundException("F23.3 checkout missing");
    }
}
