using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Nucleus.Repository;

namespace Nucleus.Discord;

public static class DiscordUserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapGet("/me", GetMe);
        app.MapGet("/user/{userId}", GetUser);
    }

    [Authorize]
    public static async Task<Results<Ok<DiscordUser>, UnauthorizedHttpResult>> GetMe(ClaimsPrincipal user, NucleusDbContext dbContext)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId == null)
            return TypedResults.Unauthorized();
        var dbUser = await dbContext.DiscordUsers.SingleAsync(u => u.DiscordId == discordId);
        string? avatar = user.FindFirst("urn:discord:avatar")?.Value;
        string avatarUrl = $"https://cdn.discordapp.com/avatars/{discordId}/{avatar}";
        return TypedResults.Ok(new DiscordUser(dbUser.Id, dbUser.Username, avatarUrl
            ));
    }
    
    [Authorize]
    public static async Task<Results<Ok<DiscordUser>, NotFound>> GetUser(Guid userId, NucleusDbContext dbContext)
    {
        var dbUser = await dbContext.DiscordUsers.SingleOrDefaultAsync(u => u.Id == userId);
        if (dbUser == null)
            return TypedResults.NotFound();
        return TypedResults.Ok(new DiscordUser(dbUser.Id, dbUser.Username, $"https://cdn.discordapp.com/avatars/{dbUser.DiscordId}/{dbUser.Avatar}"));
    }
}