using Microsoft.AspNetCore.Http.HttpResults;
using Nucleus.Auth;

namespace Nucleus.Links;

public static class LinksEndpoints
{
    public static void MapLinksEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("links")
            .RequireAuthorization();

        group.MapGet("", GetLinksForUser).WithName("GetLinksForUser");
        group.MapDelete("{id:guid}", DeleteLinkById).WithName("DeleteLink");
        group.MapPost("", AddLink).WithName("AddLink");
    }

    private static async Task<Ok<List<LinksStatements.UserFrequentLinkRow>>> GetLinksForUser(
        LinksService linksService,
        AuthenticatedUser user)
    {
        List<LinksStatements.UserFrequentLinkRow> links = await linksService.GetLinksForUser(user.DiscordId);
        return TypedResults.Ok(links);
    }

    private static async Task<Results<NoContent, NotFound>> DeleteLinkById(
        LinksService linksService,
        Guid id,
        AuthenticatedUser user)
    {
        bool result = await linksService.DeleteLink(id, user.DiscordId);
        return result ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static async Task<Created> AddLink(
        LinksService linksService,
        LinkRequest link,
        AuthenticatedUser user)
    {
        await linksService.AddLink(user.DiscordId, link.Url);
        return TypedResults.Created();
    }
}
