using Microsoft.Extensions.Configuration;
using Nucleus.Clips.Bunny;
using Nucleus.Clips.Bunny.Models;

namespace Nucleus.Test.TestFixtures;

/// <summary>
/// Mock implementation of BunnyService for testing.
/// Returns fake data without making real HTTP calls to Bunny CDN.
/// </summary>
public class MockBunnyService : BunnyService
{
    private readonly Dictionary<Guid, BunnyCollection> _collections = new();
    private readonly Dictionary<Guid, BunnyVideo> _videos = new();

    public MockBunnyService() : base(new HttpClient(), CreateMockConfiguration())
    {
    }

    private static IConfiguration CreateMockConfiguration()
    {
        var config = new Dictionary<string, string>
        {
            ["BunnyLibraryId"] = "test-library-12345",
            ["BunnyAccessKey"] = "test-access-key-67890",
            ["ASPNETCORE_ENVIRONMENT"] = "Testing"
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(config!)
            .Build();
    }

    public new async Task<BunnyCollection> CreateCollectionAsync(string categorySlug, Guid userId)
    {
        await Task.CompletedTask;

        var collection = new BunnyCollection(
            VideoLibraryId: 12345,
            Guid: Guid.NewGuid(),
            Name: $"Testing-{categorySlug}-{userId}",
            VideoCount: 0,
            TotalSize: 0,
            PreviewVideoIds: "",
            PreviewImageUrls: Array.Empty<string>()
        );

        _collections[collection.Guid] = collection;
        return collection;
    }

    public new async Task<PagedVideoResponse> GetVideosForCollectionAsync(Guid collectionId, int page, int pageSize)
    {
        await Task.CompletedTask;

        var videos = _videos.Values
            .Where(v => v.CollectionId == collectionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var totalVideos = _videos.Values.Count(v => v.CollectionId == collectionId);

        return new PagedVideoResponse(
            TotalItems: totalVideos,
            CurrentPage: page,
            ItemsPerPage: pageSize,
            Items: videos
        );
    }

    public new async Task<BunnyVideo> CreateVideoAsync(Guid collectionId, string videoTitle)
    {
        await Task.CompletedTask;

        var video = new BunnyVideo(
            VideoLibraryId: 12345,
            Guid: Guid.NewGuid(),
            Title: videoTitle,
            DateUploaded: DateTimeOffset.UtcNow,
            Length: 0,
            Status: 4, // Finished
            Framerate: 30.0,
            ThumbnailCount: 0,
            EncodeProgress: 100,
            StorageSize: 1024,
            CollectionId: collectionId,
            ThumbnailFileName: "",
            ThumbnailBlurhash: "",
            Category: "",
            Moments: new List<Moment>(),
            MetaTags: new List<MetaTag>()
        );

        _videos[video.Guid] = video;
        return video;
    }

    public new async Task<BunnyVideo?> GetVideoByIdAsync(Guid videoId)
    {
        await Task.CompletedTask;
        return _videos.TryGetValue(videoId, out var video) ? video : null;
    }

    public new async Task UpdateVideoTitleAsync(Guid videoId, string newTitle)
    {
        await Task.CompletedTask;

        if (_videos.TryGetValue(videoId, out var video))
        {
            _videos[videoId] = video with { Title = newTitle };
        }
    }

    public new async Task DeleteVideoAsync(Guid videoId)
    {
        await Task.CompletedTask;
        _videos.Remove(videoId);
    }
}
