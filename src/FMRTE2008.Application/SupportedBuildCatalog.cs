namespace FMRTE2008.Application;

/// <summary>Build identity is NOT a validated memory mapping.</summary>
public sealed record ExecutableIdentity(
    string Sha256, string FileVersion, int FilePrivatePart, string Architecture);

public sealed record BuildRecognition(
    bool IsRecognized, string Variant, bool MemoryMapApproved, string Reason);

public static class SupportedBuildCatalog
{
    // Obtido por inspeção estática do fm.exe fornecido; aprovação de offsets ainda pendente.
    public const string Known802Sha256 =
        "157f484be2ead4ab27006d1002c2c666a7228197b88b14c8d026e76f31b1d62d";

    public static BuildRecognition Recognize(ExecutableIdentity identity)
    {
        if (identity.Architecture != "x86" ||
            identity.FileVersion != "8.0.2" ||
            identity.FilePrivatePart != 52395 ||
            !string.Equals(identity.Sha256, Known802Sha256, StringComparison.OrdinalIgnoreCase))
            return new(false, "unknown", false,
                "Executável diferente da build 8.0.2 / 52395 identificada.");

        return new(true, "8.0.2v1", false,
            "Build identificada; nenhum mapa de memória foi validado ou habilitado.");
    }
}
