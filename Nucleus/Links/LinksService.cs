using Microsoft.EntityFrameworkCore;
using Nucleus.Models;

namespace Nucleus.Links;

public class LinksService(NucleusDbContext context)
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
            DiscordUser activeUser = await context.DiscordUsers.SingleAsync(u => u.DiscordId == discordId);
            UserFrequentLink linkRecord = new UserFrequentLink
            {
                UserId = activeUser.Id,
                Title = link.Title,
                Url = link.Url,
                ThumbnailUrl = link.ThumbnailUrl
            };
            await context.UserFrequentLinks.AddAsync(linkRecord);
            await context.SaveChangesAsync();
        }
        else
        {
            throw new InvalidOperationException("Failed to fetch page metadata");
        }
    }
    
    public async Task<List<UserFrequentLink>> GetLinksForUser(string discordId)
    {
        DiscordUser activeUser = await context.DiscordUsers.SingleAsync(u => u.DiscordId == discordId);
        return await context.UserFrequentLinks.Where(l => l.UserId == activeUser.Id).ToListAsync();
    }
    
    public async Task<bool> DeleteLink(Guid id, string discordId)
    {
        DiscordUser activeUser = await context.DiscordUsers.SingleAsync(u => u.DiscordId == discordId);
        UserFrequentLink link = await context.UserFrequentLinks.SingleAsync(l => l.Id == id);
        if (link.UserId != activeUser.Id)
        {
            return false;
        }
        context.UserFrequentLinks.Remove(link);
        await context.SaveChangesAsync();
        return true;
    }
}