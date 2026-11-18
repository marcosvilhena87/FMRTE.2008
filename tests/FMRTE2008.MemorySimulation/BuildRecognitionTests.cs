using FMRTE2008.Application;

namespace FMRTE2008.MemorySimulation;

internal static class BuildRecognitionTests
{
    internal static void Run()
    {
        var known = new ExecutableIdentity(SupportedBuildCatalog.Known802Sha256,
            "8.0.2", 52395, "x86");
        var recognized = SupportedBuildCatalog.Recognize(known);
        if (!recognized.IsRecognized || recognized.Variant != "8.0.2v1" ||
            recognized.MemoryMapApproved)
            throw new Exception("Build conhecida deve ser identificada, mas não aprovada para leitura");
        var fake = known with { Sha256 = new string('0', 64) };
        if (SupportedBuildCatalog.Recognize(fake).IsRecognized)
            throw new Exception("Hash diferente foi aceito");
        var wrongPart = known with { FilePrivatePart = 52512 };
        if (SupportedBuildCatalog.Recognize(wrongPart).IsRecognized)
            throw new Exception("Variante incorreta foi aceita");
        var wrongArchitecture = known with { Architecture = "x64" };
        if (SupportedBuildCatalog.Recognize(wrongArchitecture).IsRecognized)
            throw new Exception("Arquitetura incorreta foi aceita");
        Console.WriteLine("OK: 4 verificações de identidade da build");
    }
}
