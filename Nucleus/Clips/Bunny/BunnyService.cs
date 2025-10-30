using System.Text.Json;
using Nucleus.Clips.Bunny.Models;

namespace Nucleus.Clips.Bunny;

public class BunnyService
{
    private readonly HttpClient _httpClient;

    private readonly string _collectionsUrl;
    
    private readonly string _videosUrl;

    public BunnyService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        var baseUrl = $"https://video.bunnycdn.com/library/{configuration["BunnyLibraryId"] ?? throw new InvalidOperationException("Bunny API library ID not configured")}";
        _collectionsUrl = baseUrl + "/collections";
        _videosUrl = baseUrl + "/videos";
        
        _httpClient.DefaultRequestHeaders.Add("AccessKey", configuration["BunnyAccessKey"] ?? throw new InvalidOperationException("Bunny access key not configured"));
    }
    
    public async Task<BunnyCollection> CreateCollectionAsync(ClipCategoryEnum categoryEnum, Guid userId)
    {
       var collectionResponse = await _httpClient.PostAsync(_collectionsUrl, new StringContent($"{{\"name\":\"{userId.ToString() + categoryEnum}\"}}"));
       return JsonSerializer.Deserialize<BunnyCollection>(collectionResponse.Content.ToString() ?? throw new InvalidOperationException("Failed to deserialize bunny collection")) ?? throw new InvalidOperationException("Failed to deserialize bunny collection");
    }

    public async Task<List<BunnyVideo>> GetVideosForCollectionAsync(Guid collectionId, int page)
    {
        string url = _videosUrl + $"?collection={collectionId}&page={page}";
        var videosResponse = await _httpClient.GetAsync(url);
        var pagedResponse = JsonSerializer.Deserialize<PagedVideoResponse>(videosResponse.Content.ToString() ?? throw new InvalidOperationException("Failed to deserialize bunny videos")) ?? throw new InvalidOperationException("Failed to deserialize bunny videos");
        return pagedResponse.Items;
    }

    public async Task<BunnyVideo> CreateVideoAsync(Guid collectionId, string videoTitle)
    {
        string url = _videosUrl;
        
        CreateVideoRequest request = new CreateVideoRequest(videoTitle, collectionId.ToString(), 0);
        string jsonRequest = JsonSerializer.Serialize(request);
        var videoResponse = await _httpClient.PostAsync(url, new StringContent(jsonRequest));
        return JsonSerializer.Deserialize<BunnyVideo>(videoResponse.Content.ToString() ?? throw new InvalidOperationException("Failed to deserialize bunny video")) ?? throw new InvalidOperationException("Failed to deserialize bunny video");
    }
}