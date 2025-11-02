using Nucleus.Clips.Bunny.Models;

namespace Nucleus.Clips;

public record Clip(Guid ClipId, Guid OwnerId, Guid VideoId, ClipCategoryEnum CategoryEnum, BunnyVideo Video, IReadOnlyList<string> Tags, bool IsViewed)
{
}

public record PagedClipsResponse(List<Clip> Clips, long TotalPages);