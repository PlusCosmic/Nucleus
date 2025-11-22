using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Nucleus.Discord;

public static class DiscordUserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapGet("/me", GetMe);
        app.MapGet("/user/{userId}", GetUser);
        app.MapGet("/me/preferences", GetMyPreferences);
        app.MapPatch("/me/preferences", UpdateMyPreferences);
    }

    [Authorize]
    public static async Task<Results<Ok<DiscordUser>, UnauthorizedHttpResult>> GetMe(ClaimsPrincipal user, DiscordStatements discordStatements)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId == null)
        {
            return TypedResults.Unauthorized();
        }

        DiscordStatements.DiscordUserRow? dbUser = await discordStatements.GetUserByDiscordId(discordId);
        if (dbUser == null)
        {
            return TypedResults.Unauthorized();
        }

        string? avatar = user.FindFirst("urn:discord:avatar")?.Value;
        string avatarUrl = $"https://cdn.discordapp.com/avatars/{discordId}/{avatar}";
        return TypedResults.Ok(new DiscordUser(dbUser.Id, dbUser.Username, dbUser.GlobalName, avatarUrl));
    }

    [Authorize]
    public static async Task<Results<Ok<DiscordUser>, NotFound>> GetUser(Guid userId, DiscordStatements discordStatements)
    {
        DiscordStatements.DiscordUserRow? dbUser = await discordStatements.GetUserById(userId);
        if (dbUser == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(new DiscordUser(dbUser.Id, dbUser.Username, dbUser.GlobalName, $"https://cdn.discordapp.com/avatars/{dbUser.DiscordId}/{dbUser.Avatar}"));
    }

    [Authorize]
    public static async Task<Results<Ok<UserPreferences>, UnauthorizedHttpResult, NotFound>> GetMyPreferences(
        ClaimsPrincipal user,
        DiscordStatements discordStatements)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId == null)
        {
            return TypedResults.Unauthorized();
        }

        DiscordStatements.DiscordUserRow? dbUser = await discordStatements.GetUserByDiscordId(discordId);
        if (dbUser == null)
        {
            return TypedResults.Unauthorized();
        }

        UserPreferences? preferences = await discordStatements.GetUserPreferences(dbUser.Id);
        if (preferences == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(preferences);
    }

    [Authorize]
    public static async Task<Results<Ok<UserPreferences>, UnauthorizedHttpResult, BadRequest<string>>> UpdateMyPreferences(
        ClaimsPrincipal user,
        UpdatePreferencesRequest request,
        DiscordStatements discordStatements)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId == null)
        {
            return TypedResults.Unauthorized();
        }

        DiscordStatements.DiscordUserRow? dbUser = await discordStatements.GetUserByDiscordId(discordId);
        if (dbUser == null)
        {
            return TypedResults.Unauthorized();
        }

        await discordStatements.UpdateUserPreferences(dbUser.Id, request.DiscordNotificationsEnabled);

        UserPreferences? updatedPreferences = await discordStatements.GetUserPreferences(dbUser.Id);
        if (updatedPreferences == null)
        {
            return TypedResults.BadRequest("Failed to update preferences");
        }

        return TypedResults.Ok(updatedPreferences);
    }
}

public record UpdatePreferencesRequest(bool DiscordNotificationsEnabled);