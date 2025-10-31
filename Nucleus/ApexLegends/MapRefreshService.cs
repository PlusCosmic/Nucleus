using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nucleus.ApexLegends.Models;
using Nucleus.Repository;

namespace Nucleus.ApexLegends;

public class MapRefreshService(
    ILogger<MapRefreshService> logger,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    HttpClient httpClient)
    : BackgroundService
{
    private readonly string _mapUrl =
        $"https://api.mozambiquehe.re/maprotation?version=2&auth={configuration["ApexLegendsApiKey"] ?? throw new InvalidOperationException("API key not configured")}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        await RefreshMapsAsync();
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshMapsAsync();
        }
    }

    private async Task RefreshMapsAsync()
    {
        logger.LogInformation("Refreshing Map Rotation");
        string mapRotation = await httpClient.GetStringAsync(_mapUrl);
        MapRotationResponse response = JsonSerializer.Deserialize<MapRotationResponse>(mapRotation) ??
                                       throw new InvalidOperationException("Failed to deserialize map rotation");
        // Check if the db knows about all the maps in the response
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<NucleusDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await UpsertMapAsync(response.BattleRoyale.Current, ApexGamemode.Standard, dbContext);
        await UpsertMapAsync(response.BattleRoyale.Next, ApexGamemode.Standard, dbContext);
        await UpsertMapAsync(response.Ranked.Current, ApexGamemode.Ranked, dbContext);
        await UpsertMapAsync(response.Ranked.Next, ApexGamemode.Ranked, dbContext);
    }

    private async Task UpsertMapAsync(MapRotationInfo mapInfo, ApexGamemode gamemode, NucleusDbContext dbContext)
    {
        ApexMapRotation? existingRotation = await dbContext.ApexMapRotations.SingleOrDefaultAsync(r => r.Gamemode == gamemode && r.StartTime == mapInfo.StartTime && r.EndTime == mapInfo.EndTime);
        if (existingRotation == null)
        {
            var mapRotation = new ApexMapRotation
            {
                Map = MapCodeToEnum(mapInfo.Code),
                StartTime = mapInfo.StartTime,
                EndTime = mapInfo.EndTime,
                Gamemode = gamemode
            };
            await dbContext.ApexMapRotations.AddAsync(mapRotation);
            await dbContext.SaveChangesAsync();
        }
    }

    private static ApexMap MapCodeToEnum(string mapCode)
    {
        switch (mapCode)
        {
            case "kings_canyon_rotation":
                return ApexMap.KingsCanyon;
            case "edistrict_rotation":
                return ApexMap.EDistrict;
            case "olympus_rotation":
                return ApexMap.Olympus;
            case "worlds_edge_rotation":
                return ApexMap.WorldsEdge;
            case "storm_point_rotation":
                return ApexMap.StormPoint;
            case "broken_moon_rotation":
                return ApexMap.BrokenMoon;
            default:
                throw new InvalidOperationException($"Unknown map code: {mapCode}");
        }
    }
}