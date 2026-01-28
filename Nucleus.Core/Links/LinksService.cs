using Nucleus.Discord;
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

        DiscordStatements.DiscordUserRow activeUser = await discordStatements.GetUserByDiscordId(discordId)
                                                      ?? throw new UnauthorizedException("User not found");

        if (meta != null)
        {
            Link link = new(
                meta.Title ?? meta.PageUri.Host,
                meta.PageUri.ToString(),
                meta.FaviconUri?.ToString() ?? string.Empty
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
        DiscordStatements.DiscordUserRow activeUser = await discordStatements.GetUserByDiscordId(discordId)
                                                      ?? throw new UnauthorizedException("User not found");

        List<LinksStatements.UserFrequentLinkRow> linkRows = await linksStatements.GetLinksByUserId(activeUser.Id);

        return linkRows;
    }

    public async Task<bool> DeleteLink(Guid id, string discordId)
    {
        DiscordStatements.DiscordUserRow activeUser = await discordStatements.GetUserByDiscordId(discordId)
                                                      ?? throw new UnauthorizedException("User not found");

        LinksStatements.UserFrequentLinkRow? link = await linksStatements.GetLinkById(id);
        if (link == null || link.UserId != activeUser.Id)
        {
            return false;
        }

        await linksStatements.DeleteLink(id);
        return true;
    }
}