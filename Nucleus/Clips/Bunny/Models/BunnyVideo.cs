namespace Nucleus.Clips.Bunny.Models;

public record BunnyVideo(
    int VideoLibraryId,
    Guid Guid,
    string Title,
    DateTimeOffset DateUploaded,
    int Length,
    int Status,
    double Framerate,
    int ThumbnailCount,
    int EncodeProgress,
    long StorageSize,
    Guid CollectionId,
    string ThumbnailFileName,
    string ThumbnailBlurhash,
    string Category,
    List<object> Moments,
    List<object> MetaTags
);