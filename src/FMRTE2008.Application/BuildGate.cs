using FMRTE2008.Domain;

namespace FMRTE2008.Application;

/// <summary>Bloqueia builds sem mapeamento aprovado; nunca presume compatibilidade.</summary>
public sealed class BuildGate(IMappingCatalog catalog)
{
    public async Task<bool> CanReadAsync(GameBuild build, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(build.Id) ||
            build.Architecture != "x86" ||
            build.ExecutableSha256.Length != 64 ||
            !build.ExecutableSha256.All(Uri.IsHexDigit))
            return false;

        return await catalog.HasVerifiedMappingAsync(build, cancellationToken);
    }
}
