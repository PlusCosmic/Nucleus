using Nucleus.Minecraft.Models;

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
        logger.LogInformation("Starting scheduled backup sync for all active servers");

        try
        {
            using IServiceScope scope = scopeFactory.CreateScope();
            BackupService backupService = scope.ServiceProvider.GetRequiredService<BackupService>();
            MinecraftStatements statements = scope.ServiceProvider.GetRequiredService<MinecraftStatements>();

            List<MinecraftServer> servers = await statements.GetAllActiveServersAsync();

            if (servers.Count == 0)
            {
                logger.LogInformation("No active servers found, skipping backup sync");
                return;
            }

            int totalUploaded = 0;
            int totalSkipped = 0;
            long totalBytes = 0;

            foreach (MinecraftServer server in servers)
            {
                stoppingToken.ThrowIfCancellationRequested();

                logger.LogInformation("Syncing backups for server {ServerName} ({ServerId})", server.Name, server.Id);

                BackupSyncResult result = await backupService.SyncBackupsAsync(server, stoppingToken);

                if (result.Success)
                {
                    totalUploaded += result.FilesUploaded;
                    totalSkipped += result.FilesSkipped;
                    totalBytes += result.BytesUploaded;

                    logger.LogDebug(
                        "Server {ServerName}: {Uploaded} uploaded, {Skipped} skipped",
                        server.Name, result.FilesUploaded, result.FilesSkipped);
                }
                else
                {
                    logger.LogWarning("Backup sync failed for server {ServerName}: {Message}", server.Name, result.Message);
                }
            }

            logger.LogInformation(
                "Scheduled backup sync completed for {ServerCount} servers: {Uploaded} uploaded, {Skipped} skipped, {Bytes:N0} bytes",
                servers.Count, totalUploaded, totalSkipped, totalBytes);
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
