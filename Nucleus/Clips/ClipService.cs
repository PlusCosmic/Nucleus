using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Nucleus.Clips.Bunny;
using Nucleus.Clips.Bunny.Models;
using Nucleus.Repository;

namespace Nucleus.Clips;

public class ClipService(BunnyService bunnyService, NucleusDbContext dbContext, IConfiguration configuration)
{
    private static string NormalizeTag(string tag)
    {
        return tag.Trim().ToLowerInvariant();
    }

    public async Task<List<Clip>> GetClipsForCategory(ClipCategoryEnum categoryEnum, string discordUserId, int page)
    {
        DiscordUser discordUser = await dbContext.DiscordUsers.SingleOrDefaultAsync(d => d.DiscordId == discordUserId) ?? throw new InvalidOperationException("No Discord user");
        Guid userId = discordUser.Id;
        
        // get collection
        ClipCollection? clipCollection = await dbContext.ClipCollections.SingleOrDefaultAsync(s => s.OwnerId == userId && s.CategoryEnum == categoryEnum);
        if (clipCollection == null)
        {
            BunnyCollection bunnyCollection = await bunnyService.CreateCollectionAsync(categoryEnum, userId);
            ClipCollection newCollection = new ClipCollection
            {
                CategoryEnum = categoryEnum,
                CollectionId = bunnyCollection.Guid,
                OwnerId = userId
            };
            clipCollection = newCollection;
            await dbContext.ClipCollections.AddAsync(clipCollection);
            await dbContext.SaveChangesAsync();
            // If the collection didn't exist, there won't be any videos in it
            return [];
        }
        // get videos from bunny
        List<BunnyVideo> bunnyVideos = await bunnyService.GetVideosForCollectionAsync(clipCollection.CollectionId, page);
        
        // get clips from db including tags
        List<Repository.Clip> repositoryClips = await dbContext.Clips
            .Where(c => c.CategoryEnum == categoryEnum && c.OwnerId == userId)
            .Include(c => c.ClipTags)
            .ThenInclude(ct => ct.Tag)
            .ToListAsync();
        // create clip objects
        return repositoryClips.Select(c => new Clip(
            c.Id,
            c.OwnerId,
            c.VideoId,
            categoryEnum,
            bunnyVideos.First(v => v.Guid == c.VideoId),
            c.ClipTags.Select(ct => ct.Tag.Name).ToList()
        )).ToList();
    }

    public List<ClipCategory> GetCategories()
    {
        return [new("Apex Legends", ClipCategoryEnum.ApexLegends, "/images/apex_legends.jpg"),
            new ("Call of Duty: Warzone", ClipCategoryEnum.CallOfDutyWarzone, "/images/callofduty_warzone.jpg"),
            new ("Snowboarding", ClipCategoryEnum.Snowboarding, "/images/snowboarding.png")];
    }

    public async Task<CreateClipResponse> CreateClip(ClipCategoryEnum categoryEnum, string videoTitle, string discordUserId)
    {
        DiscordUser discordUser = await dbContext.DiscordUsers.SingleOrDefaultAsync(d => d.DiscordId == discordUserId) ?? throw new InvalidOperationException("No Discord user");
        Guid userId = discordUser.Id;

        ClipCollection clipCollection = await dbContext.ClipCollections.SingleOrDefaultAsync(cc => cc.OwnerId == userId && cc.CategoryEnum == categoryEnum) ?? throw new InvalidOperationException("No Clip Collection");
        BunnyVideo video = await bunnyService.CreateVideoAsync(clipCollection.CollectionId, videoTitle);
        
        Repository.Clip clip = new Repository.Clip { CategoryEnum = categoryEnum, OwnerId = userId, VideoId = video.Guid };
        await dbContext.Clips.AddAsync(clip);
        await dbContext.SaveChangesAsync();
        
        long expiration = DateTimeOffset.Now.AddHours(1).ToUnixTimeSeconds();
        string libraryId = configuration["BunnyLibraryId"] ??
                           throw new InvalidOperationException("Bunny API library ID not configured");
        string secretKey = configuration["BunnyAccessKey"] ?? throw new InvalidOperationException("Bunny access key not configured");
        byte[] signature = Encoding.UTF8.GetBytes(libraryId + secretKey + expiration + video.Guid);
        var hash = SHA256.HashData(signature);
        string hashString = Convert.ToHexString(hash);
        return new CreateClipResponse(hashString ?? throw new InvalidOperationException("Hash computation failed"), expiration, libraryId, video.Guid, video.CollectionId);
    }
    
