using Nucleus.Discord;

namespace Nucleus.Links;

public class LinksService(LinksStatements linksStatements, DiscordStatements discordStatements)
{
    public async Task AddLink(string discordId, string url)
    {
        var meta = await PageMetadataFetcher.GetPageMetadataAsync(url);
        if (meta != null)
        {
            var link = new Link(
                Title: meta.Title ?? meta.PageUri.Host,
                Url: meta.PageUri.ToString(),
                ThumbnailUrl: meta.FaviconUri?.ToString() ?? string.Empty
            );

            var activeUser = await discordStatements.GetUserByDiscordId(discordId)
                ?? throw new InvalidOperationException("Discord user not found");

            await linksStatements.InsertLink(activeUser.Id, link.Title, link.Url, link.ThumbnailUrl);
        }
        else
        {
            throw new InvalidOperationException("Failed to fetch page metadata");
        }
    }

    public async Task<List<LinksStatements.UserFrequentLinkRow>> GetLinksForUser(string discordId)
    {
        var activeUser = await discordStatements.GetUserByDiscordId(discordId)
            ?? throw new InvalidOperationException("Discord user not found");

        var linkRows = await linksStatements.GetLinksByUserId(activeUser.Id);

        return linkRows;
    }

    public async Task<bool> DeleteLink(Guid id, string discordId)
    {
        var activeUser = await discordStatements.GetUserByDiscordId(discordId)
            ?? throw new InvalidOperationException("Discord user not found");

        var link = await linksStatements.GetLinkById(id);
        if (link == null || link.UserId != activeUser.Id)
        {
            return false;
        }

        await linksStatements.DeleteLink(id);
        return true;
    }
}