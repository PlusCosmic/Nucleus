using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nucleus.ApexLegends.Models;
using Nucleus.Repository;

namespace Nucleus.ApexLegends;

public class MapService(NucleusDbContext dbContext, IConfiguration configuration)
{
    private readonly string _mapUrl =
        $"https://api.mozambiquehe.re/maprotation?version=2&auth={configuration["ApexLegendsApiKey"] ?? throw new InvalidOperationException("API key not configured")}";

    public async Task<CurrentMapRotation> GetMapRotation()
    {
        List<ApexMapRotation> recentRotations = await dbContext.ApexMapRotations
            .Where(r => r.StartTime >= DateTimeOffset.UtcNow.AddDays(-2)).ToListAsync();

        DateTimeOffset now = DateTimeOffset.UtcNow;

        ApexMapRotation? currentStandard = recentRotations.Find(r =>
            r.StartTime <= now && r.EndTime > now && r.Gamemode == ApexGamemode.Standard);
        if (currentStandard == null)
        {
            throw new InvalidOperationException("No current standard map rotation found");
        }

        DateTimeOffset nextTime = currentStandard.EndTime.AddMinutes(1);

        ApexMapRotation? nextStandard = recentRotations.Find(r =>
            r.StartTime <= nextTime && r.EndTime > nextTime && r.Gamemode == ApexGamemode.Standard);
        if (nextStandard == null)
        {
            throw new InvalidOperationException("No next standard map rotation found");
        }

        ApexMapRotation? currentRanked =
            recentRotations.Find(r => r.StartTime <= now && r.EndTime > now && r.Gamemode == ApexGamemode.Ranked);
        if (currentRanked == null)
        {
            throw new InvalidOperationException("No current ranked map rotation found");
        }

        DateTimeOffset nextRankedTime = currentRanked.EndTime.AddMinutes(1);

        ApexMapRotation? nextRanked = recentRotations.Find(r =>
            r.StartTime <= nextRankedTime && r.EndTime > nextRankedTime && r.Gamemode == ApexGamemode.Ranked);
        if (nextRanked == null)
        {
            throw new InvalidOperationException("No next ranked map rotation found");
        }

        return new CurrentMapRotation
        (
            ApexMapRotationToMapInfo(currentStandard),
            ApexMapRotationToMapInfo(nextStandard),
            ApexMapRotationToMapInfo(currentRanked),
            ApexMapRotationToMapInfo(nextRanked),
            now);
    }

    private string GetFriendlyNameForMap(ApexMap map)
    {
        return map switch
        {
            ApexMap.KingsCanyon => "Kings Canyon",
            ApexMap.EDistrict => "E-District",
            ApexMap.Olympus => "Olympus",
            ApexMap.StormPoint => "Storm Point",
            ApexMap.BrokenMoon => "Broken Moon",
            ApexMap.WorldsEdge => "Worlds Edge",
            _ => throw new InvalidOperationException($"Unknown map: {map}")
        };
    }

    private Uri GetAssetUriForMap(ApexMap map)
    {
        string? start = configuration["BackendAddress"];
        if (start == null)
            throw new InvalidOperationException("Backend address not configured");
        var filename = map switch
        {
            ApexMap.KingsCanyon => "kings-canyon.avif",
            ApexMap.WorldsEdge => "worlds-edge.avif",
            ApexMap.Olympus => "olympus.avif",
            ApexMap.StormPoint => "storm-point.avif",
            ApexMap.BrokenMoon => "broken-moon.avif",
            ApexMap.EDistrict => "e-district.avif",
            _ => throw new ArgumentOutOfRangeException(nameof(map), map, null)
        };
        return new Uri($"{start}/images/{filename}");
    }

    private MapInfo ApexMapRotationToMapInfo(ApexMapRotation rotation)
    {
        return new MapInfo(GetFriendlyNameForMap(rotation.Map), rotation.StartTime, rotation.EndTime,
            GetAssetUriForMap(rotation.Map));
    }
}