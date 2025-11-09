using Nucleus.Data.ApexLegends;
using Nucleus.Data.ApexLegends.Models;
using Nucleus.Exceptions;

namespace Nucleus.ApexLegends;

public class MapService(ApexStatements apexStatements, IConfiguration configuration)
{
    public async Task<CurrentMapRotation> GetMapRotation()
    {
        var recentRotationRows = await apexStatements.GetRecentRotations(DateTimeOffset.UtcNow.AddDays(-2));

        DateTimeOffset now = DateTimeOffset.UtcNow;

        var currentStandardRow = recentRotationRows.Find(r =>
            r.StartTime <= now && r.EndTime > now && r.Gamemode == (int)ApexGamemode.Standard);
        if (currentStandardRow == null)
        {
            throw new ServiceUnavailableException("No current standard map rotation data available");
        }

        DateTimeOffset nextTime = currentStandardRow.EndTime.AddMinutes(1);

        var nextStandardRow = recentRotationRows.Find(r =>
            r.StartTime <= nextTime && r.EndTime > nextTime && r.Gamemode == (int)ApexGamemode.Standard);
        if (nextStandardRow == null)
        {
            throw new ServiceUnavailableException("No next standard map rotation data available");
        }

        var currentRankedRow = recentRotationRows.Find(r =>
            r.StartTime <= now && r.EndTime > now && r.Gamemode == (int)ApexGamemode.Ranked);
        if (currentRankedRow == null)
        {
            throw new ServiceUnavailableException("No current ranked map rotation data available");
        }

        DateTimeOffset nextRankedTime = currentRankedRow.EndTime.AddMinutes(1);

        var nextRankedRow = recentRotationRows.Find(r =>
            r.StartTime <= nextRankedTime && r.EndTime > nextRankedTime && r.Gamemode == (int)ApexGamemode.Ranked);
        if (nextRankedRow == null)
        {
            throw new ServiceUnavailableException("No next ranked map rotation data available");
        }

        return new CurrentMapRotation
        (
            RowToMapInfo(currentStandardRow),
            RowToMapInfo(nextStandardRow),
            RowToMapInfo(currentRankedRow),
            RowToMapInfo(nextRankedRow),
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

    private MapInfo RowToMapInfo(ApexStatements.ApexMapRotationRow row)
    {
        var map = row.GetMap();
        return new MapInfo(GetFriendlyNameForMap(map), row.StartTime, row.EndTime, GetAssetUriForMap(map));
    }
}