using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Nucleus.Discord;
using Nucleus.Models;

namespace Nucleus.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapGet("/me", GetMe);
    }

    [Authorize]
    public static async Task<Results<Ok<User>, UnauthorizedHttpResult>> GetMe(ClaimsPrincipal user, NucleusDbContext dbContext)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId == null)
            return TypedResults.Unauthorized();
        var dbUser = await dbContext.DiscordUsers.SingleAsync();
        return TypedResults.Ok(new User(dbUser.Id, dbUser.Username,
            user.FindFirst("urn:discord:avatar")?.Value));
    }
}