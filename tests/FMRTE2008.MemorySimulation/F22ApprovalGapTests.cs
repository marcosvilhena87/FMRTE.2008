using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FMRTE2008.MemorySimulation;

/// <summary>F22 closed documentary gates; never accesses a game or grants operational approval.</summary>
internal static class F22ApprovalGapTests
{
    private const string MapRepo = "marcosvilhena87/FM2008.mapping";
    private const string MapCommit = "df5be0493a1ac7be57208ff0e91c2d7193dfec73";
    private const string Kind = "f22-offline-approval-gap-matrix";
    private const string EvidenceKind = "historical-user-report-plus-offline-ci-not-equivalent";
    private static readonly string[] Ids = ["G1", "G2", "G3", "G4", "G5"];
    private static readonly string[][] ExpectedCriteria =
    [
        ["A01", "A02"],
        ["A03", "A04", "A05", "A10"],
        ["A06", "A07", "A08"],
        ["A09", "A11"],
        ["A12"]
    ];
    private static readonly string[][] ExpectedRisks =
    [
        ["RA-01", "RA-06"],
        ["RA-05"],
        ["RA-02", "RA-03", "RA-04"],
        ["RA-07", "RA-08"],
        []
    ];
    private static readonly string[][] ExpectedObservations =
    [
        ["R01", "R02"],
        ["R03", "R04", "R07"],
        ["R05", "R06", "R07"],
        ["R01", "R07"],
        ["R01", "R02", "R03", "R04", "R05", "R06", "R07"]
    ];
    private static readonly (string Id, string Path, string Blob)[] Source =
    [
        ("R01", "research/builds/fm2008-8.0.2v1-first-save-distributed-sampling-2026-11-18.md", "fcbdffa2b846af2a36feaebca25b59a27f7c0912"),
        ("R02", "research/builds/fm2008-8.0.2v1-first-save-distributed-sampling-2026-11-18.md", "fcbdffa2b846af2a36feaebca25b59a27f7c0912"),
        ("R03", "research/builds/fm2008-8.0.2v1-first-save-distributed-sampling-2026-11-18.md", "fcbdffa2b846af2a36feaebca25b59a27f7c0912"),
        ("R04", "research/builds/fm2008-8.0.2v1-first-save-r04-profile-checks.csv", "45d50af819f0429e1bf887fee4b908e262b88d49"),
        ("R05", "research/builds/fm2008-8.0.2v1-r05-week-advance-2026-11-18.md", "6f9be7702e01e4023aef95d1efa540c399887d1f"),
        ("R06", "research/builds/fm2008-8.0.2v1-r06-normal-restart-2026-11-18.md", "89a1dae5102062d5e880158a7aa485a1483b028c"),
        ("R07", "research/builds/fm2008-8.0.2v1-r07-consolidation.md", "5af5157240def9b9b31d2a9533377f89892b94a6")
    ];

