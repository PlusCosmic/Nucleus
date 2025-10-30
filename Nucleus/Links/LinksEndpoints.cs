using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Nucleus.Models;

namespace Nucleus.Links;

public static class LinksEndpoints
{
    public static void MapLinksEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("links").RequireAuthorization();
        group.MapGet("",  GetLinksForUser).WithName("GetLinksForUser");
        group.MapDelete("{id:guid}", DeleteLinkById).WithName("DeleteLink");
        group.MapPost("", AddLink).WithName("AddLink");
    }
    
    public static async Task<Results<Ok<List<UserFrequentLink>>, UnauthorizedHttpResult>> GetLinksForUser(LinksService linksService, ClaimsPrincipal user)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();
    
        var links = await linksService.GetLinksForUser(discordId);
        return TypedResults.Ok(links);
    }
    
    public static async Task<Results<NoContent, NotFound, UnauthorizedHttpResult>> DeleteLinkById(LinksService linksService, Guid id, ClaimsPrincipal user)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();
    
        var result = await linksService.DeleteLink(id, discordId);
        return result ? TypedResults.NoContent() : TypedResults.NotFound();
    }
    
    public static async Task<Results<Created, UnauthorizedHttpResult>> AddLink(LinksService linksService, LinkRequest link, ClaimsPrincipal user)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();
    
        await linksService.AddLink(discordId, link.Url);
        return TypedResults.Created();
    }
}