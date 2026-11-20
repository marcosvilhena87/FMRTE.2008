using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace FMRTE2008.MemorySimulation;

/// <summary>
/// F20: validates a versioned OFFLINE evidence ledger. Never opens files outside
/// the checked-out repository and never produces a map-approval decision.
/// </summary>
internal static class F20EvidenceLedgerTests
{
    private const string Baseline = "0e1c0eb7b15059e150ba54c936d1161e52d6572c";
    private const string Mapping = "df5be0493a1ac7be57208ff0e91c2d7193dfec73";
    private const string Source = "tests/FMRTE2008.MemorySimulation/F19ExecutableNegativeCases.cs";
    private const string Repo = "marcosvilhena87/FMRTE.2008";
    private const string ExpectedManifestSha256 = "204ef8c6f4e156a12d097d3107a2e953f20efcd4be9ef4e9a473da91bd2f23cb";

    private static readonly (string Id, string Outcome)[] Expected =
    [
        ("sha-mismatch", "IsRecognized=false"),
        ("x86-overflow", "OverflowException"),
        ("null-table-header", "InvalidDataException"),
        ("invalid-player-id", "InvalidDataException"),
        ("unmapped-read", "InvalidDataException"),
        ("changed-table-during-recheck", "InvalidDataException"),
        ("cancelled-session-read", "OperationCanceledException"),
        ("live-index-source-denied", "UnauthorizedAccessException"),
        ("deny-all-catalog", "null"),
        ("oversized-distributed-sample", "ArgumentOutOfRangeException"),
        ("wrong-version-denied", "IsRecognized=false"),
        ("no-implicit-approval", "false/false")
    ];

