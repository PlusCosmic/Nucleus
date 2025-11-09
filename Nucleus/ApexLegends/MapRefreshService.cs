using Nucleus.ApexLegends;
using Nucleus.ApexLegends.Models;

namespace Nucleus.Apex;

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
        using PeriodicTimer timer = new(TimeSpan.FromMinutes(5));
        await RefreshMapsAsync();
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RefreshMapsAsync();
        }
    }

    private async Task RefreshMapsAsync()
    {
        logger.LogInformation("Refreshing Map Rotation");

        try
        {
            HttpResponseMessage mapRotation = await httpClient.GetAsync(_mapUrl);
            if (!mapRotation.IsSuccessStatusCode)
            {
                logger.LogError("Failed to refresh map rotation: HTTP {StatusCode}", mapRotation.StatusCode);
                return;
            }

            MapRotationResponse? response = await mapRotation.Content.ReadFromJsonAsync<MapRotationResponse>();
            if (response == null)
            {
                logger.LogError("Failed to deserialize map rotation response");
                return;
            }

            using IServiceScope scope = scopeFactory.CreateScope();
            MapService mapService = scope.ServiceProvider.GetRequiredService<MapService>();
            IApexMapCacheService cacheService = scope.ServiceProvider.GetRequiredService<IApexMapCacheService>();

            CurrentMapRotation processedRotation = mapService.ProcessApiResponse(response);
            await cacheService.SetMapRotationAsync(processedRotation);

            logger.LogInformation("Successfully cached map rotation data");
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to refresh and cache map rotation");
        }
    }
}