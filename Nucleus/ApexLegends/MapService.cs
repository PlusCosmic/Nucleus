using System.Text.Json;
using Nucleus.ApexLegends.Models;

namespace Nucleus.ApexLegends;

public class MapService(HttpClient httpClient, IConfiguration configuration)
{
    private readonly string _mapUrl = $"https://api.mozambiquehe.re/maprotation?version=2&auth={configuration["ApexLegendsApiKey"] ?? throw new InvalidOperationException("API key not configured")}";
    
    public async Task<CurrentMapRotation> GetMapRotation()
    {
        string mapRotation = await httpClient.GetStringAsync(_mapUrl);
        MapRotationResponse response = JsonSerializer.Deserialize<MapRotationResponse>(mapRotation) ?? throw new InvalidOperationException("Failed to deserialize map rotation");
        return new CurrentMapRotation(
            new MapInfo(response.BattleRoyale.Current.Map, response.BattleRoyale.Current.StartTime, response.BattleRoyale.Current.EndTime, response.BattleRoyale.Current.AssetUri),
            new MapInfo(response.BattleRoyale.Next.Map, response.BattleRoyale.Next.StartTime, response.BattleRoyale.Next.EndTime, response.BattleRoyale.Next.AssetUri),
            new MapInfo(response.Ranked.Current.Map, response.Ranked.Current.StartTime, response.Ranked.Current.EndTime, response.Ranked.Current.AssetUri),
            new MapInfo(response.Ranked.Next.Map, response.Ranked.Next.StartTime, response.Ranked.Next.EndTime, response.Ranked.Next.AssetUri),
            DateTimeOffset.UtcNow
        );
    }
}