using Nucleus.Clips.ApexLegends.Models;
using Nucleus.Clips.Bunny.Models;
using Nucleus.Clips.Core.Models;

namespace Nucleus.Test.Builders;

/// <summary>
///     Fluent builder for creating test Clip instances.
/// </summary>
public class ClipBuilder
{
    // Placeholder GUIDs for in-memory test objects (not database values)
    private static readonly Guid ApexLegendsPlaceholderId = new("00000001-0000-0000-0000-000000000001");
    private static readonly Guid WarzonePlaceholderId = new("00000002-0000-0000-0000-000000000002");

    private Guid _gameCategoryId = ApexLegendsPlaceholderId;
    private string _categorySlug = "apex-legends";
    private Guid _clipId = Guid.NewGuid();
    private DateTimeOffset _createdAt = DateTimeOffset.UtcNow;
    private ApexLegend _detectedLegend = ApexLegend.None;
    private string _detectedLegendCard = "/images/None_Legend_Card.webp";
    private bool _isViewed;
    private Guid _ownerId = Guid.NewGuid();
    private List<string> _tags = new();
    private BunnyVideo? _video;
    private Guid _videoId = Guid.NewGuid();

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

    public ClipBuilder WithCategory(Guid gameCategoryId, string categorySlug)
    {
        _gameCategoryId = gameCategoryId;
        _categorySlug = categorySlug;
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

    public ClipBuilder WithDetectedLegend(ApexLegend legend)
    {
        _detectedLegend = legend;
        _detectedLegendCard = legend == ApexLegend.MadMaggie
            ? "/images/Mad_Maggie_Legend_Card.webp"
            : $"/images/{legend}_Legend_Card.webp";
        return this;
    }

    public ClipBuilder WithDetectedLegendCard(string card)
    {
        _detectedLegendCard = card;
        return this;
    }

    /// <summary>
    ///     Creates a clip for the Apex Legends category with ranked tags.
    /// </summary>
    public ClipBuilder AsApexRanked()
    {
        _gameCategoryId = ApexLegendsPlaceholderId;
        _categorySlug = "apex-legends";
        _tags = new List<string> { "ranked", "apex" };
        return this;
    }

    /// <summary>
    ///     Creates a clip for the Call of Duty Warzone category.
    /// </summary>
    public ClipBuilder AsWarzone()
    {
        _gameCategoryId = WarzonePlaceholderId;
        _categorySlug = "warzone";
        _tags = new List<string> { "warzone", "cod" };
        return this;
    }

    /// <summary>
    ///     Creates a clip with many tags (useful for testing tag limits).
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
        BunnyVideo video = _video ?? new BunnyVideo(
            12345,
            _videoId,
            "Test Video",
            _createdAt,
            30,
            4,
            60.0,
            1,
            100,
            1024000,
            Guid.NewGuid(),
            "thumbnail.jpg",
            "blurhash",
            _categorySlug,
            new List<Moment>(),
            new List<MetaTag>()
        );

        object? gameMetadata = null;
        if (_categorySlug == "apex-legends" && _detectedLegend != ApexLegend.None)
        {
            gameMetadata = new ApexClipMetadata(_detectedLegend.ToString(), _detectedLegendCard);
        }

        return new Clip(
            _clipId,
            _ownerId,
            _videoId,
            _gameCategoryId,
            _categorySlug,
            _createdAt,
            video,
            _tags,
            _isViewed,
            gameMetadata
        );
    }

    /// <summary>
    ///     Creates a default test clip with common values.
    /// </summary>
    public static Clip CreateDefault()
    {
        return new ClipBuilder().Build();
    }

    /// <summary>
    ///     Creates multiple clips with sequential titles.
    /// </summary>
    public static IEnumerable<Clip> CreateMany(int count, Guid? ownerId = null)
    {
        Guid actualOwnerId = ownerId ?? Guid.NewGuid();
        for (int i = 0; i < count; i++)
        {
            yield return new ClipBuilder()
                .WithOwnerId(actualOwnerId)
                .WithCreatedAt(DateTimeOffset.UtcNow.AddMinutes(-i))
                .Build();
        }
    }
}