    internal static void Run()
    {
        string root = FindRoot();
        string path = Path.Combine(root, "tests", "FMRTE2008.MemorySimulation",
            "F20EvidenceLedger.json");
        byte[] source = File.ReadAllBytes(path);
        string digest = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
        if (digest != ExpectedManifestSha256)
            throw new InvalidDataException("F20 manifest differs from independently pinned SHA-256");
        var ledger = JsonNode.Parse(source) ?? throw new InvalidDataException("F20 ledger absent");
        Validate(ledger);
        var cases = ledger["testEvidence"]!.AsArray();
        var historic = ledger["historicalEvidence"]!.AsArray();
        int rejected = 0;
        void Mutation(string name, Action<JsonNode> change)
        {
            var altered = ledger.DeepClone();
            change(altered);
            try { Validate(altered); }
            catch (InvalidDataException) { rejected++; return; }
            throw new Exception("F20 failed to detect mutation: " + name);
        }
        Mutation("duplicate-criterion", x => x["testEvidence"]![1]!["criterion"] = "A01");
        Mutation("wrong-case-id", x => x["testEvidence"]![0]!["caseId"] = "unrelated-case");
        Mutation("wrong-expectation", x => x["testEvidence"]![2]!["expectedOutcome"] = "success");
        Mutation("wrong-evidence-kind", x => x["testEvidence"]![3]!["evidenceKind"] = "live-process");
        Mutation("missing-source-path", x => x["testEvidence"]![4]!["sourcePath"] = null);
        Mutation("invalid-commit-sha", x => x["testEvidence"]![5]!["sourceCommit"] = "1234");
        Mutation("different-source-commit", x => x["testEvidence"]![6]!["sourceCommit"] = new string('0',40));
        Mutation("missing-executor", x => x["testEvidence"]![7]!["executor"] = null);
        Mutation("invented-observation-time", x => x["testEvidence"]![8]!["observedAt"] = "2026-11-20T00:00:00Z");
        Mutation("approval-true", x => x["approval"]!["approved"] = true);
        Mutation("operational-true", x => x["approval"]!["operational"] = true);
        Mutation("A12-auto-approval", x => x["approval"]!["gateA12"] = "approved-by-ci");
        Mutation("invented-R01-observer", x => x["historicalEvidence"]![0]!["executor"] = "inferred-from-git");
        Mutation("invented-R02-observation-date", x => x["historicalEvidence"]![1]!["observedAt"] = "2026-11-19T00:00:00Z");
        Mutation("invented-R03-environment", x => x["historicalEvidence"]![2]!["environment"] = "windows");
        Mutation("historical-approval", x => x["historicalEvidence"]![3]!["status"] = "APPROVED");
        Mutation("missing-R07", x => x["historicalEvidence"]!.AsArray().RemoveAt(6));
        Mutation("missing-A12", x => x["testEvidence"]!.AsArray().RemoveAt(11));
        Mutation("extra-root-property", x => x["approvedByCi"] = true);
        Mutation("extra-approval-property", x => x["approval"]!["approvedByCi"] = true);
        Mutation("extra-test-property", x => x["testEvidence"]![0]!["approved"] = true);
        Mutation("extra-historical-property", x => x["historicalEvidence"]![0]!["approved"] = true);
        Mutation("extra-workflow-property", x => x["testEvidence"]![1]!["workflowRunId"] = "invented");

        // The manifest references a fixed historical source commit, not a
        // claim that this current run was executed at that old commit.
        string actualSuite = File.ReadAllText(Path.Combine(root,
            "tests", "FMRTE2008.MemorySimulation", "F19ExecutableNegativeCases.cs"));
        foreach (var item in Expected)
            if (!actualSuite.Contains("\"" + item.Id + "\"", StringComparison.Ordinal))
                throw new InvalidDataException("F20 cited executable case not found: " + item.Id);
        string runner = File.ReadAllText(Path.Combine(root,
            "tests", "FMRTE2008.MemorySimulation", "Program.cs"));
        if (!runner.Contains("await F19ExecutableNegativeCases.RunAsync();", StringComparison.Ordinal) ||
            !runner.Contains("F20EvidenceLedgerTests.Run();", StringComparison.Ordinal))
            throw new InvalidDataException("F20 runner not linked to executable evidence cases");

        var observations = F20ExecutionEvidence.Snapshot();
        ValidateObserved(ledger, observations);
        int actualRejected = 0;
        void RejectObservedMutation(string name,
            Func<F20ExecutionEvidence.Observed[], F20ExecutionEvidence.Observed[]> mutate)
        {
            var altered = mutate(observations.ToArray());
            try { ValidateObserved(ledger, altered); }
            catch (InvalidDataException) { actualRejected++; return; }
            throw new Exception("F20 observed evidence mutation was not rejected: " + name);
        }
        RejectObservedMutation("case-omitted", rows => rows.Take(11).ToArray());
        RejectObservedMutation("case-duplicated", rows => rows.Take(11).Append(rows[0]).ToArray());
        RejectObservedMutation("wrong-result", rows => { rows[3] = rows[3] with { ActualOutcome = "success" }; return rows; });
        RejectObservedMutation("wrong-id", rows => { rows[0] = rows[0] with { CaseId = "other" }; return rows; });
        RejectObservedMutation("wrong-criterion", rows => { rows[5] = rows[5] with { Criterion = "A02" }; return rows; });

        var identity = F20CiProvenanceTests.Run(root);
        // Export the outcomes that passed the real validator. GitHub stores this
        // immutable artifact separately and supplies its artifact SHA-256 digest.
        string? runnerTemp = Environment.GetEnvironmentVariable("RUNNER_TEMP");
        if (identity.EventName != "local-unattested")
        {
            if (string.IsNullOrWhiteSpace(runnerTemp))
                throw new InvalidDataException("F20 missing RUNNER_TEMP for evidence export");
            string exportDir = Path.Combine(runnerTemp, "f20-evidence");
            Directory.CreateDirectory(exportDir);
            File.Copy(path, Path.Combine(exportDir, "F20EvidenceLedger.json"), true);
            var report = new
            {
                schemaVersion = 1,
                manifestSha256 = digest,
                checkoutSha = identity.CheckoutSha,
                prHeadSha = identity.PrHeadSha,
                workflowRunId = identity.RunId,
                workflowAttempt = identity.RunAttempt,
                eventName = identity.EventName,
                sourceCommit = Baseline,
                mappingCommit = Mapping,
                approval = new { approved = false, operational = false, gateA12 = "blocked" },
                observed = observations.Select(o => new { criterion = o.Criterion,
                    caseId = o.CaseId, actualOutcome = o.ActualOutcome }).ToArray(),
                manifestMutationRejects = rejected,
                observedMutationRejects = actualRejected
            };
            File.WriteAllText(Path.Combine(exportDir, "F20ExecutionEvidence.json"),
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("OK F20 structured CI artifact export: cases=" + observations.Count +
                ", manifestSha256=" + digest + ", runId=" + identity.RunId);
        }
        Console.WriteLine($"OK F20 evidence ledger: sha256={digest}, executionCheckoutSha={identity.CheckoutSha}, prHeadSha={identity.PrHeadSha}, workflowRunId={identity.RunId}, workflowAttempt={identity.RunAttempt}, event={identity.EventName}, sourceCommit={Baseline}, A01-A12={cases.Count}, R01-R07 pending={historic.Count}, mutation negatives={rejected}, observed-result negatives={actualRejected}, A12 blocked; offline only.");
    }

    private static void Validate(JsonNode node)
    {
        try { ValidateStructure(node); }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException
            or NullReferenceException or FormatException or JsonException or ArgumentOutOfRangeException)
        { throw new InvalidDataException("F20 malformed evidence ledger", ex); }
    }

