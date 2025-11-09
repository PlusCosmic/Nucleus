using System.Text.Json;
using Nucleus.Data.ApexLegends.Models;
using StackExchange.Redis;

namespace Nucleus.ApexLegends;

public interface IApexMapCacheService
{
    Task SetMapRotationAsync(CurrentMapRotation rotation);
    Task<CurrentMapRotation?> GetMapRotationAsync();
}

public class ApexMapCacheService(IConnectionMultiplexer redis) : IApexMapCacheService
{
    private const string CacheKey = "apex:map_rotation";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task SetMapRotationAsync(CurrentMapRotation rotation)
    {
        var json = JsonSerializer.Serialize(rotation);
        await _db.StringSetAsync(CacheKey, json, CacheTtl);
    }

    public async Task<CurrentMapRotation?> GetMapRotationAsync()
    {
        var json = await _db.StringGetAsync(CacheKey);
        return json.IsNullOrEmpty ? null : JsonSerializer.Deserialize<CurrentMapRotation>(json!);
    }
}