    internal static void Run()
    {
        var root = FindRoot();
        var path = Path.Combine(root, "tests", "FMRTE2008.MemorySimulation", "F22ApprovalGates.json");
        byte[] raw = File.ReadAllBytes(path);
        string digest = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
        var document = JsonNode.Parse(raw) ?? throw new InvalidDataException("F22 null matrix");
        Validate(document);
        int rejected = 0;
        var cases = new List<object>();
        void Reject(string id, Action<JsonNode> change)
        {
            var altered = document.DeepClone();
            change(altered);
            try { Validate(altered); }
            catch (InvalidDataException ex)
            {
                rejected++;
                cases.Add(new { caseId=id, expectedOutcome="InvalidDataException",
                    actualOutcome=ex.GetType().Name, result="rejected", rejection=ex.Message });
                Console.WriteLine("OK F22 NEG " + id);
                return;
            }
            throw new Exception("F22 negative mutation accepted: " + id);
        }
        Reject("approve-map", x => x["decision"]!["approved"] = true);
        Reject("operational-map", x => x["decision"]!["operational"] = true);
        Reject("open-A12", x => x["decision"]!["gateA12"] = "APPROVED");
        Reject("close-B03-without-evidence", x => x["decision"]!["b03"] = "VERIFIED");
        Reject("fake-human-signoff", x => x["decision"]!["humanApproval"] = "CI");
        Reject("wrong-mapping-repo", x => x["mappingSource"]!["repository"] = "unrelated/repository");
        Reject("wrong-source-commit", x => x["mappingSource"]!["commit"] = new string('0',40));
        Reject("missing-gate", x => x["gates"]!.AsArray().RemoveAt(4));
        Reject("duplicate-gate", x => x["gates"]![1]!["gateId"] = "G1");
        Reject("wrong-gate-order", x => x["gates"]![0]!["gateId"] = "G5");
        Reject("unexpected-root-key", x => x["autoApprove"] = true);
        Reject("unexpected-gate-key", x => x["gates"]![0]!["allowedOffsets"] = "legacy");
        Reject("unexpected-reference-key", x => x["gates"]![0]!["evidenceReferences"]![0]!["ciApproved"] = true);
        Reject("remove-criterion", x => x["gates"]![1]!["criteria"]!.AsArray().RemoveAt(0));
        Reject("substitute-criterion", x => x["gates"]![1]!["criteria"]![0] = "A12");
        Reject("remove-risk", x => x["gates"]![2]!["risks"]!.AsArray().RemoveAt(0));
        Reject("remove-reference", x => x["gates"]![0]!["evidenceReferences"]!.AsArray().RemoveAt(0));
        Reject("invent-reference", x => x["gates"]![0]!["evidenceReferences"]![0]!["observationId"] = "R09");
        Reject("forge-source-blob", x => x["gates"]![1]!["evidenceReferences"]![0]!["sourceBlobSha"] = new string('0',40));
        Reject("forge-source-path", x => x["gates"]![2]!["evidenceReferences"]![0]!["sourcePath"] = "invented.md");
        Reject("promote-document-status", x => x["gates"]![3]!["documentaryStatus"] = "APPROVED");
        Reject("promote-gate-status", x => x["gates"]![3]!["approvalDecision"] = "APPROVED");
        Reject("ci-equals-human", x => x["gates"]![4]!["evidenceKind"] = "human-approved");
        Reject("promote-provenance", x => x["gates"]![4]!["evidenceReferences"]![0]!["provenanceStatus"] = "VERIFIED");
        Reject("empty-gap", x => x["gates"]![0]!["pendingIssue"] = "");
        Reject("empty-context", x => x["gates"]![1]!["documentedContext"] = "");
        Reject("schema-version", x => x["schemaVersion"] = 2);
        Reject("new-legacy-offset", x => x["legacyOffsets"] = new JsonArray(256, 280, 284));
        if (rejected != 28 || cases.Count != 28) throw new InvalidDataException("F22 mutation inventory mismatch");

        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
        {
            string temp = Environment.GetEnvironmentVariable("RUNNER_TEMP")
                ?? throw new InvalidDataException("F22 missing runner temp");
            string folder = Path.Combine(temp, "f20-evidence");
            Directory.CreateDirectory(folder);
            File.Copy(path, Path.Combine(folder, "F22ApprovalGates.json"), true);
            File.WriteAllText(Path.Combine(folder, "F22ApprovalGateValidation.json"),
                JsonSerializer.Serialize(new {schemaVersion=1, evidenceKind=Kind,
                    matrixSha256=digest, gateCount=Ids.Length, rejectedMutations=rejected,
                    negativeCases=cases, approved=false, operational=false,
                    b03="PENDING-DECLARED", gateA12="GATE-BLOQUEADO", humanApproval=false },
                    new JsonSerializerOptions { WriteIndented=true }));
        }
        Console.WriteLine($"OK F22: G1-G5=5, references validated, negatives={rejected}, sha256={digest}, approved=false operational=false");
    }

