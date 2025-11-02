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

    public async Task<PagedClipsResponse> GetClipsForCategory(ClipCategoryEnum categoryEnum, string discordUserId, int page, int pageSize)
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
            return new PagedClipsResponse([], 0);
        }
        // get videos from bunny
        PagedVideoResponse pagedResponse = await bunnyService.GetVideosForCollectionAsync(clipCollection.CollectionId, page, pageSize);

        // get clips from db including tags
        List<Repository.Clip> repositoryClips = await dbContext.Clips
            .Where(c => c.CategoryEnum == categoryEnum && c.OwnerId == userId)
            .Include(c => c.ClipTags)
            .ThenInclude(ct => ct.Tag)
            .ToListAsync();

        // get viewed clips for this user
        var clipIds = repositoryClips.Select(c => c.Id).ToList();
        var viewedClipIds = dbContext.ClipViews
            .Where(cv => cv.UserId == userId && clipIds.Contains(cv.ClipId))
            .Select(cv => cv.ClipId)
            .ToHashSet();
        
        var repoClipsByVideoId = repositoryClips.ToDictionary(c => c.VideoId, c => c);
        var clips = pagedResponse.Items
            .Select(v =>
            {
                if (!repoClipsByVideoId.TryGetValue(v.Guid, out var repoClip)) return null;
                return new Clip(
                    repoClip.Id,
                    repoClip.OwnerId,
                    repoClip.VideoId,
                    categoryEnum,
                    v,
                    repoClip.ClipTags.Count > 0 ? repoClip.ClipTags.Select(ct => ct.Tag.Name).ToList() : [],
                    viewedClipIds.Contains(repoClip.Id)
                );
            })
            .Where(c => c != null)
            .Cast<Clip>()
            .ToList();

        int totalPages = (int)Math.Ceiling((double)pagedResponse.TotalItems / pagedResponse.ItemsPerPage);
        return new PagedClipsResponse(clips, totalPages);
    }

    public List<ClipCategory> GetCategories()
    {
        return [new("Apex Legends", ClipCategoryEnum.ApexLegends, "/images/apex_legends.jpg"),
            new ("Call of Duty: Warzone", ClipCategoryEnum.CallOfDutyWarzone, "/images/callofduty_warzone.jpg"),
            new ("Snowboarding", ClipCategoryEnum.Snowboarding, "/images/snowboarding.png")];
    }

    public async Task<CreateClipResponse?> CreateClip(ClipCategoryEnum categoryEnum, string videoTitle, string discordUserId, string? md5Hash = null)
    {
        DiscordUser discordUser = await dbContext.DiscordUsers.SingleOrDefaultAsync(d => d.DiscordId == discordUserId) ?? throw new InvalidOperationException("No Discord user");
        Guid userId = discordUser.Id;

        // Check if a video with this MD5 hash already exists for this user and category
        if (!string.IsNullOrWhiteSpace(md5Hash))
        {
            bool exists = await dbContext.Clips.AnyAsync(c => c.OwnerId == userId && c.CategoryEnum == categoryEnum && c.Md5Hash == md5Hash);
            if (exists)
                return null; // Duplicate detected
        }

        ClipCollection clipCollection = await dbContext.ClipCollections.SingleOrDefaultAsync(cc => cc.OwnerId == userId && cc.CategoryEnum == categoryEnum) ?? throw new InvalidOperationException("No Clip Collection");
        BunnyVideo video = await bunnyService.CreateVideoAsync(clipCollection.CollectionId, videoTitle);

        Repository.Clip clip = new Repository.Clip { CategoryEnum = categoryEnum, OwnerId = userId, VideoId = video.Guid, Md5Hash = md5Hash };
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

        bool isViewed = await dbContext.ClipViews.AnyAsync(cv => cv.UserId == userId && cv.ClipId == clipId);

        return new Clip(clip.Id, clip.OwnerId, clip.VideoId, clip.CategoryEnum, video, clip.ClipTags.Select(ct => ct.Tag.Name).ToList(), isViewed);
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

    public async Task<Clip?> UpdateClipTitle(Guid clipId, string discordUserId, string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle)) throw new ArgumentException("Title cannot be empty", nameof(newTitle));
        if (newTitle.Length > 200) newTitle = newTitle.Substring(0, 200);

        DiscordUser discordUser = await dbContext.DiscordUsers.SingleOrDefaultAsync(d => d.DiscordId == discordUserId) ?? throw new InvalidOperationException("No Discord user");
        Repository.Clip? clip = await dbContext.Clips.SingleOrDefaultAsync(c => c.Id == clipId && c.OwnerId == discordUser.Id);
        if (clip == null) return null;

        await bunnyService.UpdateVideoTitleAsync(clip.VideoId, newTitle);
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

    public async Task<bool> MarkClipAsViewed(Guid clipId, string discordUserId)
    {
        DiscordUser discordUser = await dbContext.DiscordUsers.SingleOrDefaultAsync(d => d.DiscordId == discordUserId) ?? throw new InvalidOperationException("No Discord user");
        Guid userId = discordUser.Id;

        Repository.Clip? clip = await dbContext.Clips.SingleOrDefaultAsync(c => c.Id == clipId);
        if (clip == null) return false;

        bool alreadyViewed = await dbContext.ClipViews.AnyAsync(cv => cv.UserId == userId && cv.ClipId == clipId);
        if (alreadyViewed) return true;

        ClipView clipView = new ClipView
        {
            UserId = userId,
            ClipId = clipId,
            ViewedAt = DateTime.UtcNow
        };
        await dbContext.ClipViews.AddAsync(clipView);
        await dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<PagedClipsResponse> GetUnviewedClipsForCategory(ClipCategoryEnum categoryEnum, string discordUserId, int page, int pageSize)
    {
        DiscordUser discordUser = await dbContext.DiscordUsers.SingleOrDefaultAsync(d => d.DiscordId == discordUserId) ?? throw new InvalidOperationException("No Discord user");
        Guid userId = discordUser.Id;

        ClipCollection? clipCollection = await dbContext.ClipCollections.SingleOrDefaultAsync(s => s.OwnerId == userId && s.CategoryEnum == categoryEnum);
        if (clipCollection == null) return new PagedClipsResponse([], 0);

        PagedVideoResponse pagedResponse = await bunnyService.GetVideosForCollectionAsync(clipCollection.CollectionId, page, pageSize);

        List<Repository.Clip> repositoryClips = await dbContext.Clips
            .Where(c => c.CategoryEnum == categoryEnum && c.OwnerId == userId)
            .Include(c => c.ClipTags)
            .ThenInclude(ct => ct.Tag)
            .ToListAsync();

        var clipIds = repositoryClips.Select(c => c.Id).ToList();
        var viewedClipIds = dbContext.ClipViews
            .Where(cv => cv.UserId == userId && clipIds.Contains(cv.ClipId))
            .Select(cv => cv.ClipId)
            .ToHashSet();

        var clips = repositoryClips
            .Where(c => !viewedClipIds.Contains(c.Id))
            .Select(c => new Clip(
                c.Id,
                c.OwnerId,
                c.VideoId,
                categoryEnum,
                pagedResponse.Items.First(v => v.Guid == c.VideoId),
                c.ClipTags.Select(ct => ct.Tag.Name).ToList(),
                false
            )).ToList();

        int totalPages = (int)Math.Ceiling((double)pagedResponse.TotalItems / pagedResponse.ItemsPerPage);
        return new PagedClipsResponse(clips, totalPages);
    }

    public async Task<bool> DeleteClip(Guid clipId, string discordUserId)
    {
        DiscordUser discordUser = await dbContext.DiscordUsers.SingleOrDefaultAsync(d => d.DiscordId == discordUserId) ?? throw new InvalidOperationException("No Discord user");

        Repository.Clip? clip = await dbContext.Clips
            .Include(c => c.ClipTags)
            .SingleOrDefaultAsync(c => c.Id == clipId && c.OwnerId == discordUser.Id);
        if (clip == null) return false;

        await bunnyService.DeleteVideoAsync(clip.VideoId);
        dbContext.Clips.Remove(clip);
        await dbContext.SaveChangesAsync();
        return true;
    }
}