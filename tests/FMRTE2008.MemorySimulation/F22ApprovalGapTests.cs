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


    internal static void Run()
    {
        var root = FindRoot();
        var path = Path.Combine(root, "tests", "FMRTE2008.MemorySimulation", "F22ApprovalGates.json");
        byte[] raw = File.ReadAllBytes(path);
        string digest = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant();
        var document = JsonNode.Parse(raw) ?? throw new InvalidDataException("F22 null matrix");
        string f21path = Path.Combine(root, "tests", "FMRTE2008.MemorySimulation", "F21B03Evidence.json");
        byte[] f21bytes = File.ReadAllBytes(f21path);
        var f21 = JsonNode.Parse(f21bytes) ?? throw new InvalidDataException("F22 missing F21 inventory");
        Validate(document, f21);
        string f21Digest = Convert.ToHexString(SHA256.HashData(f21bytes)).ToLowerInvariant();
        string? mutationDir = null;
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
        {
            string runnerTemp = Environment.GetEnvironmentVariable("RUNNER_TEMP")
                ?? throw new InvalidDataException("F22 missing runner temp");
            mutationDir = Path.Combine(runnerTemp, "f20-evidence", "f22-schema-cases");
            Directory.CreateDirectory(mutationDir);
        }
        int rejected = 0;
        var cases = new List<object>();
        void Reject(string id, Action<JsonNode> change)
        {
            var altered = document.DeepClone();
            change(altered);
            if (mutationDir is not null)
                File.WriteAllText(Path.Combine(mutationDir, id + ".json"), altered.ToJsonString());
            try { Validate(altered, f21); }
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
        Reject("valid-sha-wrong-blob", x => x["gates"]![0]!["evidenceReferences"]![0]!["sourceBlobSha"] = new string('a',40));
        Reject("swap-reference-order", x =>
        {
            var arr = x["gates"]![0]!["evidenceReferences"]!.AsArray();
            var item = arr[0]!.DeepClone();
            arr[0] = arr[1]!.DeepClone();
            arr[1] = item;
        });
        Reject("swap-gate-criteria", x => x["gates"]![0]!["criteria"] = x["gates"]![3]!["criteria"]!.DeepClone());
        Reject("swap-gate-risks", x => x["gates"]![0]!["risks"] = x["gates"]![2]!["risks"]!.DeepClone());
        int f21Rejected = 0;
        void RejectF21(string id, Action<JsonNode> change)
        {
            var altered = f21.DeepClone();
            change(altered);
            try { Validate(document, altered); }
            catch (InvalidDataException)
            {
                f21Rejected++;
                Console.WriteLine("OK F22 F21 LINK NEG " + id);
                return;
            }
            throw new Exception("F22 F21 linkage mutation accepted: " + id);
        }
        RejectF21("changed-source-commit", x => x["sourceCommit"] = new string('0',40));
        RejectF21("fabricated-executor", x => x["observations"]![0]!["executor"] = "CI");
        RejectF21("invented-source-path", x => x["observations"]![3]!["sourcePath"] = "invented.md");
        RejectF21("invented-source-blob", x => x["observations"]![4]!["sourceBlobSha"] = new string('f',40));
        if (f21Rejected != 4) throw new InvalidDataException("F22 F21 linkage negative inventory incomplete");
        if (rejected != 32 || cases.Count != 32) throw new InvalidDataException("F22 mutation inventory mismatch");

        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
        {
            string temp = Environment.GetEnvironmentVariable("RUNNER_TEMP")
                ?? throw new InvalidDataException("F22 missing runner temp");
            string folder = Path.Combine(temp, "f20-evidence");
            Directory.CreateDirectory(folder);
            File.Copy(path, Path.Combine(folder, "F22ApprovalGates.json"), true);
            File.WriteAllText(Path.Combine(folder, "F22ApprovalGateValidation.json"),
                JsonSerializer.Serialize(new {schemaVersion=1, evidenceKind=Kind,
                    matrixSha256=digest, f21Sha256=f21Digest, gateCount=Ids.Length, rejectedMutations=rejected,
                    negativeCases=cases, f21LinkNegativesRejected=f21Rejected, approved=false, operational=false,
                    b03="PENDING-DECLARED", gateA12="GATE-BLOQUEADO", humanApproval=false },
                    new JsonSerializerOptions { WriteIndented=true }));
        }
        Console.WriteLine($"OK F22: G1-G5=5, references validated, negatives={rejected}, sha256={digest}, approved=false operational=false");
    }

    private static void Validate(JsonNode root, JsonNode f21)
    {
        try
        {
            Keys(root, "schemaVersion","kind","mappingSource","decision","gates");
            if (root["schemaVersion"]!.GetValue<int>() != 1 || S(root,"kind") != Kind)
                throw new InvalidDataException("F22 schema mismatch");
            Keys(f21,"schemaVersion","kind","sourceRepo","sourceCommit","approval","observations");
            if (f21["schemaVersion"]!.GetValue<int>() != 1 ||
                S(f21,"kind") != "historical-evidence-inventory-not-operational" ||
                S(f21,"sourceRepo") != MapRepo || S(f21,"sourceCommit") != MapCommit)
                throw new InvalidDataException("F22 F21 source identity mismatch");
            var f21Approval=f21["approval"]!;
            Keys(f21Approval,"approved","operational","gateA12","decisionReference");
            if (f21Approval["approved"]!.GetValue<bool>() ||
                f21Approval["operational"]!.GetValue<bool>() ||
                S(f21Approval,"gateA12") != "blocked" ||
                f21Approval["decisionReference"] is not null)
                throw new InvalidDataException("F22 F21 approval mismatch");
            var f21Rows=f21["observations"]!.AsArray();
            if (f21Rows.Count != 7) throw new InvalidDataException("F22 F21 incomplete evidence registry");
            for (int j=0;j<7;j++)
            {
                var row = f21Rows[j] ?? throw new InvalidDataException("F22 F21 null observation");
                if (S(row,"observationId") != "R0" + (j+1) ||
                    S(row,"sourceRepo") != MapRepo || S(row,"sourceCommit") != MapCommit ||
                    S(row,"provenanceStatus") != "PENDING-DECLARED" ||
                    row["executor"] is not null || row["observedAt"] is not null ||
                    row["environment"] is not null || row["collectionMethod"] is not null ||
                    row["externalCorroboration"] is not null)
                    throw new InvalidDataException("F22 F21 untrusted historical attribution");
            }
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
                    var original = f21Rows[int.Parse(id.AsSpan(1)) - 1]!;
                    if (S(e,"observationId") != id ||
                        S(e,"sourcePath") != S(original,"sourcePath") ||
                        S(e,"sourceBlobSha") != S(original,"sourceBlobSha") ||
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
