namespace FMRTE2008.Domain;

/// <summary>Identidade de build baseada em hash do executável.</summary>
public sealed record GameBuild(string Id, string Architecture, string ExecutableSha256);

/// <summary>Campo definido pelo projeto FM2008.mapping.</summary>
public sealed record FieldDefinition(string Name, int Offset, string Type, bool Writable);

/// <summary>Leitura somente leitura, implementada por adaptadores de plataforma.</summary>
public interface IProcessReader
{
    Task<byte[]> ReadAsync(nuint address, int length, CancellationToken cancellationToken);
}

/// <summary>Catálogo de mapas aprovados. Não usar mapas ilustrativos.</summary>
public interface IMappingCatalog
{
    Task<bool> HasVerifiedMappingAsync(GameBuild build, CancellationToken cancellationToken);
}
