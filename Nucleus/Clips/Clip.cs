using Nucleus.Clips.Bunny.Models;
using Nucleus.Data.ApexLegends.Models;

namespace Nucleus.Clips;

public record Clip(
    Guid ClipId,
    Guid OwnerId,
    Guid VideoId,
    ClipCategoryEnum CategoryEnum,
    DateTimeOffset CreatedAt,
    BunnyVideo Video,
    IReadOnlyList<string> Tags,
    bool IsViewed,
    ApexLegend DetectedLegend,
    string DetectedLegendCard)
{
}

public record PagedClipsResponse(List<Clip> Clips, long TotalClips, long TotalPages);