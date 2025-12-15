namespace Nucleus.Minecraft;

public class BackupSyncBackgroundService(
    ILogger<BackupSyncBackgroundService> logger,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration)
    : BackgroundService
{
    private readonly TimeSpan _syncInterval = TimeSpan.FromHours(
        double.TryParse(configuration["Backblaze:SyncIntervalHours"], out double hours) ? hours : 1
    );

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Skip if B2 is not configured
        if (string.IsNullOrWhiteSpace(configuration["Backblaze:BucketName"]))
        {
            logger.LogInformation("Backblaze B2 not configured - backup sync service disabled");
            return;
        }

        logger.LogInformation("Backup sync service starting with interval: {Interval}", _syncInterval);

        // Wait a bit before the first sync to let the app fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        using PeriodicTimer timer = new(_syncInterval);

        await SyncBackupsAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await SyncBackupsAsync(stoppingToken);
        }
    }

    private async Task SyncBackupsAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting scheduled backup sync");

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            BackupService backupService = scope.ServiceProvider.GetRequiredService<BackupService>();

            BackupSyncResult result = await backupService.SyncBackupsAsync(stoppingToken);

            if (result.Success)
            {
                logger.LogInformation(
                    "Scheduled backup sync completed: {Uploaded} uploaded, {Skipped} skipped, {Bytes:N0} bytes",
                    result.FilesUploaded,
                    result.FilesSkipped,
                    result.BytesUploaded
                );
            }
            else
            {
                logger.LogWarning("Scheduled backup sync failed: {Message}", result.Message);
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Backup sync cancelled");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to run scheduled backup sync");
        }
    }
}
