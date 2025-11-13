using System.Text.Json.Serialization;

namespace Nucleus.Apex.BunnyVideo;

public record VideoProgressUpdate(
    [property: JsonPropertyName("VideoLibraryId")] int VideoLibraryId,
    [property: JsonPropertyName("VideoGuid")] Guid VideoGuid,
    [property: JsonPropertyName("Status")] int Status)
{
}