    private static void Validate(JsonNode root)
    {
        try
        {
            Keys(root, "schemaVersion","kind","mappingSource","decision","gates");
            if (root["schemaVersion"]!.GetValue<int>() != 1 || S(root,"kind") != Kind)
                throw new InvalidDataException("F22 schema mismatch");
            var source = root["mappingSource"]!;
            Keys(source, "repository","commit");
            if (S(source,"repository") != MapRepo || S(source,"commit") != MapCommit)
                throw new InvalidDataException("F22 mapping source mismatch");
            var decision = root["decision"]!;
            Keys(decision,"approved","operational","gateA12","b03","humanApproval");
            if (decision["approved"]!.GetValue<bool>() || decision["operational"]!.GetValue<bool>() ||
                S(decision,"gateA12") != "GATE-BLOQUEADO" ||
                S(decision,"b03") != "PENDING-DECLARED" || decision["humanApproval"] is not null)
                throw new InvalidDataException("F22 forbidden approval");
            var gates = root["gates"]!.AsArray();
            if (gates.Count != Ids.Length) throw new InvalidDataException("F22 gates missing");
            for (int i=0;i<Ids.Length;i++)
            {
                var gate = gates[i] ?? throw new InvalidDataException("F22 null gate");
                Keys(gate,"gateId","title","criteria","risks","documentaryStatus",
                    "approvalDecision","evidenceKind","evidenceReferences","documentedContext","pendingIssue");
                if (S(gate,"gateId") != Ids[i] || S(gate,"title").Length == 0 ||
                    S(gate,"documentaryStatus") != "PENDING" ||
                    S(gate,"approvalDecision") != "NOT_APPROVED" ||
                    S(gate,"evidenceKind") != EvidenceKind ||
                    S(gate,"documentedContext").Length < 10 || S(gate,"pendingIssue").Length < 10)
                    throw new InvalidDataException("F22 invalid gate state: " + i);
                CheckArray(gate["criteria"]!,ExpectedCriteria[i],"criteria");
                CheckArray(gate["risks"]!,ExpectedRisks[i],"risks");
                var references = gate["evidenceReferences"]!.AsArray();
                if (references.Count != ExpectedObservations[i].Length)
                    throw new InvalidDataException("F22 wrong reference count");
                for(int n=0;n<references.Count;n++)
                {
                    var e = references[n] ?? throw new InvalidDataException("F22 null reference");
                    Keys(e,"observationId","sourcePath","sourceBlobSha","provenanceStatus");
                    string id = ExpectedObservations[i][n];
                    var original = Source.Single(s => s.Id == id);
                    if (S(e,"observationId") != id ||
                        S(e,"sourcePath") != original.Path ||
                        S(e,"sourceBlobSha") != original.Blob ||
                        S(e,"provenanceStatus") != "PENDING-DECLARED")
                        throw new InvalidDataException("F22 invalid historical reference");
                }
            }
        }
        catch (Exception e) when (e is InvalidOperationException or NullReferenceException or
            ArgumentException or KeyNotFoundException or JsonException or FormatException)
        { throw new InvalidDataException("F22 closed schema rejected document",e); }
    }

    private static void CheckArray(JsonNode node,string[] expected,string label)
    {
        var a=node.AsArray();
        if(a.Count != expected.Length || a.Where((v,i)=>v?.GetValue<string>()!=expected[i]).Any())
            throw new InvalidDataException("F22 wrong " + label);
    }
    private static void Keys(JsonNode node,params string[] names)
    {
        var obj=node.AsObject();
        if(obj.Count!=names.Length || names.Any(n=>!obj.ContainsKey(n)) ||
            obj.Any(p=>!names.Contains(p.Key,StringComparer.Ordinal)))
            throw new InvalidDataException("F22 closed schema keys mismatch");
    }
    private static string S(JsonNode node,string key) =>
        node[key]?.GetValue<string>() ?? throw new InvalidDataException("F22 missing " + key);
    private static string FindRoot()
    {
        DirectoryInfo? d = new(AppContext.BaseDirectory);
        for(int i=0;i<12 && d is not null;i++,d=d.Parent)
            if(File.Exists(Path.Combine(d.FullName,"tests","FMRTE2008.MemorySimulation","F22ApprovalGates.json")))
                return d.FullName;
        throw new DirectoryNotFoundException("F22 matrix missing in checkout");
    }
}
