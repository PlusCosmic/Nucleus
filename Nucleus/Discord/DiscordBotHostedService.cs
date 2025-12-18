using Discord;
using Discord.WebSocket;

namespace Nucleus.Discord;

public class DiscordBotHostedService(
    DiscordSocketClient discordClient,
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<DiscordBotHostedService> logger) : IHostedService
{
    private readonly string? _botToken = configuration["DiscordBotToken"];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_botToken))
        {
            logger.LogWarning("Discord bot token not configured. Discord notifications will not be sent.");
            return;
        }

        discordClient.Log += LogAsync;
        discordClient.Ready += OnReadyAsync;
        discordClient.GuildMemberUpdated += OnGuildMemberUpdatedAsync;

        try
        {
            await discordClient.LoginAsync(TokenType.Bot, _botToken);
            await discordClient.StartAsync();
            logger.LogInformation("Discord bot started successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start Discord bot");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (discordClient.ConnectionState == ConnectionState.Connected)
        {
            await discordClient.LogoutAsync();
            await discordClient.StopAsync();
            logger.LogInformation("Discord bot stopped");
        }
    }

    private Task LogAsync(LogMessage log)
    {
        var logLevel = log.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Debug,
            LogSeverity.Debug => LogLevel.Trace,
            _ => LogLevel.Information
        };

        logger.Log(logLevel, log.Exception, "[Discord] {Message}", log.Message);
        return Task.CompletedTask;
    }

    private Task OnReadyAsync()
    {
        logger.LogInformation("Discord bot is ready! Logged in as {Username}", discordClient.CurrentUser.Username);
        return Task.CompletedTask;
    }

    private async Task OnGuildMemberUpdatedAsync(Cacheable<SocketGuildUser, ulong> before, SocketGuildUser after)
    {
        try
        {
            // Check if roles have changed
            var beforeRoles = before.HasValue ? before.Value.Roles.Select(r => r.Id).ToHashSet() : new HashSet<ulong>();
            var afterRoles = after.Roles.Select(r => r.Id).ToHashSet();

            if (beforeRoles.SetEquals(afterRoles))
            {
                // Roles haven't changed, skip
                return;
            }

            logger.LogDebug("Detected role change for {Username} ({DiscordId})", after.Username, after.Id);

            // Get the sync service and trigger role update
            var syncService = serviceProvider.GetRequiredService<GuildMemberSyncService>();
            await syncService.SyncSingleUserRoleAsync(after.Id.ToString(), afterRoles.ToList());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle guild member update for {Username}", after.Username);
        }
    }
}