    private static void ValidateStructure(JsonNode root)
    {
        RequireKeys(root, "schemaVersion", "approval", "baselineCommit", "mappingReferenceCommit", "testEvidence", "historicalEvidence");
        if (Number(root, "schemaVersion") != 1 ||
            Field(root, "baselineCommit") != Baseline ||
            Field(root, "mappingReferenceCommit") != Mapping)
            Fail("version/immutable base mismatch");
        var approval = root["approval"]!;
        RequireKeys(approval, "approved", "operational", "gateA12", "decisionReference");
        if (approval["approved"]!.GetValue<bool>() ||
            approval["operational"]!.GetValue<bool>() ||
            Field(approval, "gateA12") != "blocked" ||
            approval["decisionReference"] is not null)
            Fail("A12 approval state must remain blocked");
        var tests = root["testEvidence"]!.AsArray();
        if (tests.Count != Expected.Length) Fail("missing or additional A01-A12 records");
        var distinct = new HashSet<string>(StringComparer.Ordinal);
        for (int i=0; i<Expected.Length; i++)
        {
            var row=tests[i] ?? throw new InvalidDataException("null evidence row");
            RequireKeys(row, "criterion", "caseId", "testSuite", "expectedOutcome", "evidenceKind", "sourceRepo", "sourceCommit", "sourcePath", "executor", "observedAt", "status", "limits");
            var criterion = $"A{i+1:00}";
            if (Field(row,"criterion") != criterion ||
                !distinct.Add(Field(row,"criterion")) ||
                Field(row,"caseId") != Expected[i].Id ||
                Field(row,"expectedOutcome") != Expected[i].Outcome ||
                Field(row,"testSuite") != "F19ExecutableNegativeCases.RunAsync" ||
                Field(row,"evidenceKind") != "offline-simulation" ||
                Field(row,"sourceRepo") != Repo ||
                Field(row,"sourcePath") != Source ||
                Field(row,"sourceCommit") != Baseline ||
                !ValidSha(Field(row,"sourceCommit")) ||
                Field(row,"executor") != "GitHub Actions (Windows runner, on actual CI execution)" ||
                Field(row,"status") != "defined-not-independent-approval" ||
                !Text(row,"limits").Contains("never approves",StringComparison.Ordinal) ||
                row["observedAt"] is not null)
                Fail("invalid A01-A12 record " + criterion);
        }
        var history=root["historicalEvidence"]!.AsArray();
        if(history.Count != 7) Fail("R01-R07 missing or extra");
        for (int i=0; i<history.Count; i++)
        {
            var row=history[i] ?? throw new InvalidDataException("null historical row");
            RequireKeys(row, "id", "evidenceKind", "sourceRepo", "sourceCommit", "sourcePath", "executor", "observedAt", "environment", "status", "limits");
            if (Field(row,"id") != $"R{i+1:00}" ||
                Field(row,"evidenceKind") != "historical-user-report" ||
                Field(row,"sourceRepo") != "marcosvilhena87/FM2008.mapping" ||
                Field(row,"sourceCommit") != Mapping ||
                !ValidSha(Field(row,"sourceCommit")) ||
                Field(row,"sourcePath") != "research/builds/fm2008-8.0.2v1-minimal-map-approval-matrix.md" ||
                Field(row,"status") != "PENDING-DECLARED" ||
                !Text(row,"limits").Contains("B03",StringComparison.Ordinal) ||
                row["executor"] is not null || row["observedAt"] is not null ||
                row["environment"] is not null)
                Fail("B03 historical unknowns must remain pending");
        }
    }

    private static void RequireKeys(JsonNode node, params string[] keys)
    {
        var obj = node.AsObject();
        if (obj.Count != keys.Length ||
            obj.Select(k => k.Key).Except(keys, StringComparer.Ordinal).Any() ||
            keys.Any(key => !obj.ContainsKey(key)))
            throw new InvalidDataException("F20 unknown or missing schema property");
    }

    private static void ValidateObserved(JsonNode ledger,
        IReadOnlyList<F20ExecutionEvidence.Observed> records)
    {
        var expectedRows = ledger["testEvidence"]!.AsArray();
        if (records.Count != 12) throw new InvalidDataException("F20 missing observed cases");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < expectedRows.Count; i++)
        {
            var row = expectedRows[i]!;
            var actual = records[i];
            if (actual.Criterion != Field(row, "criterion") ||
                actual.CaseId != Field(row, "caseId") ||
                actual.ActualOutcome != Field(row, "expectedOutcome") ||
                !seen.Add(actual.Criterion + "/" + actual.CaseId))
                throw new InvalidDataException("F20 observed outcome mismatch: " + i);
        }
    }

    private static bool ValidSha(string sha) =>
        Regex.IsMatch(sha, @"\A[0-9a-f]{40}\z", RegexOptions.CultureInvariant);
    private static string Field(JsonNode item,string key) =>
        item[key]?.GetValue<string>() ?? throw new InvalidDataException("F20 missing: "+key);
    private static string Text(JsonNode item,string key) => Field(item,key);
    private static int Number(JsonNode item,string key) =>
        item[key]?.GetValue<int>() ?? throw new InvalidDataException("F20 missing number "+key);
    private static void Fail(string reason) => throw new InvalidDataException("F20: "+reason);

    private static string FindRoot()
    {
        DirectoryInfo? current=new(AppContext.BaseDirectory);
        for(int i=0; i<12 && current is not null; i++, current=current.Parent)
            if(File.Exists(Path.Combine(current.FullName,"tests","FMRTE2008.MemorySimulation",
                "F20EvidenceLedger.json"))) return current.FullName;
        throw new DirectoryNotFoundException("F20 ledger must be present in repository checkout");
    }
}