    public async Task<Clip?> GetClipById(Guid clipId, string discordUserId)
    {
        DiscordUser discordUser = await dbContext.DiscordUsers.SingleOrDefaultAsync(d => d.DiscordId == discordUserId) ?? throw new InvalidOperationException("No Discord user");
        Guid userId = discordUser.Id;
        
        Repository.Clip? clip = await dbContext.Clips
            .Include(c => c.ClipTags)
            .ThenInclude(ct => ct.Tag)
            .SingleOrDefaultAsync(c => c.Id == clipId);
        if (clip == null)
            return null;
        
        BunnyVideo? video = await bunnyService.GetVideoByIdAsync(clip.VideoId);
        if (video == null)
            return null;
        
        return new Clip(clip.Id, clip.OwnerId, clip.VideoId, clip.CategoryEnum, video, clip.ClipTags.Select(ct => ct.Tag.Name).ToList());
    }

    public async Task<Clip?> AddTagToClip(Guid clipId, string discordUserId, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("Tag cannot be empty", nameof(tag));
        tag = NormalizeTag(tag);
        if (tag.Length > 32) tag = tag.Substring(0, 32);

        DiscordUser discordUser = await dbContext.DiscordUsers.SingleOrDefaultAsync(d => d.DiscordId == discordUserId) ?? throw new InvalidOperationException("No Discord user");
        Repository.Clip? clip = await dbContext.Clips
            .Include(c => c.ClipTags)
            .ThenInclude(ct => ct.Tag)
            .SingleOrDefaultAsync(c => c.Id == clipId && c.OwnerId == discordUser.Id);
        if (clip == null) return null;

        if (clip.ClipTags.Select(ct => ct.Tag.Name).Contains(tag))
            return await GetClipById(clipId, discordUserId); // already tagged
        if (clip.ClipTags.Count >= 5)
            throw new InvalidOperationException("A clip can have up to 5 tags");

        Tag? tagEntity = await dbContext.Tags.SingleOrDefaultAsync(t => t.Name == tag);
        if (tagEntity == null)
        {
            tagEntity = new Tag { Name = tag };
            await dbContext.Tags.AddAsync(tagEntity);
            await dbContext.SaveChangesAsync();
        }
        clip.ClipTags.Add(new ClipTag { ClipId = clip.Id, TagId = tagEntity.Id, Tag = tagEntity, Clip = clip });
        await dbContext.SaveChangesAsync();

        return await GetClipById(clipId, discordUserId);
    }

    public async Task<Clip?> RemoveTagFromClip(Guid clipId, string discordUserId, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("Tag cannot be empty", nameof(tag));
        tag = NormalizeTag(tag);

        DiscordUser discordUser = await dbContext.DiscordUsers.SingleOrDefaultAsync(d => d.DiscordId == discordUserId) ?? throw new InvalidOperationException("No Discord user");
        Repository.Clip? clip = await dbContext.Clips
            .Include(c => c.ClipTags)
            .ThenInclude(ct => ct.Tag)
            .SingleOrDefaultAsync(c => c.Id == clipId && c.OwnerId == discordUser.Id);
        if (clip == null) return null;

        ClipTag? link = clip.ClipTags.FirstOrDefault(ct => ct.Tag.Name == tag);
        if (link != null)
        {
            dbContext.ClipTags.Remove(link);
            await dbContext.SaveChangesAsync();
        }
        return await GetClipById(clipId, discordUserId);
    }

    public async Task<List<TopTag>> GetTopTags(int limit = 20)
    {
        return await dbContext.ClipTags
            .GroupBy(ct => ct.Tag.Name)
            .Select(g => new TopTag(g.Key, g.Count()))
            .OrderByDescending(t => t.Count)
            .ThenBy(t => t.Name)
            .Take(limit)
            .ToListAsync();
    }
}