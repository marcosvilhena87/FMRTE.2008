using System.Diagnostics;
using System.Text.Json;

namespace FMRTE2008.MemorySimulation;

/// <summary>
/// F20-R02: validates local GitHub Actions execution identity against the
/// event payload AND checked-out Git commit. Does not use network or game.
/// The workflow runner's own event data is not independent attestation.
/// </summary>
internal static class F20CiProvenanceTests
{
    internal sealed record Identity(
        string EventName, string EventSha, string CheckedOutSha, string EventJson,
        string Repo, string Ref, string HeadRef, string RunId, string RunAttempt);

    internal sealed record Verified(
        string EventName, string CheckoutSha, string PrHeadSha,
        string RunId, string RunAttempt);

    private const string Repository = "marcosvilhena87/FMRTE.2008";

    internal static Verified Run(string root)
    {
        var actual = ReadFromRunner(root);
        var result = Validate(actual);

        // Call the same validator as production with deterministic mutations.
        int rejected = 0;
        void Reject(string title, Identity altered)
        {
            try { Validate(altered); }
            catch (InvalidDataException) { rejected++; return; }
            throw new InvalidOperationException("F20 CI provenance mutation accepted: " + title);
        }
        Reject("wrong-worktree", actual with { CheckedOutSha = new string('0', 40) });
        Reject("wrong-execution-sha", actual with { EventSha = new string('1', 40) });
        Reject("wrong-run-id", actual with { RunId = "invalid" });
        Reject("wrong-attempt", actual with { RunAttempt = "0" });
        Reject("wrong-repository", actual with { Repo = "unrelated/project" });
        Reject("wrong-event-kind", actual with { EventName = "workflow_dispatch" });
        Reject("wrong-event-head", MutateEventHead(actual));
        Reject("wrong-ref", actual with { Ref = "refs/heads/not-this-branch" });
        if (rejected != 8) throw new InvalidDataException("F20 provenance negative inventory incomplete");

        Console.WriteLine($"OK F20 CI identity: event={result.EventName}, checkoutSha={result.CheckoutSha}, prHeadSha={result.PrHeadSha}, workflowRunId={result.RunId}, attempt={result.RunAttempt}, provenance negatives={rejected}; runner-owned evidence only.");
        return result;
    }

    private static Identity ReadFromRunner(string root)
    {
        var env = Environment.GetEnvironmentVariable("GITHUB_ACTIONS");
        if (!string.Equals(env, "true", StringComparison.OrdinalIgnoreCase))
            return new("local-unattested", "", "", "", "", "", "", "", "");

        string Require(string name) => Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidDataException("F20 missing GitHub environment " + name);
        string eventPath = Require("GITHUB_EVENT_PATH");
        // GitHub Actions supplies the path. It is never game-related data.
        string payload = File.ReadAllText(eventPath);
        string worktree = GitHead(root);
        return new(
            Require("GITHUB_EVENT_NAME"), Require("GITHUB_SHA"), worktree,
            payload, Require("GITHUB_REPOSITORY"), Require("GITHUB_REF"),
            Environment.GetEnvironmentVariable("GITHUB_HEAD_REF") ?? "",
            Require("GITHUB_RUN_ID"), Require("GITHUB_RUN_ATTEMPT"));
    }

    private static string GitHead(string root)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("git", "rev-parse HEAD")
            {
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        if (!process.Start()) throw new InvalidDataException("F20 failed to inspect Git checkout");
        string output = process.StandardOutput.ReadToEnd().Trim().ToLowerInvariant();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 || !ValidSha(output))
            throw new InvalidDataException("F20 Git checkout SHA unavailable: " + error);
        return output;
    }

    private static Verified Validate(Identity i)
    {
        if (i.EventName == "local-unattested")
        {
            if (i.EventSha.Length != 0 || i.RunId.Length != 0)
                throw new InvalidDataException("F20 malformed local execution");
            return new(i.EventName, "local-unattested", "local-unattested",
                "local-unattested", "local-unattested");
        }

        if (!ValidSha(i.EventSha) || !ValidSha(i.CheckedOutSha) ||
            i.EventSha != i.CheckedOutSha || i.Repo != Repository ||
            !long.TryParse(i.RunId, out var id) || id <= 0 ||
            !int.TryParse(i.RunAttempt, out var attempt) || attempt <= 0)
            throw new InvalidDataException("F20 GitHub checkout/run identity mismatch");

        using var json = JsonDocument.Parse(i.EventJson);
        var ev = json.RootElement;
        if (i.EventName == "push")
        {
            string after = Field(ev, "after");
            string eventRef = Field(ev, "ref");
            if (!ValidSha(after) || after != i.EventSha ||
                i.Ref != eventRef || !i.Ref.StartsWith("refs/heads/", StringComparison.Ordinal) ||
                i.HeadRef.Length != 0)
                throw new InvalidDataException("F20 push event and checkout mismatch");
            return new("push", i.CheckedOutSha, i.EventSha, i.RunId, i.RunAttempt);
        }
        if (i.EventName == "pull_request")
        {
            var pr = ev.GetProperty("pull_request");
            var head = pr.GetProperty("head");
            var baseRef = pr.GetProperty("base");
            string prHead = Field(head, "sha");
            string branch = Field(head, "ref");
            string target = Field(baseRef, "ref");
            if (!ValidSha(prHead) || prHead == i.CheckedOutSha ||
                i.HeadRef != branch || i.HeadRef.Length == 0 ||
                i.Ref != "refs/pull/" + ev.GetProperty("number").GetInt32() + "/merge" ||
                target != "main")
                throw new InvalidDataException("F20 PR event head and synthetic merge not distinguished");
            return new("pull_request", i.CheckedOutSha, prHead, i.RunId, i.RunAttempt);
        }
        throw new InvalidDataException("F20 unsupported GitHub event");
    }

    private static Identity MutateEventHead(Identity original)
    {
        if (original.EventName == "local-unattested") return original with { EventSha = "invalid" };
        using var json = JsonDocument.Parse(original.EventJson);
        var root = System.Text.Json.Nodes.JsonNode.Parse(original.EventJson)!;
        if (original.EventName == "push") root["after"] = new string('0', 40);
        else root["pull_request"]!["head"]!["ref"] = "altered-head";
        return original with { EventJson = root.ToJsonString() };
    }

    private static string Field(JsonElement item, string key) =>
        item.GetProperty(key).GetString() ?? throw new InvalidDataException("F20 missing CI event field: " + key);

    private static bool ValidSha(string sha) =>
        sha.Length == 40 && sha.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
