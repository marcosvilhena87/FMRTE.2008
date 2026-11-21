namespace FMRTE2008.Domain;

/// <summary>F26: entirely synthetic, immutable demonstration models. No memory offsets.</summary>
public sealed record OfflineAttribute
{
    public string Name { get; }
    public int Value { get; }

    public OfflineAttribute(string name, int value)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 60)
            throw new ArgumentException("Nome de atributo inválido.", nameof(name));
        if (value is < 1 or > 20)
            throw new ArgumentOutOfRangeException(nameof(value), "Escala demonstrativa: 1–20.");
        Name = name.Trim();
        Value = value;
    }
}

public sealed record OfflineContract
{
    public string Club { get; }
    public DateOnly StartsOn { get; }
    public DateOnly EndsOn { get; }
    public decimal WeeklyWage { get; }

    public OfflineContract(string club, DateOnly startsOn, DateOnly endsOn, decimal weeklyWage)
    {
        if (string.IsNullOrWhiteSpace(club) || club.Length > 100)
            throw new ArgumentException("Clube inválido.", nameof(club));
        if (endsOn < startsOn)
            throw new ArgumentException("Contrato termina antes de começar.", nameof(endsOn));
        if (weeklyWage < 0 || weeklyWage > 1_000_000m)
            throw new ArgumentOutOfRangeException(nameof(weeklyWage));
        Club = club.Trim();
        StartsOn = startsOn;
        EndsOn = endsOn;
        WeeklyWage = weeklyWage;
    }
}

public sealed class OfflinePlayerProfile
{
    public string DemonstrationId { get; }
    public string DisplayName { get; }
    public string Position { get; }
    public IReadOnlyList<OfflineAttribute> Attributes { get; }
    public OfflineContract Contract { get; }
    public bool IsSynthetic => true;

    public OfflinePlayerProfile(string demonstrationId, string displayName, string position,
        IEnumerable<OfflineAttribute> attributes, OfflineContract contract)
    {
        if (string.IsNullOrWhiteSpace(demonstrationId) || !demonstrationId.StartsWith("DEMO-", StringComparison.Ordinal))
            throw new ArgumentException("Identidade de demonstração obrigatória.", nameof(demonstrationId));
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 120)
            throw new ArgumentException("Nome inválido.", nameof(displayName));
        if (string.IsNullOrWhiteSpace(position) || position.Length > 40)
            throw new ArgumentException("Posição inválida.", nameof(position));
        ArgumentNullException.ThrowIfNull(attributes);
        ArgumentNullException.ThrowIfNull(contract);
        var copy = attributes.ToArray();
        if (copy.Length is < 1 or > 40 || copy.Any(x => x is null) ||
            copy.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != copy.Length)
            throw new ArgumentException("Lista de atributos inválida ou duplicada.", nameof(attributes));
        DemonstrationId = demonstrationId;
        DisplayName = displayName.Trim();
        Position = position.Trim();
        Attributes = Array.AsReadOnly(copy);
        Contract = contract;
    }
}
