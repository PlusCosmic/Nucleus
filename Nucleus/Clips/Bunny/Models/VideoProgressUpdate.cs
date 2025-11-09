namespace Nucleus.Apex.BunnyVideo;

public record VideoProgressUpdate(int VideoLibraryId, Guid VideoGuid, int Status)
{
}