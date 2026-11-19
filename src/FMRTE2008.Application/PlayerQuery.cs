using System.Globalization;

namespace FMRTE2008.Application;

/// <summary>
/// Fonte de dados para a tela. Implementação distribuída: fixture inerte.
/// Não há fonte conectada ao processo do jogo nesta versão.
/// </summary>
public interface IPlayerIdentitySource
{
    IReadOnlyList<MinimalPlayerIdentity> ReadSnapshot();
    bool IsLiveProcessSource { get; }
}

public sealed class DemonstrationPlayerSource : IPlayerIdentitySource
{
    private static readonly MinimalPlayerIdentity[] Players =
    [
        new(0, 0, 20, "Ivica", "Dragutinovic", null),
        new(1, 0, 21, "Nivaldo", "Lima Vieira", "Nivaldo"),
        new(2, 0, 45, "Paolo", "Maldini", null),
        new(3, 0, 54, "Edgar", "Davids", null),
        new(4, 0, 66, "Massimo", "Ambrosini", null),
        new(5, 0, 107, "Alexander", "Östlund", null)
    ];

    public IReadOnlyList<MinimalPlayerIdentity> ReadSnapshot() => Players;
    public bool IsLiveProcessSource => false;
}

public sealed record PlayerSearchPage(
    IReadOnlyList<MinimalPlayerIdentity> Items,
    int TotalMatches, int PageNumber, int PageCount, int PageSize);

/// <summary>Pesquisa e paginação inertes, sem operações de memória.</summary>
public sealed class PlayerQueryService
{
    private readonly IPlayerIdentitySource _source;
    private readonly IReadOnlyList<MinimalPlayerIdentity> _snapshot;

    public PlayerQueryService(IPlayerIdentitySource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.IsLiveProcessSource)
            throw new UnauthorizedAccessException("Fonte de processo real bloqueada até aprovação formal do mapa");
        _source = source;
        _snapshot = source.ReadSnapshot().ToArray();
    }

    public bool IsLiveProcessSource => _source.IsLiveProcessSource;

    public PlayerSearchPage Search(string? query, int pageNumber, int pageSize = 3)
    {
        if (pageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        if (pageNumber < 1)
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        string normalized = (query ?? "").Trim();
        if (normalized.Length > 128)
            throw new ArgumentOutOfRangeException(nameof(query));
        IEnumerable<MinimalPlayerIdentity> matches = _snapshot;
        if (normalized.Length > 0)
        {
            bool numeric = int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out int id);
            matches = matches.Where(p =>
                (numeric && p.Id == id) ||
                Contains(p.FirstName, normalized) ||
                Contains(p.LastName, normalized) ||
                Contains(p.CommonName, normalized) ||
                Contains(p.FirstName + " " + p.LastName, normalized));
        }
        var filtered = matches.ToArray();
        int pages = Math.Max(1, (int)Math.Ceiling(filtered.Length / (double)pageSize));
        int selectedPage = Math.Min(pageNumber, pages);
        var result = filtered.Skip((selectedPage - 1) * pageSize).Take(pageSize).ToArray();
        return new PlayerSearchPage(result, filtered.Length, selectedPage, pages, pageSize);
    }

    private static bool Contains(string? value, string query) =>
        value is not null && CultureInfo.InvariantCulture.CompareInfo.IndexOf(
            value, query, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;
}
