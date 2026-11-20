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
    private const string ScopeResult = "documented-research-evidence-not-independently-attested";
    private static readonly (string Id, string Scope, string Path)[] Expected =
    [
        ("R01", "Build identification and process identity", "research/builds/fm2008-8.0.2v1-minimal-map-approval-matrix.md"),
        ("R02", "Module base and address calculation across sessions", "research/builds/fm2008-8.0.2v1-minimal-map-approval-matrix.md"),
        ("R03", "Distributed player identity sampling", "research/builds/fm2008-8.0.2v1-first-save-distributed-sampling-2026-11-18.md"),
        ("R04", "Name components and optional common name", "research/builds/fm2008-8.0.2v1-first-save-r04-profile-checks.csv"),
        ("R05", "Temporal consistency after natural game progress", "research/builds/fm2008-8.0.2v1-r05-week-advance-2026-11-18.md"),
        ("R06", "Normal stop and restart of diagnostic session", "research/builds/fm2008-8.0.2v1-r06-normal-restart-2026-11-18.md"),
        ("R07", "Changed count and second-save sampling", "research/builds/fm2008-8.0.2v1-r07-consolidation.md")
    ];

    internal static void Run()
    {
        string root = FindRoot();
        string file = Path.Combine(root, "tests", "FMRTE2008.MemorySimulation", "F21B03Evidence.json");
        byte[] bytes = File.ReadAllBytes(file);
        string sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var manifest = JsonNode.Parse(bytes) ?? throw new InvalidDataException("F21 empty inventory");
        Validate(manifest);
        int rejected = 0;
        void Reject(string label, Action<JsonNode> mutate)
        {
            var altered = manifest.DeepClone();
            mutate(altered);
            try { Validate(altered); }
            catch (InvalidDataException) { rejected++; Console.WriteLine("OK F21 NEG " + label); return; }
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

        if (rejected != 25) throw new InvalidDataException("F21 negative test inventory incomplete");
        string? temp = Environment.GetEnvironmentVariable("RUNNER_TEMP");
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true")
        {
            if (string.IsNullOrWhiteSpace(temp)) throw new InvalidDataException("F21 missing runner temp");
            string directory = Path.Combine(temp, "f20-evidence");
            Directory.CreateDirectory(directory);
            File.Copy(file, Path.Combine(directory, "F21B03Evidence.json"), true);
            var report = new { schemaVersion = 1, evidenceKind = Kind,
                manifestSha256 = sha256, observationCount = Expected.Length,
                provenanceStatus = Status, negativeMutationsRejected = rejected,
                approved = false, operational = false, gateA12 = "blocked",
                historicalObservationAttested = false };
            File.WriteAllText(Path.Combine(directory, "F21B03Validation.json"),
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        }
        Console.WriteLine($"OK F21 B03: R01-R07=7, all PENDING-DECLARED, negatives={rejected}, sha256={sha256}, no historical attestation, no map approval");
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
                Keys(row, "observationId", "sourceRepo", "sourceCommit", "sourcePath",
                    "evidenceScope", "reportedResult", "executor", "observedAt",
                    "environment", "collectionMethod", "externalCorroboration",
                    "provenanceStatus", "limitations");
                if (Value(row, "observationId") != Expected[i].Id ||
                    !unique.Add(Value(row, "observationId")) ||
                    Value(row, "sourceRepo") != Repository ||
                    Value(row, "sourceCommit") != MappingCommit ||
                    Value(row, "sourcePath") != Expected[i].Path ||
                    Value(row, "evidenceScope") != Expected[i].Scope ||
                    Value(row, "reportedResult") != ScopeResult ||
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
