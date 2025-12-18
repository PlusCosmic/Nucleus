namespace Nucleus.Discord;

public class GuildMemberSyncService(
    DiscordBotService discordBotService,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<GuildMemberSyncService> logger) : BackgroundService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(InitialDelay, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncGuildMembersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to sync guild members");
            }

            await Task.Delay(SyncInterval, stoppingToken);
        }
    }

    private async Task SyncGuildMembersAsync(CancellationToken stoppingToken)
    {
        if (!discordBotService.IsConnected)
        {
            logger.LogDebug("Discord bot not connected, skipping guild member sync");
            return;
        }

        var members = await discordBotService.GetAllGuildMembersAsync();

        if (members.Count == 0)
        {
            logger.LogDebug("No guild members found to sync");
            return;
        }

        using var scope = serviceScopeFactory.CreateScope();
        var discordStatements = scope.ServiceProvider.GetRequiredService<DiscordStatements>();

        var upsertedUsers = await discordStatements.BulkUpsertUsers(members);
        logger.LogInformation("Synced {Count} guild members to database", upsertedUsers.Count);
    }
}
