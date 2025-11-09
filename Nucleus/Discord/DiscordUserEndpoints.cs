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
}