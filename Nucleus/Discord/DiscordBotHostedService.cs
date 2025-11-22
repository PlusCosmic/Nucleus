using Discord;
using Discord.WebSocket;

namespace Nucleus.Discord;

public class DiscordBotHostedService(
    DiscordSocketClient discordClient,
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
}
