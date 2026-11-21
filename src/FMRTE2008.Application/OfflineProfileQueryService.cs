using FMRTE2008.Domain;

namespace FMRTE2008.Application;

/// <summary>F26: never accepts a live process source, PID or runtime mapping.</summary>
public interface IOfflinePlayerProfileSource
{
    bool IsLiveProcessSource { get; }
    IReadOnlyList<OfflinePlayerProfile> Snapshot();
}

public sealed class DemonstrationProfileSource : IOfflinePlayerProfileSource
{
    public bool IsLiveProcessSource => false;

    public IReadOnlyList<OfflinePlayerProfile> Snapshot() => Array.AsReadOnly(new[]
    {
        new OfflinePlayerProfile("DEMO-001", "Alex Exemplo", "Meio-campo",
            new[] { new OfflineAttribute("Passe", 15), new OfflineAttribute("Visão", 14),
                    new OfflineAttribute("Resistência", 12), new OfflineAttribute("Técnica", 16) },
            new OfflineContract("Clube Exemplo A", new DateOnly(2007,7,1),
                new DateOnly(2010,6,30), 2500m)),
        new OfflinePlayerProfile("DEMO-002", "Bruno Fictício", "Defesa",
            new[] { new OfflineAttribute("Marcação", 16), new OfflineAttribute("Desarme", 15),
                    new OfflineAttribute("Força", 14), new OfflineAttribute("Passe", 10) },
            new OfflineContract("Clube Exemplo B", new DateOnly(2008,1,1),
                new DateOnly(2011,12,31), 1700m)),
        new OfflinePlayerProfile("DEMO-003", "Caio Demonstração", "Atacante",
            new[] { new OfflineAttribute("Finalização", 17), new OfflineAttribute("Aceleração", 14),
                    new OfflineAttribute("Técnica", 13), new OfflineAttribute("Passe", 11) },
            new OfflineContract("Clube Exemplo C", new DateOnly(2008,7,1),
                new DateOnly(2012,6,30), 3000m))
    });
}

public sealed record OfflineProfilePage(IReadOnlyList<OfflinePlayerProfile> Items,
    int TotalMatches, int PageNumber, int PageCount);

/// <summary>Queries immutable synthetic snapshots only; fail-closed if live data is supplied.</summary>
public sealed class OfflineProfileQueryService
{
    private readonly IReadOnlyList<OfflinePlayerProfile> _players;

    public OfflineProfileQueryService(IOfflinePlayerProfileSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.IsLiveProcessSource)
            throw new UnauthorizedAccessException("F26 não aceita processos reais.");
        var snapshot = source.Snapshot() ?? throw new ArgumentException("Fonte sem snapshot.", nameof(source));
        if (snapshot.Count > 1000 || snapshot.Any(p => p is null || !p.IsSynthetic))
            throw new ArgumentException("Snapshot sintético inválido.", nameof(source));
        var copy = snapshot.ToArray();
        if (copy.Select(p => p.DemonstrationId).Distinct(StringComparer.Ordinal).Count() != copy.Length)
            throw new ArgumentException("IDs fictícios duplicados.", nameof(source));
        _players = Array.AsReadOnly(copy);
    }

    public OfflineProfilePage Search(string? query, int page = 1, int pageSize = 10)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page));
        if (pageSize is < 1 or > 50) throw new ArgumentOutOfRangeException(nameof(pageSize));
        query = (query ?? "").Trim();
        if (query.Length > 120) throw new ArgumentOutOfRangeException(nameof(query));
        var filtered = _players.Where(p =>
            p.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            p.Position.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            p.DemonstrationId.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        var pages = Math.Max(1, (filtered.Length + pageSize - 1) / pageSize);
        var effectivePage = Math.Min(page, pages);
        return new OfflineProfilePage(Array.AsReadOnly(filtered.Skip((effectivePage - 1) * pageSize).Take(pageSize).ToArray()),
            filtered.Length, effectivePage, pages);
    }
}
