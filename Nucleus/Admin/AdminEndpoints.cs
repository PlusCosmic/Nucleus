using Microsoft.AspNetCore.Http.HttpResults;
using Nucleus.Shared.Auth;
using Nucleus.Discord;

namespace Nucleus.Admin;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("admin")
            .RequireAuthorization()
            .RequirePermission(Permissions.AdminUsers);

        group.MapGet("users", GetAllUsers).WithName("GetAllUsers");
        group.MapGet("users/{id:guid}", GetUserById).WithName("GetUserById");
        group.MapPost("users/{id:guid}/permissions/{permission}", GrantPermission).WithName("GrantPermission");
        group.MapDelete("users/{id:guid}/permissions/{permission}", RevokePermission).WithName("RevokePermission");
    }

    private static async Task<Ok<List<UserWithPermissions>>> GetAllUsers(
        DiscordStatements statements)
    {
        List<DiscordStatements.DiscordUserRow> users = await statements.GetAllUsers();
        List<UserWithPermissions> result = [];

        foreach (DiscordStatements.DiscordUserRow user in users)
        {
            List<string> additionalPermissions = await statements.GetUserAdditionalPermissions(user.Id);
            UserRole role = ParseRole(user.Role);
            List<string> effectivePermissions = GetEffectivePermissions(role, additionalPermissions);

            result.Add(new UserWithPermissions(
                user.Id,
                user.DiscordId,
                user.Username,
                user.GlobalName,
                user.Avatar,
                role,
                additionalPermissions,
                effectivePermissions
            ));
        }

        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<UserWithPermissions>, NotFound>> GetUserById(
        DiscordStatements statements,
        Guid id)
    {
        DiscordStatements.DiscordUserRow? user = await statements.GetUserById(id);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        List<string> additionalPermissions = await statements.GetUserAdditionalPermissions(user.Id);
        UserRole role = ParseRole(user.Role);
        List<string> effectivePermissions = GetEffectivePermissions(role, additionalPermissions);

        return TypedResults.Ok(new UserWithPermissions(
            user.Id,
            user.DiscordId,
            user.Username,
            user.GlobalName,
            user.Avatar,
            role,
            additionalPermissions,
            effectivePermissions
        ));
    }

    private static async Task<Results<Ok<UserWithPermissions>, NotFound, BadRequest<string>>> GrantPermission(
        DiscordStatements statements,
        AuthenticatedUser currentUser,
        Guid id,
        string permission)
    {
        DiscordStatements.DiscordUserRow? user = await statements.GetUserById(id);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        if (!IsValidPermission(permission))
        {
            return TypedResults.BadRequest($"Invalid permission: {permission}");
        }

        await statements.GrantPermission(user.Id, permission, currentUser.Id);

        List<string> additionalPermissions = await statements.GetUserAdditionalPermissions(user.Id);
        UserRole role = ParseRole(user.Role);
        List<string> effectivePermissions = GetEffectivePermissions(role, additionalPermissions);

        return TypedResults.Ok(new UserWithPermissions(
            user.Id,
            user.DiscordId,
            user.Username,
            user.GlobalName,
            user.Avatar,
            role,
            additionalPermissions,
            effectivePermissions
        ));
    }

    private static async Task<Results<Ok<UserWithPermissions>, NotFound>> RevokePermission(
        DiscordStatements statements,
        Guid id,
        string permission)
    {
        DiscordStatements.DiscordUserRow? user = await statements.GetUserById(id);
        if (user is null)
        {
            return TypedResults.NotFound();
        }

        await statements.RevokePermission(user.Id, permission);

        List<string> additionalPermissions = await statements.GetUserAdditionalPermissions(user.Id);
        UserRole role = ParseRole(user.Role);
        List<string> effectivePermissions = GetEffectivePermissions(role, additionalPermissions);

        return TypedResults.Ok(new UserWithPermissions(
            user.Id,
            user.DiscordId,
            user.Username,
            user.GlobalName,
            user.Avatar,
            role,
            additionalPermissions,
            effectivePermissions
        ));
    }

    private static UserRole ParseRole(string roleString)
    {
        return Enum.TryParse<UserRole>(roleString, ignoreCase: true, out UserRole role)
            ? role
            : UserRole.Viewer;
    }

    private static List<string> GetEffectivePermissions(UserRole role, List<string> additionalPermissions)
    {
        HashSet<string> effective = Permissions.GetRolePermissions(role);

        // If user has wildcard permission, return just that
        if (effective.Contains(Permissions.All))
        {
            return [Permissions.All];
        }

        // Add additional permissions
        foreach (string perm in additionalPermissions)
        {
            effective.Add(perm);
        }

        return effective.Order().ToList();
    }

    private static bool IsValidPermission(string permission)
    {
        // Check against known permissions
        return permission == Permissions.All ||
               permission == Permissions.ClipsRead ||
               permission == Permissions.ClipsCreate ||
               permission == Permissions.ClipsEdit ||
               permission == Permissions.ClipsDelete ||
               permission == Permissions.PlaylistsRead ||
               permission == Permissions.PlaylistsManage ||
               permission == Permissions.LinksRead ||
               permission == Permissions.LinksManage ||
               permission == Permissions.MinecraftStatus ||
               permission == Permissions.MinecraftConsole ||
               permission == Permissions.MinecraftFiles ||
               permission == Permissions.AdminUsers;
    }
}
