using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Nucleus.Clips;

namespace Nucleus.Discord;

public class DiscordBotService(
    DiscordSocketClient discordClient,
    DiscordStatements discordStatements,
    IConfiguration configuration,
    ILogger<DiscordBotService> logger)
{
    private readonly string _backendAddress = configuration["BackendAddress"] ?? "https://api.pluscosmic.dev";

    public async Task SendPlaylistInvitationMessage(DiscordUser invitor, DiscordUser invitee, Playlist playlist)
    {
        try
        {
            var preferences = await discordStatements.GetUserPreferences(invitee.Id);
            if (preferences?.DiscordNotificationsEnabled == false)
            {
                logger.LogInformation("User {UserId} has Discord notifications disabled, skipping notification", invitee.Id);
                return;
            }

            var inviteeRow = await discordStatements.GetUserById(invitee.Id);
            if (inviteeRow == null || !ulong.TryParse(inviteeRow.DiscordId, out var discordUserId))
            {
                logger.LogWarning("Could not find Discord ID for user {UserId}", invitee.Id);
                return;
            }

            var user = await discordClient.GetUserAsync(discordUserId);
            if (user == null)
            {
                logger.LogWarning("Could not find Discord user with ID {DiscordId}", discordUserId);
                return;
            }

            var playlistUrl = $"{_backendAddress}/playlists/{playlist.Id}";
            var invitorDisplay = invitor.GlobalName ?? invitor.Username;

            var embed = new EmbedBuilder()
                .WithTitle("🎮 Plus Cosmic Clips")
                .WithDescription($"**@{invitorDisplay}** added you to **\"{playlist.Name}\"**")
                .WithColor(new Color(88, 101, 242))
                .AddField("View Playlist", playlistUrl)
                .WithFooter("You can disable these notifications in your account settings")
                .WithCurrentTimestamp()
                .Build();

            await user.SendMessageAsync(embed: embed);
            logger.LogInformation("Sent playlist invitation notification to {Username} for playlist {PlaylistId}",
                invitee.Username, playlist.Id);
        }
        catch (global::Discord.Net.HttpException ex)
        {
            logger.LogWarning(ex, "Cannot send DM to user {Username} (DMs may be disabled or user blocked bot)", invitee.Username);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Discord notification to {Username} for playlist {PlaylistId}",
                invitee.Username, playlist.Id);
        }
    }
}