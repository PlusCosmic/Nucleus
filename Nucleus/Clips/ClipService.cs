using System.Security.Cryptography;
using System.Text;
using Nucleus.Clips.Bunny;
using Nucleus.Clips.Bunny.Models;
using Nucleus.Discord;

namespace Nucleus.Clips;

public class ClipService(
    BunnyService bunnyService,
    ClipsStatements clipsStatements,
    DiscordStatements discordStatements,
    IConfiguration configuration)
{
    private static string NormalizeTag(string tag)
    {
        return tag.Trim().ToLowerInvariant();
    }

    public async Task<PagedClipsResponse> GetClipsForCategory(ClipCategoryEnum categoryEnum, string discordUserId, int page, int pageSize)
    {
        var discordUser = await discordStatements.GetUserByDiscordId(discordUserId)
            ?? throw new InvalidOperationException("No Discord user");
        Guid userId = discordUser.Id;

        // get collection
        var clipCollection = await clipsStatements.GetCollectionByOwnerAndCategory(userId, (int)categoryEnum);
        if (clipCollection == null)
        {
            BunnyCollection bunnyCollection = await bunnyService.CreateCollectionAsync(categoryEnum, userId);
            clipCollection = await clipsStatements.InsertCollection(userId, bunnyCollection.Guid, (int)categoryEnum);
            // If the collection didn't exist, there won't be any videos in it
            return new PagedClipsResponse([], 0);
        }

        // get videos from bunny
        PagedVideoResponse pagedResponse = await bunnyService.GetVideosForCollectionAsync(clipCollection.CollectionId, page, pageSize);

        // get clips from db including tags
        var clipsWithTags = await clipsStatements.GetClipsWithTagsByOwnerAndCategory(userId, (int)categoryEnum);

        // get viewed clips for this user
        var clipIds = clipsWithTags.Select(c => c.Id).ToList();
        var viewedClipIds = await clipsStatements.GetViewedClipIds(userId, clipIds);

        var repoClipsByVideoId = clipsWithTags.ToDictionary(c => c.VideoId, c => c);
        var clips = pagedResponse.Items
            .Select(v =>
            {
                if (!repoClipsByVideoId.TryGetValue(v.Guid, out var repoClip)) return null;
                var tags = !string.IsNullOrEmpty(repoClip.TagNames)
                    ? repoClip.TagNames.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    : new List<string>();

                return new Clip(
                    repoClip.Id,
                    repoClip.OwnerId,
                    repoClip.VideoId,
                    categoryEnum,
                    v,
                    tags,
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
        var discordUser = await discordStatements.GetUserByDiscordId(discordUserId)
            ?? throw new InvalidOperationException("No Discord user");
        Guid userId = discordUser.Id;

        // Check if a video with this MD5 hash already exists for this user and category
        if (!string.IsNullOrWhiteSpace(md5Hash))
        {
            bool exists = await clipsStatements.ClipExistsByMd5Hash(userId, (int)categoryEnum, md5Hash);
            if (exists)
                return null; // Duplicate detected
        }

        var clipCollection = await clipsStatements.GetCollectionByOwnerAndCategory(userId, (int)categoryEnum)
            ?? throw new InvalidOperationException("No Clip Collection");

        BunnyVideo video = await bunnyService.CreateVideoAsync(clipCollection.CollectionId, videoTitle);

        await clipsStatements.InsertClip(userId, video.Guid, (int)categoryEnum, md5Hash);

        long expiration = DateTimeOffset.Now.AddHours(1).ToUnixTimeSeconds();
        string libraryId = configuration["BunnyLibraryId"]
            ?? throw new InvalidOperationException("Bunny API library ID not configured");
        string secretKey = configuration["BunnyAccessKey"]
            ?? throw new InvalidOperationException("Bunny access key not configured");
        byte[] signature = Encoding.UTF8.GetBytes(libraryId + secretKey + expiration + video.Guid);
        var hash = SHA256.HashData(signature);
        string hashString = Convert.ToHexString(hash);
        return new CreateClipResponse(
            hashString ?? throw new InvalidOperationException("Hash computation failed"),
            expiration,
            libraryId,
            video.Guid,
            video.CollectionId);
    }
    
    public async Task<Clip?> GetClipById(Guid clipId, string discordUserId)
    {
        var discordUser = await discordStatements.GetUserByDiscordId(discordUserId)
            ?? throw new InvalidOperationException("No Discord user");
        Guid userId = discordUser.Id;

        var clipWithTags = await clipsStatements.GetClipWithTagsById(clipId);
        if (clipWithTags == null)
            return null;

        BunnyVideo? video = await bunnyService.GetVideoByIdAsync(clipWithTags.VideoId);
        if (video == null)
            return null;

        bool isViewed = await clipsStatements.IsClipViewed(userId, clipId);

        var tags = !string.IsNullOrEmpty(clipWithTags.TagNames)
            ? clipWithTags.TagNames.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
            : new List<string>();

        return new Clip(clipWithTags.Id, clipWithTags.OwnerId, clipWithTags.VideoId, (ClipCategoryEnum)clipWithTags.Category, video, tags, isViewed);
    }

    public async Task<Clip?> AddTagToClip(Guid clipId, string discordUserId, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("Tag cannot be empty", nameof(tag));
        tag = NormalizeTag(tag);
        if (tag.Length > 32) tag = tag.Substring(0, 32);

        var discordUser = await discordStatements.GetUserByDiscordId(discordUserId)
            ?? throw new InvalidOperationException("No Discord user");

        var clipWithTags = await clipsStatements.GetClipWithTagsByIdAndOwner(clipId, discordUser.Id);
        if (clipWithTags == null) return null;

        var existingTags = !string.IsNullOrEmpty(clipWithTags.TagNames)
            ? clipWithTags.TagNames.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
            : new List<string>();

        if (existingTags.Contains(tag))
            return await GetClipById(clipId, discordUserId); // already tagged
        if (existingTags.Count >= 5)
            throw new InvalidOperationException("A clip can have up to 5 tags");

        var tagEntity = await clipsStatements.GetTagByName(tag);
        if (tagEntity == null)
        {
            tagEntity = await clipsStatements.InsertTag(tag);
        }

        await clipsStatements.InsertClipTag(clipId, tagEntity.Id);

        return await GetClipById(clipId, discordUserId);
    }

    public async Task<Clip?> RemoveTagFromClip(Guid clipId, string discordUserId, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) throw new ArgumentException("Tag cannot be empty", nameof(tag));
        tag = NormalizeTag(tag);

        var discordUser = await discordStatements.GetUserByDiscordId(discordUserId)
            ?? throw new InvalidOperationException("No Discord user");

        var clipWithTags = await clipsStatements.GetClipWithTagsByIdAndOwner(clipId, discordUser.Id);
        if (clipWithTags == null) return null;

        var tagEntity = await clipsStatements.GetTagByNameForClip(clipId, tag);
        if (tagEntity != null)
        {
            await clipsStatements.DeleteClipTag(clipId, tagEntity.Id);
        }
        return await GetClipById(clipId, discordUserId);
    }

    public async Task<Clip?> UpdateClipTitle(Guid clipId, string discordUserId, string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle)) throw new ArgumentException("Title cannot be empty", nameof(newTitle));
        if (newTitle.Length > 200) newTitle = newTitle.Substring(0, 200);

        var discordUser = await discordStatements.GetUserByDiscordId(discordUserId)
            ?? throw new InvalidOperationException("No Discord user");

        var clip = await clipsStatements.GetClipWithTagsByIdAndOwner(clipId, discordUser.Id);
        if (clip == null) return null;

        await bunnyService.UpdateVideoTitleAsync(clip.VideoId, newTitle);
        return await GetClipById(clipId, discordUserId);
    }

    public async Task<List<TopTag>> GetTopTags(int limit = 20)
    {
        var topTagRows = await clipsStatements.GetTopTags(limit);
        return topTagRows.Select(t => new TopTag(t.Name, t.Count)).ToList();
    }

    public async Task<bool> MarkClipAsViewed(Guid clipId, string discordUserId)
    {
        var discordUser = await discordStatements.GetUserByDiscordId(discordUserId)
            ?? throw new InvalidOperationException("No Discord user");
        Guid userId = discordUser.Id;

        var clip = await clipsStatements.GetClipById(clipId);
        if (clip == null) return false;

        bool alreadyViewed = await clipsStatements.IsClipViewed(userId, clipId);
        if (alreadyViewed) return true;

        await clipsStatements.InsertClipView(userId, clipId);
        return true;
    }

    public async Task<PagedClipsResponse> GetUnviewedClipsForCategory(ClipCategoryEnum categoryEnum, string discordUserId, int page, int pageSize)
    {
        var discordUser = await discordStatements.GetUserByDiscordId(discordUserId)
            ?? throw new InvalidOperationException("No Discord user");
        Guid userId = discordUser.Id;

        var clipCollection = await clipsStatements.GetCollectionByOwnerAndCategory(userId, (int)categoryEnum);
        if (clipCollection == null) return new PagedClipsResponse([], 0);

        PagedVideoResponse pagedResponse = await bunnyService.GetVideosForCollectionAsync(clipCollection.CollectionId, page, pageSize);

        var clipsWithTags = await clipsStatements.GetClipsWithTagsByOwnerAndCategory(userId, (int)categoryEnum);

        var clipIds = clipsWithTags.Select(c => c.Id).ToList();
        var viewedClipIds = await clipsStatements.GetViewedClipIds(userId, clipIds);

        var clipsByVideoId = clipsWithTags.ToDictionary(c => c.VideoId, c => c);
        var clips = clipsWithTags
            .Where(c => !viewedClipIds.Contains(c.Id))
            .Select(c =>
            {
                var video = pagedResponse.Items.FirstOrDefault(v => v.Guid == c.VideoId);
                if (video == null) return null;

                var tags = !string.IsNullOrEmpty(c.TagNames)
                    ? c.TagNames.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
                    : new List<string>();

                return new Clip(
                    c.Id,
                    c.OwnerId,
                    c.VideoId,
                    categoryEnum,
                    video,
                    tags,
                    false
                );
            })
            .Where(c => c != null)
            .Cast<Clip>()
            .ToList();

        int totalPages = (int)Math.Ceiling((double)pagedResponse.TotalItems / pagedResponse.ItemsPerPage);
        return new PagedClipsResponse(clips, totalPages);
    }

    public async Task<bool> DeleteClip(Guid clipId, string discordUserId)
    {
        var discordUser = await discordStatements.GetUserByDiscordId(discordUserId)
            ?? throw new InvalidOperationException("No Discord user");

        var clip = await clipsStatements.GetClipWithTagsByIdAndOwner(clipId, discordUser.Id);
        if (clip == null) return false;

        await bunnyService.DeleteVideoAsync(clip.VideoId);
        await clipsStatements.DeleteClip(clipId);
        return true;
    }
}