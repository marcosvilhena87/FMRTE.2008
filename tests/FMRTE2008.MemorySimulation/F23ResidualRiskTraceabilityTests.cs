using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FMRTE2008.MemorySimulation;

/// <summary>F23.2 checks traceability of pending documentary risks only.</summary>
internal static class F23ResidualRiskTraceabilityTests
{
    private static readonly string[] ExpectedRiskIds =
        ["RA-01","RA-02","RA-03","RA-04","RA-05","RA-06","RA-07","RA-08"];

    internal static void Run()
    {
        string directory = FindDirectory();
        byte[] sourceBytes = File.ReadAllBytes(Path.Combine(directory,"F23ResidualRisks.json"));
        byte[] matrixBytes = File.ReadAllBytes(Path.Combine(directory,"F22ApprovalGates.json"));
        byte[] b03Bytes = File.ReadAllBytes(Path.Combine(directory,"F23B03ProvenanceGaps.json"));
        var source = JsonNode.Parse(sourceBytes) ?? throw new InvalidDataException("F23.2 null registry");
        var matrix = JsonNode.Parse(matrixBytes) ?? throw new InvalidDataException("F23.2 null F22");
        var b03 = JsonNode.Parse(b03Bytes) ?? throw new InvalidDataException("F23.2 null B03");
        Validate(source,matrix,b03);
        string? dir=null;
        if(Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true")
        {
            string temp=Environment.GetEnvironmentVariable("RUNNER_TEMP")
                ?? throw new InvalidDataException("F23.2 runner temp missing");
            dir=Path.Combine(temp,"f20-evidence","f23-risk-cases");
            Directory.CreateDirectory(dir);
        }
        var negatives=new List<object>();
        void Reject(string id, Action<JsonNode> mutate)
        {
            var copy=source.DeepClone();
            mutate(copy);
            if(dir is not null)
                File.WriteAllText(Path.Combine(dir,id+".json"),copy.ToJsonString());
            try { Validate(copy,matrix,b03); }
            catch(InvalidDataException ex)
            {
                negatives.Add(new {caseId=id,expectedOutcome="InvalidDataException",
                    actualOutcome=ex.GetType().Name,result="rejected",reason=ex.Message});
                Console.WriteLine("OK F23.2 NEG "+id);
                return;
            }
            throw new Exception("F23.2 mutation accepted: "+id);
        }
        Reject("approve-map",x=>x["decision"]!["approved"]=true);
        Reject("enable-operational",x=>x["decision"]!["operational"]=true);
        Reject("fake-human-approval",x=>x["decision"]!["humanApproval"]="CI");
        Reject("unblock-A12",x=>x["decision"]!["gateA12"]="APPROVED");
        Reject("close-B03",x=>x["decision"]!["b03"]="VERIFIED");
        Reject("forge-decision",x=>x["decision"]!["decisionStatus"]="APPROVED");
        Reject("accept-risk",x=>x["risks"]![0]!["riskAccepted"]=true);
        Reject("fabricate-acceptance-reference",x=>x["risks"]![1]!["riskAcceptanceReference"]="CI-run");
        Reject("promote-document-status",x=>x["risks"]![2]!["documentaryStatus"]="APPROVED");
        Reject("forge-risk-origin",x=>x["risks"]![3]!["origin"]="human-observed");
        Reject("wrong-risk-id",x=>x["risks"]![0]!["riskId"]="RA-09");
        Reject("duplicate-risk-id",x=>x["risks"]![1]!["riskId"]="RA-01");
        Reject("missing-risk",x=>x["risks"]!.AsArray().RemoveAt(7));
        Reject("swap-risks",x=>
        {
            var a=x["risks"]!.AsArray();
            var first=a[0]!.DeepClone();a[0]=a[1]!.DeepClone();a[1]=first;
        });
        Reject("wrong-gate",x=>x["risks"]![0]!["gateId"]="G5");
        Reject("criterion-from-other-gate",x=>x["risks"]![4]!["criteria"]![0]="A12");
        Reject("missing-criterion",x=>x["risks"]![5]!["criteria"]!.AsArray().RemoveAt(0));
        Reject("wrong-observation",x=>x["risks"]![6]!["observationIds"]![0]="R04");
        Reject("missing-observation",x=>x["risks"]![7]!["observationIds"]!.AsArray().RemoveAt(0));
        Reject("invent-description",x=>x["risks"]![0]!["description"]="All risks mitigated");
        Reject("wrong-map-commit",x=>x["source"]!["mappingCommit"]=new string('0',40));
        Reject("wrong-matrix-path",x=>x["source"]!["f22MatrixPath"]="other.json");
        Reject("wrong-B03-path",x=>x["source"]!["f23B03ManifestPath"]="other.json");
        Reject("unexpected-root",x=>x["promotedOffsets"]=true);
        Reject("unexpected-risk-property",x=>x["risks"]![0]!["offset"]=256);
        if(negatives.Count!=25) throw new InvalidDataException("F23.2 case inventory invalid");
        int sourceNegatives=0;
        void RejectSource(string id,JsonNode original,Action<JsonNode> mutate,bool isMatrix)
        {
            var copy=original.DeepClone();mutate(copy);
            try
            {
                if(isMatrix) Validate(source,copy,b03);
                else Validate(source,matrix,copy);
            }
            catch(InvalidDataException)
            {
                sourceNegatives++;
                Console.WriteLine("OK F23.2 LINK NEG "+id);
                return;
            }
            throw new Exception("F23.2 changed source accepted "+id);
        }
        RejectSource("changed-F22-gate-risk",matrix,x=>x["gates"]![0]!["risks"]![0]="RA-08",true);
        RejectSource("changed-F22-criterion",matrix,x=>x["gates"]![1]!["criteria"]![0]="A12",true);
        RejectSource("changed-F22-observation",matrix,x=>x["gates"]![2]!["evidenceReferences"]![0]!["observationId"]="R01",true);
        RejectSource("changed-B03-observation",b03,x=>x["observations"]![0]!["observationId"]="R07",false);
        RejectSource("B03-provenance-elevation",b03,x=>x["observations"]![0]!["provenanceStatus"]="VERIFIED",false);
        if(sourceNegatives!=5) throw new InvalidDataException("F23.2 source-negative mismatch");
        if(dir is not null)
        {
            string evidence=Directory.GetParent(dir)!.FullName;
            File.WriteAllBytes(Path.Combine(evidence,"F23ResidualRisks.json"),sourceBytes);
            File.WriteAllText(Path.Combine(evidence,"F23ResidualRiskValidation.json"),
                JsonSerializer.Serialize(new {schemaVersion=1,kind="f23-residual-risk-validation-offline",
                    registrySha256=Sha(sourceBytes),f22Sha256=Sha(matrixBytes),b03Sha256=Sha(b03Bytes),
                    riskCount=8,negativeCasesRejected=negatives.Count,negativeCases=negatives,
                    sourceLinkNegativesRejected=sourceNegatives,approved=false,operational=false,
                    riskAccepted=false,humanApproval=false,b03="PENDING-DECLARED",
                    gateA12="GATE-BLOQUEADO"},new JsonSerializerOptions{WriteIndented=true}));
        }
        Console.WriteLine($"OK F23.2 RA-01..08=8, rejected={negatives.Count}, sourceNegatives={sourceNegatives}, sha256={Sha(sourceBytes)}; no map approval");
    }

    private static void Validate(JsonNode root,JsonNode matrix,JsonNode b03)
    {
        try
        {
            Keys(root,"schemaVersion","kind","source","decision","risks");
            if(root["schemaVersion"]!.GetValue<int>()!=1 || S(root,"kind")!="f23-residual-risk-traceability-offline")
                throw new InvalidDataException("F23.2 identity mismatch");
            var sources=root["source"]!;
            Keys(sources,"f22MatrixPath","f23B03ManifestPath","mappingRepo","mappingCommit");
            if(S(sources,"f22MatrixPath")!="tests/FMRTE2008.MemorySimulation/F22ApprovalGates.json" ||
                S(sources,"f23B03ManifestPath")!="tests/FMRTE2008.MemorySimulation/F23B03ProvenanceGaps.json" ||
                S(sources,"mappingRepo")!=S(matrix["mappingSource"]!,"repository") ||
                S(sources,"mappingCommit")!=S(matrix["mappingSource"]!,"commit") ||
                S(sources,"mappingRepo")!=S(b03["source"]!,"repository") ||
                S(sources,"mappingCommit")!=S(b03["source"]!,"commit"))
                throw new InvalidDataException("F23.2 source identity mismatch");
            var decision=root["decision"]!;
            Keys(decision,"approved","operational","humanApproval","gateA12","b03","decisionStatus");
            if(decision["approved"]!.GetValue<bool>() || decision["operational"]!.GetValue<bool>() ||
                decision["humanApproval"] is not null || S(decision,"gateA12")!="GATE-BLOQUEADO" ||
                S(decision,"b03")!="PENDING-DECLARED" || S(decision,"decisionStatus")!="NOT_MADE")
                throw new InvalidDataException("F23.2 forbidden approval");
            var records=root["risks"]!.AsArray();
            var gates=matrix["gates"]!.AsArray();
            var observed=b03["observations"]!.AsArray();
            if(records.Count!=8 || gates.Count!=5 || observed.Count!=7)
                throw new InvalidDataException("F23.2 missing references");
            if(matrix["decision"]!["approved"]!.GetValue<bool>() ||
               matrix["decision"]!["operational"]!.GetValue<bool>() ||
               b03["decision"]!["approved"]!.GetValue<bool>() ||
               b03["decision"]!["operational"]!.GetValue<bool>() ||
               S(matrix["decision"]!,"gateA12")!="GATE-BLOQUEADO" ||
               S(b03["decision"]!,"b03")!="PENDING-DECLARED")
                throw new InvalidDataException("F23.2 source approval mismatch");
            for(int i=0;i<8;i++)
            {
                var record=records[i] ?? throw new InvalidDataException("F23.2 null risk");
                Keys(record,"riskId","gateId","criteria","observationIds","description",
                    "documentaryStatus","riskAccepted","riskAcceptanceReference","origin");
                var id=ExpectedRiskIds[i];
                var gate=gates.FirstOrDefault(g=>g!["risks"]!.AsArray().Any(v=>v?.GetValue<string>()==id))
                    ?? throw new InvalidDataException("F23.2 risk absent from F22");
                if(S(record,"riskId")!=id || S(record,"gateId")!=S(gate,"gateId") ||
                   S(record,"description")!=Description(id) ||
                   S(record,"documentaryStatus")!="PENDING" ||
                   record["riskAccepted"]!.GetValue<bool>() || record["riskAcceptanceReference"] is not null ||
                   S(record,"origin")!="f22-documentary-gap-not-independent-game-observation")
                   throw new InvalidDataException("F23.2 invalid pending risk");
                string[] criteria=gate["criteria"]!.AsArray().Select(x=>x!.GetValue<string>()).ToArray();
                string[] references=gate["evidenceReferences"]!.AsArray()
                    .Select(x=>S(x!,"observationId")).ToArray();
                CheckList(record["criteria"]!,criteria);
                CheckList(record["observationIds"]!,references);
                foreach(var obs in references)
                {
                    var entry=observed.SingleOrDefault(x=>S(x!,"observationId")==obs)
                        ?? throw new InvalidDataException("F23.2 absent B03 observation");
                    if(S(entry,"provenanceStatus")!="PENDING-DECLARED" ||
                        S(entry,"sourceEvidenceStatus")!="user-reported-not-independently-audited")
                        throw new InvalidDataException("F23.2 promoted historical source");
                    var linked=gate["evidenceReferences"]!.AsArray().Single(x=>S(x!,"observationId")==obs)!;
                    if(S(linked,"sourcePath")!=S(entry,"sourcePath") ||
                       S(linked,"sourceBlobSha")!=S(entry,"sourceBlobSha"))
                       throw new InvalidDataException("F23.2 divergent documentary evidence");
                }
            }
        }
        catch(Exception ex) when(ex is InvalidOperationException or ArgumentException or JsonException
            or NullReferenceException or KeyNotFoundException)
        { throw new InvalidDataException("F23.2 closed schema rejected",ex); }
    }
    private static string Description(string id)=> id switch
    {
        "RA-01" => "Build and instance identity not independently attested",
        "RA-02" => "Cross-snapshot consistency under natural table changes unverified",
        "RA-03" => "Termination during an actual read not historically observed",
        "RA-04" => "Session-switch and ABA recovery not historically demonstrated",
        "RA-05" => "Distributed ID/full-name samples do not validate entire table or common-name components",
        "RA-06" => "Naturally relocated module base not documented",
        "RA-07" => "Read-only scope and unapproved offset exclusion require continued guard",
        "RA-08" => "Build variant and map isolation must remain enforced",
        _ => throw new InvalidDataException("F23.2 unknown risk")
    };
    private static void CheckList(JsonNode node,string[] expected)
    {
        var items=node.AsArray();
        if(items.Count!=expected.Length || items.Where((x,i)=>x?.GetValue<string>()!=expected[i]).Any())
            throw new InvalidDataException("F23.2 source mapping inconsistent");
    }
    private static void Keys(JsonNode node,params string[] expected)
    {
        var o=node.AsObject();
        if(o.Count!=expected.Length || expected.Any(x=>!o.ContainsKey(x)) ||
           o.Any(x=>!expected.Contains(x.Key,StringComparer.Ordinal)))
            throw new InvalidDataException("F23.2 unexpected schema properties");
    }
    private static string S(JsonNode node,string name)=>node[name]?.GetValue<string>()
        ?? throw new InvalidDataException("F23.2 missing "+name);
    private static string Sha(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string FindDirectory()
    {
        DirectoryInfo? d=new(AppContext.BaseDirectory);
        for(int i=0;i<12&&d is not null;i++,d=d.Parent)
        {
            var path=Path.Combine(d.FullName,"tests","FMRTE2008.MemorySimulation");
            if(File.Exists(Path.Combine(path,"F22ApprovalGates.json")))return path;
        }
        throw new DirectoryNotFoundException("F23.2 checkout unavailable");
    }
}
