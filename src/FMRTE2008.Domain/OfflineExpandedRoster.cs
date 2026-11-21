namespace FMRTE2008.Domain;

/// <summary>F27: conceptual labels for invented attributes, not historical memory mappings.</summary>
public enum OfflineAttributeCategory { Technical, Mental, Physical }

public sealed record OfflineCategorizedAttribute
{
    public OfflineAttribute Attribute { get; }
    public OfflineAttributeCategory Category { get; }

    public OfflineCategorizedAttribute(OfflineAttribute attribute, OfflineAttributeCategory category)
    {
        ArgumentNullException.ThrowIfNull(attribute);
        if (!Enum.IsDefined(category)) throw new ArgumentOutOfRangeException(nameof(category));
        Attribute = attribute;
        Category = category;
    }
}

public sealed record OfflineClub
{
    public string DemonstrationId { get; }
    public string Name { get; }
    public string City { get; }
    public string Country { get; }

    public OfflineClub(string demonstrationId, string name, string city, string country)
    {
        if (demonstrationId is null || demonstrationId.Length != 8 ||
            !demonstrationId.StartsWith("CLUB-", StringComparison.Ordinal) ||
            demonstrationId[5] is < '0' or > '9' ||
            demonstrationId[6] is < '0' or > '9' ||
            demonstrationId[7] is < '0' or > '9')
            throw new ArgumentException("Identificador de clube deve seguir CLUB-NNN.", nameof(demonstrationId));
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 ||
            string.IsNullOrWhiteSpace(city) || city.Length > 100 ||
            string.IsNullOrWhiteSpace(country) || country.Length > 100)
            throw new ArgumentException("Dados do clube fictício incompletos.");
        DemonstrationId = demonstrationId;
        Name = name.Trim();
        City = city.Trim();
        Country = country.Trim();
    }
}

public sealed class OfflineExpandedPlayer
{
    public OfflinePlayerProfile Profile { get; }
    public OfflineClub Club { get; }
    public IReadOnlyList<OfflineCategorizedAttribute> CategorizedAttributes { get; }
    public bool IsSynthetic => true;

    public OfflineExpandedPlayer(OfflinePlayerProfile profile, OfflineClub club,
        IEnumerable<OfflineCategorizedAttribute> categorizedAttributes)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(club);
        ArgumentNullException.ThrowIfNull(categorizedAttributes);
        if (!string.Equals(profile.Contract.Club, club.Name, StringComparison.Ordinal))
            throw new ArgumentException("Clube não corresponde ao contrato fictício.", nameof(club));
        var entries = categorizedAttributes.ToArray();
        if (entries.Length != profile.Attributes.Count ||
            entries.Any(a => a is null) ||
            entries.Select(a => a.Attribute.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != entries.Length ||
            entries.Any(a => !profile.Attributes.Any(original => original == a.Attribute)))
            throw new ArgumentException("Classificação divergente dos atributos fictícios.", nameof(categorizedAttributes));
        Profile = profile;
        Club = club;
        CategorizedAttributes = Array.AsReadOnly(entries);
    }

    public IReadOnlyList<OfflineAttribute> AttributesFor(OfflineAttributeCategory category) =>
        Array.AsReadOnly(CategorizedAttributes.Where(a => a.Category == category).Select(a => a.Attribute).ToArray());
}
