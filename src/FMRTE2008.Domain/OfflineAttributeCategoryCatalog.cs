namespace FMRTE2008.Domain;

/// <summary>
/// Closed, synthetic F27 name-to-category catalog. Names are demonstration labels,
/// not CLI symbols, offsets, game memory data, or historical taxonomy.
/// </summary>
public static class OfflineAttributeCategoryCatalog
{
    public static OfflineAttributeCategory Resolve(string name) => name switch
    {
        "Passe" => OfflineAttributeCategory.Technical,
        "Técnica" => OfflineAttributeCategory.Technical,
        "Marcação" => OfflineAttributeCategory.Technical,
        "Desarme" => OfflineAttributeCategory.Technical,
        "Finalização" => OfflineAttributeCategory.Technical,
        "Visão" => OfflineAttributeCategory.Mental,
        "Resistência" => OfflineAttributeCategory.Physical,
        "Força" => OfflineAttributeCategory.Physical,
        "Aceleração" => OfflineAttributeCategory.Physical,
        _ => throw new ArgumentException("Atributo não pertence ao catálogo fictício F27.", nameof(name))
    };

    public static IReadOnlyList<OfflineCategorizedAttribute> Categorize(
        IEnumerable<OfflineAttribute> attributes)
    {
        ArgumentNullException.ThrowIfNull(attributes);
        var copy = attributes.ToArray();
        if (copy.Length is < 1 or > 40 || copy.Any(a => a is null) ||
            copy.Select(a => a.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != copy.Length)
            throw new ArgumentException("Coleção de atributos fictícios inválida.", nameof(attributes));
        return Array.AsReadOnly(copy.Select(a =>
            new OfflineCategorizedAttribute(a, Resolve(a.Name))).ToArray());
    }
}
