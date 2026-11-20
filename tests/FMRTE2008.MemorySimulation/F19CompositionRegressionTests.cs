using System.Text.RegularExpressions;

namespace FMRTE2008.MemorySimulation;

/// <summary>
/// F19 source-composition regression tripwires. Source inspection is NOT
/// a security proof: it guards these explicit known production declarations.
/// The runner never loads a game executable or opens a process.
/// </summary>
internal static class F19CompositionRegressionTests
{
    internal static void Run()
    {
        var root = LocateRepositoryRoot();
        var desktop = File.ReadAllText(Path.Combine(root,
            "src", "FMRTE2008.Desktop", "MainWindow.xaml.cs"));
        var session = File.ReadAllText(Path.Combine(root,
            "src", "FMRTE2008.Application", "VerifiedPlayerReadSession.cs"));
        var catalog = File.ReadAllText(Path.Combine(root,
            "src", "FMRTE2008.Application", "SupportedBuildCatalog.cs"));
        var map = File.ReadAllText(Path.Combine(root,
            "src", "FMRTE2008.Application", "MinimalPlayerIdentityMap.cs"));
        int checks = 0;
        void Check(bool ok, string reason)
        {
            if (!ok) throw new InvalidOperationException("F19 composition: " + reason);
            checks++;
        }

        // Match the actual field initializer, not a comment or a test fixture.
        const string demonstrationPattern =
            @"private\s+readonly\s+IncrementalSearchController\s+_search\s*=\s*new\s*\(\s*\(\s*\)\s*=>\s*new\s+DemonstrationIndexedPlayerSource\s*\(\s*\)\s*\)";
        Check(Regex.IsMatch(desktop, demonstrationPattern),
            "WPF must construct the inert demonstration source");
        Check(!Regex.IsMatch(desktop, @"new\s+VerifiedWindowsProcessOpener\s*\("),
            "WPF must not construct a real process opener");
        Check(Regex.IsMatch(session,
            @"class\s+NoApprovedPlayerMaps\s*:\s*IApprovedPlayerMapCatalog"),
            "production deny-all catalog missing");
        Check(Regex.IsMatch(session,
            @"FindApprovedAsync\s*\([^)]*\)\s*=>\s*Task\.FromResult<ApprovedPlayerMap\?>\s*\(\s*null\s*\)"),
            "production catalog must always reject an approval lookup");
        Check(Regex.IsMatch(catalog,
            @"return\s+new\s*\(\s*true\s*,\s*""8\.0\.2v1""\s*,\s*false\s*,"),
            "recognized build cannot set MemoryMapApproved");
        Check(Regex.IsMatch(map,
            @"bool\s+IsApprovedForOperationalUse\s*=>\s*false\s*;"),
            "research map cannot become approved");

        // B02: scan EVERY desktop source in this checkout, not only the
        // original field initializer. This is a bounded project-source guard.
        var allDesktopSources = Directory.GetFiles(Path.Combine(root,
            "src", "FMRTE2008.Desktop"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText).ToArray();
        ValidateDesktopRoots(allDesktopSources);
        checks++;
        // Unlike the former negative controls, call the SAME validator on
        // a deliberately introduced second composition path.
        bool deniedAlternate = false;
        try
        {
            ValidateDesktopRoots(allDesktopSources.Append(
                "class ExtraRoot { void Go() { VerifiedPlayerReadSession.OpenAsync(); } }").ToArray());
        }
        catch (InvalidOperationException) { deniedAlternate = true; }
        Check(deniedAlternate, "alternate desktop session access was not rejected");
        bool deniedFactory = false;
        try
        {
            ValidateDesktopRoots(allDesktopSources.Append(
                "class ExtraRoot { void Go() { new VerifiedWindowsProcessOpener(); } }").ToArray());
        }
        catch (InvalidOperationException) { deniedFactory = true; }
        Check(deniedFactory, "alternate desktop opener factory was not rejected");

        // Functional evidence for the currently compiled demonstration source:
        // no process, no OS handle, only static in-memory player fixtures.
        var demonstration = new FMRTE2008.Application.DemonstrationIndexedPlayerSource();
        Check(!demonstration.IsLiveProcessSource && demonstration.Count > 0,
            "compiled demonstration source became live or empty");
        Check(demonstration.ReadAtAsync(0, CancellationToken.None).GetAwaiter().GetResult().Id > 0,
            "compiled demonstration fixture returned invalid identity");

        // Mutation controls prove the tripwire rejects a changed composition.
        Check(!Regex.IsMatch(desktop.Replace(
            "new DemonstrationIndexedPlayerSource()",
            "new DifferentSource()"), demonstrationPattern),
            "mutated UI composition escaped the tripwire");
        Check(!Regex.IsMatch(session.Replace(
            "Task.FromResult<ApprovedPlayerMap?>(null)",
            "Task.FromResult<ApprovedPlayerMap?>(someMap)"),
            @"FindApprovedAsync\s*\([^)]*\)\s*=>\s*Task\.FromResult<ApprovedPlayerMap\?>\s*\(\s*null\s*\)"),
            "mutated catalog return escaped the tripwire");
        Check(!Regex.IsMatch(map.Replace(
            "IsApprovedForOperationalUse => false",
            "IsApprovedForOperationalUse => true"),
            @"bool\s+IsApprovedForOperationalUse\s*=>\s*false\s*;"),
            "mutated research approval flag escaped the tripwire");
        Console.WriteLine($"OK F19 composition regression: {checks} source tripwires and mutation negatives (not an operational approval).");
    }

    private static void ValidateDesktopRoots(string[] sources)
    {
        if (sources.Length == 0)
            throw new InvalidOperationException("F19 missing desktop sources");
        string joined = string.Join("\n", sources);
        if (Regex.IsMatch(joined,
            @"\bVerifiedPlayerReadSession\s*\.\s*OpenAsync\s*\(|\bOpenVerifiedAsync\s*\(|\bnew\s+VerifiedWindowsProcessOpener\s*\("))
            throw new InvalidOperationException("F19 unexpected desktop live session construction");
        if (Regex.Matches(joined,
            @"\bnew\s*\(\s*\(\s*\)\s*=>\s*new\s+DemonstrationIndexedPlayerSource\s*\(\s*\)\s*\)").Count != 1)
            throw new InvalidOperationException("F19 desktop demonstration root count changed");
        // This check does not cover reflection, generated binaries, or future
        // provider types: review of every source diff remains mandatory.
    }

    private static string LocateRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        for (int i = 0; i < 12 && current is not null; i++, current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName,
                "src", "FMRTE2008.Desktop", "MainWindow.xaml.cs")) &&
                File.Exists(Path.Combine(current.FullName,
                "src", "FMRTE2008.Application", "VerifiedPlayerReadSession.cs")))
                return current.FullName;
        }
        throw new DirectoryNotFoundException(
            "F19 production source unavailable; composition was NOT verified");
    }
}
