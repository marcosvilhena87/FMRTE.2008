using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FMRTE2008.MemorySimulation;

/// <summary>F21 historical evidence registry is research-only and cannot approve any map.</summary>
internal static class F21B03EvidenceTests
{
    private const string Repository = "marcosvilhena87/FM2008.mapping";
    private const string MappingCommit = "df5be0493a1ac7be57208ff0e91c2d7193dfec73";
    private const string Kind = "historical-evidence-inventory-not-operational";
    private const string Status = "PENDING-DECLARED";
    private const string SourceStatus = "user-reported-not-independently-audited";
    private static readonly (string Id, string Scope, string Path, string Blob, string ReportedResult)[] Expected =
    [
        ("R01", "Build identification and process identity", "research/builds/fm2008-8.0.2v1-first-save-distributed-sampling-2026-11-18.md", "fcbdffa2b846af2a36feaebca25b59a27f7c0912", "ReadOnlyProbe recognized build 8.0.2v1 for reported PID 24860; absent PID 1234 was rejected; modified-build rejection not observed."),
        ("R02", "Module base and address calculation across sessions", "research/builds/fm2008-8.0.2v1-first-save-distributed-sampling-2026-11-18.md", "fcbdffa2b846af2a36feaebca25b59a27f7c0912", "ModuleBase 0x00400000 and HeaderVA 0x01B1ED4C reported; no naturally relocated module base observed."),
        ("R03", "Distributed player identity sampling", "research/builds/fm2008-8.0.2v1-first-save-distributed-sampling-2026-11-18.md", "fcbdffa2b846af2a36feaebca25b59a27f7c0912", "First save reported 9 distributed positions from 6697 players, repeated twice; subsequent manual declaration confirmed 9/9 ID/full-name pairs, not common-name components."),
        ("R04", "Name components and optional common name", "research/builds/fm2008-8.0.2v1-first-save-r04-profile-checks.csv", "45d50af819f0429e1bf887fee4b908e262b88d49", "First-save profile-check CSV records eight additional ID/full-name confirmations; common names remain not separately confirmed."),
        ("R05", "Temporal consistency after natural game progress", "research/builds/fm2008-8.0.2v1-r05-week-advance-2026-11-18.md", "6f9be7702e01e4023aef95d1efa540c399887d1f", "Count changed 6697 to 6698 after one reported week; eight overlapping distributed positions remained identical; ABA and interrupted reads not demonstrated."),
        ("R06", "Normal stop and restart of diagnostic session", "research/builds/fm2008-8.0.2v1-r06-normal-restart-2026-11-18.md", "89a1dae5102062d5e880158a7aa485a1483b028c", "Normal restart changed PID 24860 to 17632 and table address; 9/9 ID/full-name pairs on the new session were manually reported confirmed; mid-read termination untested."),
        ("R07", "Changed count and second-save sampling", "research/builds/fm2008-8.0.2v1-r07-consolidation.md", "5af5157240def9b9b31d2a9533377f89892b94a6", "Two saves with 6697 and 5154 players each have nine distributed positions and 9/9 manually declared ID/full-name matches; no full-table or common-name validation.")
    ];

