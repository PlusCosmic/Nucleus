using Nucleus.Auth;

namespace Nucleus.Discord;

public class GuildMemberSyncService(
    DiscordBotService discordBotService,
    DiscordRoleMapping roleMapping,
    WhitelistService whitelistService,
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

        // Upsert basic user data
        var upsertedUsers = await discordStatements.BulkUpsertUsers(members);
        logger.LogInformation("Synced {Count} guild members to database", upsertedUsers.Count);

        // Sync roles from Discord (if role mappings are configured)
        if (roleMapping.HasMappings)
        {
            await SyncRolesAsync(members, upsertedUsers, discordStatements);
        }
    }

    private async Task SyncRolesAsync(
        List<GuildMemberData> members,
        List<DiscordStatements.DiscordUserRow> upsertedUsers,
        DiscordStatements discordStatements)
    {
        var memberLookup = members.ToDictionary(m => m.DiscordId);
        var rolesUpdated = 0;

        foreach (var user in upsertedUsers)
        {
            // Skip users who have an explicit role override in whitelist
            if (whitelistService.HasExplicitRole(user.DiscordId))
            {
                continue;
            }

            // Get the member's Discord roles
            if (!memberLookup.TryGetValue(user.DiscordId, out var member))
                continue;

            // Map Discord roles to Nucleus role
            var discordRole = roleMapping.GetRoleForDiscordRoles(member.DiscordRoleIds);
            var targetRole = discordRole ?? UserRole.Viewer;

            // Only update if the role has changed
            if (!Enum.TryParse<UserRole>(user.Role, ignoreCase: true, out var currentRole) ||
                currentRole != targetRole)
            {
                await discordStatements.UpdateUserRole(user.Id, targetRole.ToString());
                logger.LogDebug("Updated role for {Username} from {OldRole} to {NewRole} (Discord sync)",
                    user.Username, user.Role, targetRole);
                rolesUpdated++;
            }
        }

        if (rolesUpdated > 0)
        {
            logger.LogInformation("Updated roles for {Count} users from Discord", rolesUpdated);
        }
    }

    /// <summary>
    /// Syncs a single user's role from their Discord roles.
    /// Called by real-time event handlers.
    /// </summary>
    public async Task SyncSingleUserRoleAsync(string discordId, List<ulong> discordRoleIds)
    {
        // Skip if whitelist has explicit role override
        if (whitelistService.HasExplicitRole(discordId))
        {
            logger.LogDebug("Skipping Discord role sync for {DiscordId} - whitelist role override exists", discordId);
            return;
        }

        using var scope = serviceScopeFactory.CreateScope();
        var discordStatements = scope.ServiceProvider.GetRequiredService<DiscordStatements>();

        var user = await discordStatements.GetUserByDiscordId(discordId);
        if (user is null)
        {
            logger.LogDebug("User {DiscordId} not found in database, skipping role sync", discordId);
            return;
        }

        var discordRole = roleMapping.GetRoleForDiscordRoles(discordRoleIds);
        var targetRole = discordRole ?? UserRole.Viewer;

        if (!Enum.TryParse<UserRole>(user.Role, ignoreCase: true, out var currentRole) ||
            currentRole != targetRole)
        {
            await discordStatements.UpdateUserRole(user.Id, targetRole.ToString());
            logger.LogInformation("Real-time role update: {Username} changed from {OldRole} to {NewRole}",
                user.Username, user.Role, targetRole);
        }
    }
}
