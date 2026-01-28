using Discord;
using Discord.WebSocket;
using Nucleus.Shared.Discord;

namespace Nucleus.Discord;

public class DiscordBotService(
    DiscordSocketClient discordClient,
    ILogger<DiscordBotService> logger)
{

    public async Task<List<GuildMemberData>> GetAllGuildMembersAsync()
    {
        var members = new Dictionary<ulong, GuildMemberData>();

        foreach (var guild in discordClient.Guilds)
        {
            try
            {
                await guild.DownloadUsersAsync();

                foreach (var member in guild.Users)
                {
                    if (member.IsBot)
                        continue;

                    var discordId = member.Id;
                    if (members.ContainsKey(discordId))
                        continue;

                    members[discordId] = new GuildMemberData(
                        discordId.ToString(),
                        member.Username,
                        member.GlobalName,
                        member.GetAvatarUrl() ?? member.GetDefaultAvatarUrl(),
                        member.Roles.Select(r => r.Id).ToList()
                    );
                }

                logger.LogDebug("Fetched {Count} members from guild {GuildName}", guild.Users.Count, guild.Name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch members from guild {GuildName}", guild.Name);
            }
        }

        logger.LogInformation("Fetched {Count} unique members across {GuildCount} guilds",
            members.Count, discordClient.Guilds.Count);

        return members.Values.ToList();
    }

    public bool IsConnected => discordClient.ConnectionState == ConnectionState.Connected;

    /// <summary>
    /// Gets member data for a specific user by their Discord ID.
    /// Returns null if the user is not found in any connected guild.
    /// </summary>
    public GuildMemberData? GetMemberData(ulong discordUserId)
    {
        foreach (var guild in discordClient.Guilds)
        {
            var member = guild.GetUser(discordUserId);
            if (member is not null && !member.IsBot)
            {
                return new GuildMemberData(
                    discordUserId.ToString(),
                    member.Username,
                    member.GlobalName,
                    member.GetAvatarUrl() ?? member.GetDefaultAvatarUrl(),
                    member.Roles.Select(r => r.Id).ToList()
                );
            }
        }

        return null;
    }

}