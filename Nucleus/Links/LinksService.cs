using Microsoft.EntityFrameworkCore;
using Nucleus.Models;

namespace Nucleus.Links;

public class LinksService(NucleusDbContext context)
{
    public async Task AddLink(string discordId, Link link)
    {
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