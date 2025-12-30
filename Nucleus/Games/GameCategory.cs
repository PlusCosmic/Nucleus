namespace Nucleus.Games;

public record GameCategory(
    Guid Id,
    long? IgdbId,
    string Name,
    string Slug,
    string? CoverUrl,
    bool IsCustom,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record CreateGameCategoryRequest(
    long? IgdbId,
    string Name,
    string Slug,
    string? CoverUrl
);

public record GameCategoryResponse(
    Guid Id,
    string Name,
    string Slug,
    string? CoverUrl,
    bool IsCustom
);

public record GameSearchResult(long IgdbId, string Name, string Slug, string? CoverUrl);

public record GameDetails(
    long IgdbId,
    string Name,
    string Slug,
    string? CoverUrl,
    List<string> Genres,
    List<string> Platforms,
    string? Summary
);

public record AddGameFromIgdbRequest(long IgdbId);

public record AddCustomCategoryRequest(string Name, string? CoverUrl);
