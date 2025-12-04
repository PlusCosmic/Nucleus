using System.Security.Claims;
using Nucleus.Discord;

namespace Nucleus.Auth;

/// <summary>
/// Middleware that resolves the authenticated user from the database and stores it in HttpContext.Items.
/// This middleware runs after WhitelistMiddleware and before endpoint execution.
/// The user is then available via AuthenticatedUser.BindAsync for parameter binding.
/// </summary>
public class AuthenticatedUserMiddleware(RequestDelegate next)
{
    // Paths that don't need user resolution (same as WhitelistMiddleware bypass paths)
    private static readonly HashSet<string> BypassPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health",
        "/auth",
        "/apex-legends",
        "/dropzone",
        "/webhooks"
    };

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip user resolution for bypass paths
        string path = context.Request.Path.Value ?? "";
        if (BypassPaths.Any(bp => path.StartsWith(bp, StringComparison.OrdinalIgnoreCase)))
        {
            await next(context);
            return;
        }

        // Skip if not authenticated
        string? discordId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            await next(context);
            return;
        }

        // Resolve the user from the database and cache in HttpContext.Items
        DiscordStatements discordStatements = context.RequestServices.GetRequiredService<DiscordStatements>();
        DiscordStatements.DiscordUserRow? dbUser = await discordStatements.GetUserByDiscordId(discordId);

        if (dbUser is not null)
        {
            // Parse the role from the database
            UserRole role = ParseRole(dbUser.Role);

            // Load additional permissions
            List<string> additionalPermissions = await discordStatements.GetUserAdditionalPermissions(dbUser.Id);

            context.Items[AuthenticatedUser.HttpContextKey] = new AuthenticatedUser(
                dbUser.Id,
                dbUser.DiscordId,
                dbUser.Username,
                dbUser.GlobalName,
                dbUser.Avatar,
                role,
                new HashSet<string>(additionalPermissions));
        }

        await next(context);
    }

    private static UserRole ParseRole(string roleString)
    {
        return Enum.TryParse<UserRole>(roleString, ignoreCase: true, out UserRole role)
            ? role
            : UserRole.Viewer; // Default to Viewer if parsing fails
    }
}

/// <summary>
/// Extension methods for registering AuthenticatedUserMiddleware.
/// </summary>
public static class AuthenticatedUserMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthenticatedUserResolution(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AuthenticatedUserMiddleware>();
    }
}
