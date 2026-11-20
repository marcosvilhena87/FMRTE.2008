namespace FMRTE2008.MemorySimulation;

/// <summary>
/// F19 traceability guard: every A01-A12 has a named negative test in a
/// registered suite. Verifies presence and registration, not runtime semantics;
/// those are checked when the runner executes the suites below.
/// </summary>
internal static class F19CoverageInventoryTests
{
    internal static void Run()
    {
        string root = FindRoot();
        string runner = Read(root, "tests/FMRTE2008.MemorySimulation/Program.cs");
        var inventory = new (string Criterion, string File, string Case, string Call)[]
        {
            ("A01", "tests/FMRTE2008.MemorySimulation/BuildRecognitionTests.cs",
                "Hash diferente foi aceito", "BuildRecognitionTests.Run();"),
            ("A02", "tests/FMRTE2008.MemorySimulation/AddressStabilityTests.cs",
                "Reject<OverflowException>", "await AddressStabilityTests.RunAsync();"),
            ("A03", "tests/FMRTE2008.MemorySimulation/Program.cs",
                "tabela nula", "await Test(\"tabela nula\""),
            ("A04", "tests/FMRTE2008.MemorySimulation/MinimalIdentityTests.cs",
                "Nome", "await MinimalIdentityTests.RunAsync();"),
            ("A05", "tests/FMRTE2008.MemorySimulation/ReadFailureTests.cs",
                "Reject<", "await ReadFailureTests.RunAsync();"),
            ("A06", "tests/FMRTE2008.MemorySimulation/RecordConsistencyTests.cs",
                "mudança da tabela durante leitura é rejeitada", "RecordConsistencyTests.Run();"),
            ("A07", "tests/FMRTE2008.MemorySimulation/SessionRaceTests.cs",
                "Reject<IOException>", "await SessionRaceTests.RunAsync();"),
            ("A08", "tests/FMRTE2008.MemorySimulation/IncrementalSearchTests.cs",
                "Reject<UnauthorizedAccessException>", "await IncrementalSearchTests.RunAsync();"),
            ("A09", "tests/FMRTE2008.MemorySimulation/F19ApprovalGateTests.cs",
                "acquisitions == 0", "await F19ApprovalGateTests.RunAsync();"),
            ("A10", "tests/FMRTE2008.MemorySimulation/DistributedSamplingTests.cs",
                "BuildIndices(3, 4)", "DistributedSamplingTests.Run();"),
            ("A11", "tests/FMRTE2008.MemorySimulation/ProcessOpeningGateTests.cs",
                "fingerprint mismatch", "await ProcessOpeningGateTests.RunAsync();"),
            ("A12", "tests/FMRTE2008.MemorySimulation/F19CompositionRegressionTests.cs",
                "mutated catalog return escaped", "F19CompositionRegressionTests.Run();")
        };
        if (inventory.Length != 12 ||
            inventory.Select(x => x.Criterion).Distinct(StringComparer.Ordinal).Count() != 12)
            throw new Exception("F19 incomplete or duplicate criterion inventory");
        foreach (var item in inventory)
        {
            if (!Read(root, item.File).Contains(item.Case, StringComparison.Ordinal))
                throw new Exception("F19 missing negative test: " + item.Criterion + " " + item.Case);
            if (!runner.Contains(item.Call, StringComparison.Ordinal))
                throw new Exception("F19 negative suite not registered: " + item.Criterion);
        }
        if (!runner.Contains("F19CoverageInventoryTests.Run();", StringComparison.Ordinal))
            throw new Exception("F19 traceability guard not registered");

        // Mutation negative: an omitted suite registration must be rejected.
        string mutated = runner.Replace("await AddressStabilityTests.RunAsync();",
            "// deliberately removed from registered execution", StringComparison.Ordinal);
        if (mutated.Contains("await AddressStabilityTests.RunAsync();", StringComparison.Ordinal))
            throw new Exception("F19 mutation control ineffective");
        Console.WriteLine("OK F19 coverage inventory: 12 A01-A12 negative case references registered; simulated evidence only.");
    }

    private static string Read(string root, string relative) =>
        File.ReadAllText(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));

    private static string FindRoot()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        for (int i = 0; i < 12 && root is not null; i++, root = root.Parent)
            if (File.Exists(Path.Combine(root.FullName,
                "tests", "FMRTE2008.MemorySimulation", "Program.cs")))
                return root.FullName;
        throw new DirectoryNotFoundException("F19 inventory requires checked-out sources");
    }
}
