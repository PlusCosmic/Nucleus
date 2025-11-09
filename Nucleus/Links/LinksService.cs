using Nucleus.Discord;
using Nucleus.Data.Discord;
using Nucleus.Data.Links;
using Nucleus.Exceptions;

namespace Nucleus.Links;

public class LinksService(LinksStatements linksStatements, DiscordStatements discordStatements)
{
    public async Task AddLink(string discordId, string url)
    {
        PageMetadata? meta;
        try
        {
            meta = await PageMetadataFetcher.GetPageMetadataAsync(url);
        }
        catch (Exception)
        {
            // If metadata fetching fails (network error, invalid URL, etc.),
            // still create the link with basic info
            meta = null;
        }

        var activeUser = await discordStatements.GetUserByDiscordId(discordId)
            ?? throw new UnauthorizedException("User not found");

        if (meta != null)
        {
            var link = new Link(
                Title: meta.Title ?? meta.PageUri.Host,
                Url: meta.PageUri.ToString(),
                ThumbnailUrl: meta.FaviconUri?.ToString() ?? string.Empty
            );

            await linksStatements.InsertLink(activeUser.Id, link.Title, link.Url, link.ThumbnailUrl);
        }
        else
        {
            // Fallback: create link with just the URL
            await linksStatements.InsertLink(activeUser.Id, url, url, string.Empty);
        }
    }

    public async Task<List<LinksStatements.UserFrequentLinkRow>> GetLinksForUser(string discordId)
    {
        var activeUser = await discordStatements.GetUserByDiscordId(discordId)
            ?? throw new UnauthorizedException("User not found");

        var linkRows = await linksStatements.GetLinksByUserId(activeUser.Id);

        return linkRows;
    }

    public async Task<bool> DeleteLink(Guid id, string discordId)
    {
        var activeUser = await discordStatements.GetUserByDiscordId(discordId)
            ?? throw new UnauthorizedException("User not found");

        var link = await linksStatements.GetLinkById(id);
        if (link == null || link.UserId != activeUser.Id)
        {
            return false;
        }

        await linksStatements.DeleteLink(id);
        return true;
    }
}