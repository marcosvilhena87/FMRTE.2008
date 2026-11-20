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