    internal static void Run()
    {
        string root = FindRoot();
        string file = Path.Combine(root, "tests", "FMRTE2008.MemorySimulation", "F21B03Evidence.json");
        byte[] bytes = File.ReadAllBytes(file);
        string sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var manifest = JsonNode.Parse(bytes) ?? throw new InvalidDataException("F21 empty inventory");
        Validate(manifest);
        VerifyFrozenSourceTree(manifest);
        int rejected = 0;
        var negativeResults = new List<object>();
        void Reject(string label, Action<JsonNode> mutate)
        {
            var altered = manifest.DeepClone();
            mutate(altered);
            try { Validate(altered); }
            catch (InvalidDataException ex)
            {
                rejected++;
                negativeResults.Add(new { caseId = label, expectedOutcome = "InvalidDataException",
                    actualOutcome = ex.GetType().Name, result = "rejected",
                    reasonCode = ex.Message.StartsWith("F21", StringComparison.Ordinal) ? ex.Message : "F21 invalid inventory" });
                Console.WriteLine("OK F21 NEG " + label);
                return;
            }
            throw new Exception("F21 mutation accepted: " + label);
        }
        Reject("missing-R07", x => x["observations"]!.AsArray().RemoveAt(6));
        Reject("duplicate-R01", x => x["observations"]![1]!["observationId"] = "R01");
        Reject("reordered-R01", x => x["observations"]![0]!["observationId"] = "R06");
        Reject("invented-executor", x => x["observations"]![0]!["executor"] = "git-commit-author");
        Reject("invented-time", x => x["observations"]![1]!["observedAt"] = "2026-11-18T00:00:00Z");
        Reject("invented-environment", x => x["observations"]![2]!["environment"] = "Windows");
        Reject("invented-collection-method", x => x["observations"]![3]!["collectionMethod"] = "read-only-process");
        Reject("invented-corroboration", x => x["observations"]![4]!["externalCorroboration"] = "github-actions");
        Reject("implicit-approval", x => x["approval"]!["approved"] = true);
        Reject("operational-true", x => x["approval"]!["operational"] = true);
        Reject("gate-open", x => x["approval"]!["gateA12"] = "approved");
        Reject("approval-reference", x => x["approval"]!["decisionReference"] = "CI");
        Reject("unknown-root-property", x => x["approvedByCI"] = true);
        Reject("unknown-row-property", x => x["observations"]![0]!["workflowRunId"] = "1");
        Reject("unknown-approval-property", x => x["approval"]!["reviewer"] = "invented");
        Reject("missing-row-property", x => x["observations"]![0]!.AsObject().Remove("limitations"));
        Reject("wrong-source-repo", x => x["observations"]![0]!["sourceRepo"] = "other/repo");
        Reject("wrong-source-commit", x => x["observations"]![1]!["sourceCommit"] = new string('0', 40));
        Reject("wrong-source-path", x => x["observations"]![2]!["sourcePath"] = "fabricated.md");
        Reject("wrong-scope", x => x["observations"]![3]!["evidenceScope"] = "unverified");
        Reject("invented-result", x => x["observations"]![4]!["reportedResult"] = "approved");
        Reject("invented-status", x => x["observations"]![5]!["provenanceStatus"] = "VERIFIED");
        Reject("missing-B03-limitation", x => x["observations"]![6]!["limitations"] = "all valid");
        Reject("changed-manifest-kind", x => x["kind"] = "approved-operational-maps");
        Reject("changed-schema-version", x => x["schemaVersion"] = 2);

        Reject("wrong-source-blob-sha", x => x["observations"]![0]!["sourceBlobSha"] = new string('0', 40));
        Reject("missing-source-status", x => x["observations"]![1]!.AsObject().Remove("sourceEvidenceStatus"));
        Reject("approved-source-status", x => x["observations"]![2]!["sourceEvidenceStatus"] = "independently-approved");
        Reject("swapped-result-R01", x => x["observations"]![0]!["reportedResult"] = Expected[1].ReportedResult);
        if (rejected != 29) throw new InvalidDataException("F21 negative test inventory incomplete");
        string? temp = Environment.GetEnvironmentVariable("RUNNER_TEMP");
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
        {
            if (string.IsNullOrWhiteSpace(temp)) throw new InvalidDataException("F21 missing runner temp");
            string directory = Path.Combine(temp, "f20-evidence");
            Directory.CreateDirectory(directory);
            File.Copy(file, Path.Combine(directory, "F21B03Evidence.json"), true);
            var report = new { schemaVersion = 1, evidenceKind = Kind,
                manifestSha256 = sha256, observationCount = Expected.Length,
                provenanceStatus = Status, negativeMutationsRejected = rejected, negativeCases = negativeResults,
                approved = false, operational = false, gateA12 = "blocked",
                historicalObservationAttested = false };
            File.WriteAllText(Path.Combine(directory, "F21B03Validation.json"),
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine($"OK F21 B03: R01-R07=7, all PENDING-DECLARED, negatives={rejected}, sha256={sha256}, no historical attestation, no map approval");
    }


    // Independent, read-only GitHub source-tree check: a source blob must exist
    // at the declared immutable mapping commit with its exact Git blob SHA.
    // It proves document identity, never the truth or authorship of historical observations.
    private static void VerifyFrozenSourceTree(JsonNode ledger)
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true") return;
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("FMRTE2008-F21-document-attestation/1");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        var uri = "https://api.github.com/repos/" + Repository +
            "/git/trees/" + MappingCommit + "?recursive=1";
        using var response = client.GetAsync(uri).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
            throw new InvalidDataException("F21 frozen research tree unavailable: HTTP " + (int)response.StatusCode);
        using var treeDoc = JsonDocument.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
        var root = treeDoc.RootElement;
        if (root.GetProperty("sha").GetString() != MappingCommit ||
            root.GetProperty("truncated").GetBoolean())
            throw new InvalidDataException("F21 research tree identity mismatch/truncated");
        var entries = root.GetProperty("tree").EnumerateArray()
            .Where(x => x.GetProperty("type").GetString() == "blob")
            .ToDictionary(x => x.GetProperty("path").GetString()!,
                x => x.GetProperty("sha").GetString()!, StringComparer.Ordinal);
        foreach (var item in Expected)
            if (!entries.TryGetValue(item.Path, out var sha) || sha != item.Blob)
                throw new InvalidDataException("F21 frozen source blob mismatch: " + item.Id);
        Console.WriteLine("OK F21 external research source tree: commit=" + MappingCommit +
            ", uniqueSourceDocuments=" + Expected.Select(x => x.Path).Distinct().Count() +
            ", blob commitments verified; no historical attestation");
    }

    private static void Validate(JsonNode root)
    {
        try
        {
            Keys(root, "schemaVersion", "kind", "sourceRepo", "sourceCommit", "approval", "observations");
            if (root["schemaVersion"]!.GetValue<int>() != 1 ||
                Value(root, "kind") != Kind ||
                Value(root, "sourceRepo") != Repository ||
                Value(root, "sourceCommit") != MappingCommit)
                throw new InvalidDataException("F21 root provenance mismatch");
            var approval = root["approval"]!;
            Keys(approval, "approved", "operational", "gateA12", "decisionReference");
            if (approval["approved"]!.GetValue<bool>() ||
                approval["operational"]!.GetValue<bool>() ||
                Value(approval, "gateA12") != "blocked" ||
                approval["decisionReference"] is not null)
                throw new InvalidDataException("F21 approval gate must remain closed");
            var rows = root["observations"]!.AsArray();
            if (rows.Count != Expected.Length) throw new InvalidDataException("F21 missing historical observations");
            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i=0; i<Expected.Length; i++)
            {
                var row = rows[i] ?? throw new InvalidDataException("F21 null row");
                Keys(row, "observationId", "sourceRepo", "sourceCommit", "sourcePath", "sourceBlobSha", "sourceEvidenceStatus",
                    "evidenceScope", "reportedResult", "executor", "observedAt",
                    "environment", "collectionMethod", "externalCorroboration",
                    "provenanceStatus", "limitations");
                if (Value(row, "observationId") != Expected[i].Id ||
                    !unique.Add(Value(row, "observationId")) ||
                    Value(row, "sourceRepo") != Repository ||
                    Value(row, "sourceCommit") != MappingCommit ||
                    Value(row, "sourcePath") != Expected[i].Path ||
                    Value(row, "sourceBlobSha") != Expected[i].Blob ||
                    Value(row, "sourceEvidenceStatus") != SourceStatus ||
                    Value(row, "evidenceScope") != Expected[i].Scope ||
                    Value(row, "reportedResult") != Expected[i].ReportedResult ||
                    Value(row, "provenanceStatus") != Status ||
                    !Value(row, "limitations").StartsWith("B03:", StringComparison.Ordinal) ||
                    row["executor"] is not null ||
                    row["observedAt"] is not null ||
                    row["environment"] is not null ||
                    row["collectionMethod"] is not null ||
                    row["externalCorroboration"] is not null)
                    throw new InvalidDataException("F21 historical row is not a pending, grounded reference: " + i);
            }
        }
        catch (Exception error) when (error is InvalidOperationException or KeyNotFoundException or
            NullReferenceException or FormatException or JsonException or ArgumentException)
        {
            throw new InvalidDataException("F21 invalid closed schema", error);
        }
    }

    private static void Keys(JsonNode node, params string[] expected)
    {
        var obj = node.AsObject();
        if (obj.Count != expected.Length ||
            obj.Any(x => !expected.Contains(x.Key, StringComparer.Ordinal)) ||
            expected.Any(k => !obj.ContainsKey(k)))
            throw new InvalidDataException("F21 unknown or missing property");
    }
    private static string Value(JsonNode obj, string name) =>
        obj[name]?.GetValue<string>() ?? throw new InvalidDataException("F21 missing " + name);

    private static string FindRoot()
    {
        DirectoryInfo? d = new(AppContext.BaseDirectory);
        for (int i=0; i<12 && d is not null; i++, d=d.Parent)
            if (File.Exists(Path.Combine(d.FullName, "tests", "FMRTE2008.MemorySimulation", "F21B03Evidence.json")))
                return d.FullName;
        throw new DirectoryNotFoundException("F21 inventory not in checkout");
    }
}
