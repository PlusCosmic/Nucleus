using Nucleus.Clips;
using Nucleus.Clips.Bunny.Models;

namespace Nucleus.Test.Builders;

/// <summary>
/// Fluent builder for creating test Clip instances.
/// </summary>
public class ClipBuilder
{
    private Guid _clipId = Guid.NewGuid();
    private Guid _ownerId = Guid.NewGuid();
    private Guid _videoId = Guid.NewGuid();
    private ClipCategoryEnum _categoryEnum = ClipCategoryEnum.ApexLegends;
    private DateTimeOffset _createdAt = DateTimeOffset.UtcNow;
    private BunnyVideo? _video = null;
    private List<string> _tags = new();
    private bool _isViewed = false;

    public ClipBuilder WithClipId(Guid clipId)
    {
        _clipId = clipId;
        return this;
    }

    public ClipBuilder WithOwnerId(Guid ownerId)
    {
        _ownerId = ownerId;
        return this;
    }

    public ClipBuilder WithVideoId(Guid videoId)
    {
        _videoId = videoId;
        return this;
    }

    public ClipBuilder WithCategory(ClipCategoryEnum category)
    {
        _categoryEnum = category;
        return this;
    }

    public ClipBuilder WithCreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public ClipBuilder WithVideo(BunnyVideo video)
    {
        _video = video;
        return this;
    }

    public ClipBuilder WithTag(string tag)
    {
        _tags.Add(tag);
        return this;
    }

    public ClipBuilder WithTags(params string[] tags)
    {
        _tags.AddRange(tags);
        return this;
    }

    public ClipBuilder WithTags(IEnumerable<string> tags)
    {
        _tags.AddRange(tags);
        return this;
    }

    public ClipBuilder AsViewed(bool isViewed = true)
    {
        _isViewed = isViewed;
        return this;
    }

    /// <summary>
    /// Creates a clip for the Apex Legends category with ranked tags.
    /// </summary>
    public ClipBuilder AsApexRanked()
    {
        _categoryEnum = ClipCategoryEnum.ApexLegends;
        _tags = new List<string> { "ranked", "apex" };
        return this;
    }

    /// <summary>
    /// Creates a clip for the Call of Duty Warzone category.
    /// </summary>
    public ClipBuilder AsWarzone()
    {
        _categoryEnum = ClipCategoryEnum.CallOfDutyWarzone;
        _tags = new List<string> { "warzone", "cod" };
        return this;
    }

    /// <summary>
    /// Creates a clip with many tags (useful for testing tag limits).
    /// </summary>
    public ClipBuilder WithManyTags(int count = 6)
    {
        _tags.Clear();
        for (int i = 0; i < count; i++)
        {
            _tags.Add($"tag{i + 1}");
        }
        return this;
    }

    public Clip Build()
    {
        var video = _video ?? new BunnyVideo(
            VideoLibraryId: 12345,
            Guid: _videoId,
            Title: "Test Video",
            DateUploaded: _createdAt,
            Length: 30,
            Status: 4,
            Framerate: 60.0,
            ThumbnailCount: 1,
            EncodeProgress: 100,
            StorageSize: 1024000,
            CollectionId: Guid.NewGuid(),
            ThumbnailFileName: "thumbnail.jpg",
            ThumbnailBlurhash: "blurhash",
            Category: _categoryEnum.ToString(),
            Moments: new List<Moment>(),
            MetaTags: new List<MetaTag>()
        );

        return new Clip(
            ClipId: _clipId,
            OwnerId: _ownerId,
            VideoId: _videoId,
            CategoryEnum: _categoryEnum,
            CreatedAt: _createdAt,
            Video: video,
            Tags: _tags,
            IsViewed: _isViewed
        );
    }

    /// <summary>
    /// Creates a default test clip with common values.
    /// </summary>
    public static Clip CreateDefault() => new ClipBuilder().Build();

    /// <summary>
    /// Creates multiple clips with sequential titles.
    /// </summary>
    public static IEnumerable<Clip> CreateMany(int count, Guid? ownerId = null)
    {
        var actualOwnerId = ownerId ?? Guid.NewGuid();
        for (int i = 0; i < count; i++)
        {
            yield return new ClipBuilder()
                .WithOwnerId(actualOwnerId)
                .WithCreatedAt(DateTimeOffset.UtcNow.AddMinutes(-i))
                .Build();
        }
    }
}
