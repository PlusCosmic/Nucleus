using System.Security.Cryptography;
using System.Text;
using Nucleus.ApexLegends;
using Nucleus.ApexLegends.Models;
using Nucleus.Clips.Bunny;
using Nucleus.Clips.Bunny.Models;
using Nucleus.Discord;
using Nucleus.Exceptions;

namespace Nucleus.Clips;

public class ClipService(
    BunnyService bunnyService,
    ClipsStatements clipsStatements,
    DiscordStatements discordStatements,
    ApexStatements apexStatements,
    IConfiguration configuration)
{
    private static string NormalizeTag(string tag)
    {
        return tag.Trim().ToLowerInvariant();
    }

    public List<ClipCategory> GetCategories()
    {
        return
        [
            new ClipCategory("Apex Legends", ClipCategoryEnum.ApexLegends, "/images/apex_legends.jpg"),
            new ClipCategory("Call of Duty: Warzone", ClipCategoryEnum.CallOfDutyWarzone,
                "/images/callofduty_warzone.jpg"),
            new ClipCategory("Snowboarding", ClipCategoryEnum.Snowboarding, "/images/snowboarding.png")
        ];
    }

    public async Task<CreateClipResponse?> CreateClip(ClipCategoryEnum categoryEnum, string videoTitle,
        string discordUserId, DateTimeOffset createdAt, string? md5Hash = null)
    {
        DiscordStatements.DiscordUserRow discordUser = await discordStatements.GetUserByDiscordId(discordUserId)
                                                       ?? throw new UnauthorizedException("User not found");
        Guid userId = discordUser.Id;

        // Check if a video with this MD5 hash already exists for this user and category
        if (!string.IsNullOrWhiteSpace(md5Hash))
        {
            bool exists = await clipsStatements.ClipExistsByMd5Hash(userId, (int)categoryEnum, md5Hash);
            if (exists)
            {
                return null; // Duplicate detected
            }
        }

        // Get or create collection (same pattern as GetClipsForCategory)
        ClipsStatements.ClipCollectionRow? clipCollection =
            await clipsStatements.GetCollectionByOwnerAndCategory(userId, (int)categoryEnum);
        if (clipCollection == null)
        {
            BunnyCollection bunnyCollection = await bunnyService.CreateCollectionAsync(categoryEnum, userId);
            clipCollection = await clipsStatements.InsertCollection(userId, bunnyCollection.Guid, (int)categoryEnum);
        }

        BunnyVideo video = await bunnyService.CreateVideoAsync(clipCollection.CollectionId, videoTitle);

        await clipsStatements.InsertClip(
            userId,
            video.Guid,
            (int)categoryEnum,
            md5Hash,
            createdAt,
            video.Title,
            video.Length,
            video.ThumbnailFileName,
            video.DateUploaded,
            video.StorageSize,
            video.Status,
            video.EncodeProgress);

        long expiration = DateTimeOffset.Now.AddHours(1).ToUnixTimeSeconds();
        string libraryId = configuration["BunnyLibraryId"]
                           ?? throw new InvalidOperationException("Bunny API library ID not configured");
        string secretKey = configuration["BunnyAccessKey"]
                           ?? throw new InvalidOperationException("Bunny access key not configured");
        byte[] signature = Encoding.UTF8.GetBytes(libraryId + secretKey + expiration + video.Guid);
        byte[] hash = SHA256.HashData(signature);
        string? hashString = Convert.ToHexString(hash);
        return new CreateClipResponse(
            hashString ?? throw new InvalidOperationException("Hash computation failed"),
            expiration,
            libraryId,
            video.Guid,
            video.CollectionId);
    }

    public async Task<Clip?> GetClipById(Guid clipId, string discordUserId)
    {
        DiscordStatements.DiscordUserRow discordUser = await discordStatements.GetUserByDiscordId(discordUserId)
                                                       ?? throw new UnauthorizedException("User not found");
        Guid userId = discordUser.Id;

        ClipsStatements.ClipWithTagsRow? clipWithTags = await clipsStatements.GetClipWithTagsById(clipId);
        if (clipWithTags == null)
        {
            return null;
        }

        // Get clip collection to retrieve CollectionId
        ClipsStatements.ClipCollectionRow? clipCollection =
            await clipsStatements.GetCollectionByOwnerAndCategory(clipWithTags.OwnerId, clipWithTags.Category);
        if (clipCollection == null)
        {
            return null;
        }

        string libraryId = configuration["BunnyLibraryId"]
                           ?? throw new InvalidOperationException("Bunny API library ID not configured");
        int videoLibraryId = int.Parse(libraryId);

        // Create BunnyVideo from database metadata (no need to fetch from Bunny CDN)
        BunnyVideo video = new(
            videoLibraryId,
            clipWithTags.VideoId,
            clipWithTags.Title ?? "Untitled",
            clipWithTags.DateUploaded ?? DateTimeOffset.UtcNow,
            clipWithTags.Length ?? 0,
            clipWithTags.VideoStatus ?? 0,
            0,
            0,
            clipWithTags.EncodeProgress ?? 0,
            clipWithTags.StorageSize ?? 0,
            clipCollection.CollectionId,
            clipWithTags.ThumbnailFileName ?? string.Empty,
            string.Empty,
            ((ClipCategoryEnum)clipWithTags.Category).ToString(),
            [],
            []
        );

        bool isViewed = await clipsStatements.IsClipViewed(userId, clipId);

        List<string> tags = !string.IsNullOrEmpty(clipWithTags.TagNames)
            ? clipWithTags.TagNames.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
            : new List<string>();

        ApexStatements.ApexClipDetectionRow clipDetection = await apexStatements.GetApexClipDetection(clipWithTags.Id);

        return new Clip(clipWithTags.Id, clipWithTags.OwnerId, clipWithTags.VideoId,
            (ClipCategoryEnum)clipWithTags.Category, clipWithTags.CreatedAt, video, tags, isViewed, clipDetection.GetPrimaryDetection(), GetLegendCard(clipDetection.GetPrimaryDetection()));
    }

    public async Task<Clip?> AddTagToClip(Guid clipId, string discordUserId, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new BadRequestException("Tag cannot be empty");
        }

        tag = NormalizeTag(tag);
        if (tag.Length > 32)
        {
            tag = tag.Substring(0, 32);
        }

        DiscordStatements.DiscordUserRow discordUser = await discordStatements.GetUserByDiscordId(discordUserId)
                                                       ?? throw new UnauthorizedException("User not found");

        ClipsStatements.ClipWithTagsRow? clipWithTags =
            await clipsStatements.GetClipWithTagsByIdAndOwner(clipId, discordUser.Id);
        if (clipWithTags == null)
        {
            return null;
        }

        List<string> existingTags = !string.IsNullOrEmpty(clipWithTags.TagNames)
            ? clipWithTags.TagNames.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList()
            : new List<string>();

        if (existingTags.Contains(tag))
        {
            return await GetClipById(clipId, discordUserId); // already tagged
        }

        if (existingTags.Count >= 5)
        {
            throw new BadRequestException("A clip can have a maximum of 5 tags");
        }

        ClipsStatements.TagRow? tagEntity = await clipsStatements.GetTagByName(tag);
        if (tagEntity == null)
        {
            tagEntity = await clipsStatements.InsertTag(tag);
        }

        await clipsStatements.InsertClipTag(clipId, tagEntity.Id);

        return await GetClipById(clipId, discordUserId);
    }

    public async Task<Clip?> RemoveTagFromClip(Guid clipId, string discordUserId, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            throw new BadRequestException("Tag cannot be empty");
        }

        tag = NormalizeTag(tag);

        DiscordStatements.DiscordUserRow discordUser = await discordStatements.GetUserByDiscordId(discordUserId)
                                                       ?? throw new UnauthorizedException("User not found");

        ClipsStatements.ClipWithTagsRow? clipWithTags =
            await clipsStatements.GetClipWithTagsByIdAndOwner(clipId, discordUser.Id);
        if (clipWithTags == null)
        {
            return null;
        }

        ClipsStatements.TagRow? tagEntity = await clipsStatements.GetTagByNameForClip(clipId, tag);
        if (tagEntity != null)
        {
            await clipsStatements.DeleteClipTag(clipId, tagEntity.Id);
        }

        return await GetClipById(clipId, discordUserId);
    }

    public async Task<Clip?> UpdateClipTitle(Guid clipId, string discordUserId, string newTitle)
    {
        if (string.IsNullOrWhiteSpace(newTitle))
        {
            throw new BadRequestException("Title cannot be empty");
        }

        if (newTitle.Length > 200)
        {
            newTitle = newTitle.Substring(0, 200);
        }

        DiscordStatements.DiscordUserRow discordUser = await discordStatements.GetUserByDiscordId(discordUserId)
                                                       ?? throw new UnauthorizedException("User not found");

        ClipsStatements.ClipWithTagsRow? clip =
            await clipsStatements.GetClipWithTagsByIdAndOwner(clipId, discordUser.Id);
        if (clip == null)
        {
            return null;
        }

        await clipsStatements.UpdateClipTitle(clipId, newTitle);
        return await GetClipById(clipId, discordUserId);
    }

    public async Task<List<TopTag>> GetTopTags(int limit = 20)
    {
        List<ClipsStatements.TopTagRow> topTagRows = await clipsStatements.GetTopTags(limit);
        return topTagRows.Select(t => new TopTag(t.Name, t.Count)).ToList();
    }

    public async Task<bool> MarkClipAsViewed(Guid clipId, string discordUserId)
    {
        DiscordStatements.DiscordUserRow discordUser = await discordStatements.GetUserByDiscordId(discordUserId)
                                                       ?? throw new UnauthorizedException("User not found");
        Guid userId = discordUser.Id;

        ClipsStatements.ClipRow? clip = await clipsStatements.GetClipById(clipId);
        if (clip == null)
        {
            return false;
        }

        bool alreadyViewed = await clipsStatements.IsClipViewed(userId, clipId);
        if (alreadyViewed)
        {
            return true;
        }

        await clipsStatements.InsertClipView(userId, clipId);
        return true;
    }

    public async Task<PagedClipsResponse> GetClipsForCategory(ClipCategoryEnum categoryEnum,
        string discordUserId, int page, int pageSize, List<string>? tags = null, string? titleSearch = null,
        bool unviewedOnly = false)
    {
        DiscordStatements.DiscordUserRow discordUser = await discordStatements.GetUserByDiscordId(discordUserId)
                                                       ?? throw new UnauthorizedException("User not found");
        Guid userId = discordUser.Id;

        ClipsStatements.ClipCollectionRow? clipCollection =
            await clipsStatements.GetCollectionByOwnerAndCategory(userId, (int)categoryEnum);
        if (clipCollection == null)
        {
            return new PagedClipsResponse([], 0, 0);
        }

        // Normalize tags for filtering (same as when adding tags)
        List<string>? normalizedTags = tags?.Select(NormalizeTag).ToList();

        List<ClipsStatements.ClipWithTagsRow> clipsWithTags =
            await clipsStatements.GetClipsWithTagsByOwnerAndCategory(userId, (int)categoryEnum, normalizedTags);

        // Apply title search filter if provided
        if (!string.IsNullOrWhiteSpace(titleSearch))
        {
            clipsWithTags = clipsWithTags
                .Where(c => c.Title != null && c.Title.Contains(titleSearch, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        List<Guid> clipIds = clipsWithTags.Select(c => c.Id).ToList();
        HashSet<Guid> viewedClipIds = await clipsStatements.GetViewedClipIds(userId, clipIds);

        if (unviewedOnly)
        {
            clipsWithTags.RemoveAll(c => viewedClipIds.Contains(c.Id));
        }

        clipsWithTags = clipsWithTags.OrderByDescending(c => c.CreatedAt).ToList();

        // get correct page from filtered list
        List<ClipsStatements.ClipWithTagsRow> pagedClips = clipsWithTags.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        int totalPages = (int)Math.Ceiling((double)clipsWithTags.Count / pageSize);

        List<Guid> pagedClipIds = pagedClips.Select(c => c.Id).ToList();
        List<ApexStatements.ApexClipDetectionRow> detections = await apexStatements.GetApexClipDetectionsByClipIds(pagedClipIds);

        string libraryId = configuration["BunnyLibraryId"]
                           ?? throw new InvalidOperationException("Bunny API library ID not configured");
        int videoLibraryId = int.Parse(libraryId);

        List<Clip> finalClips = pagedClips.Select(c =>
        {
            // Create BunnyVideo from database metadata (no need to fetch from Bunny CDN)
            BunnyVideo video = new(
                videoLibraryId,
                c.VideoId,
                c.Title ?? "Untitled",
                c.DateUploaded ?? DateTimeOffset.UtcNow,
                c.Length ?? 0,
                c.VideoStatus ?? 0,
                0, // Not stored in DB, not critical for display
                0, // Not stored in DB, not critical for display
                c.EncodeProgress ?? 0,
                c.StorageSize ?? 0,
                clipCollection.CollectionId,
                c.ThumbnailFileName ?? string.Empty,
                string.Empty, // Not stored in DB
                categoryEnum.ToString(),
                [],
                []
            );

            ApexStatements.ApexClipDetectionRow? detection = detections.FirstOrDefault(d => d.ClipId == c.Id);

            return new Clip(c.Id, c.OwnerId, c.VideoId, categoryEnum, c.CreatedAt, video,
                c.TagNames?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? [],
                viewedClipIds.Contains(c.Id), detection?.GetPrimaryDetection() ?? ApexLegend.None, GetLegendCard(detection?.GetPrimaryDetection() ?? ApexLegend.None));
        }).ToList();

        PagedClipsResponse pagedClipsResponse = new(finalClips, clipsWithTags.Count, totalPages);
        return pagedClipsResponse;
    }

    public async Task<bool> DeleteClip(Guid clipId, string discordUserId)
    {
        DiscordStatements.DiscordUserRow discordUser = await discordStatements.GetUserByDiscordId(discordUserId) ?? throw new UnauthorizedException("User not found");

        ClipsStatements.ClipWithTagsRow? clip =
            await clipsStatements.GetClipWithTagsByIdAndOwner(clipId, discordUser.Id);
        if (clip == null)
        {
            return false;
        }

        await bunnyService.DeleteVideoAsync(clip.VideoId);
        await clipsStatements.DeleteClip(clipId);
        return true;
    }

    private string GetLegendCard(ApexLegend legend)
    {
        if (legend == ApexLegend.MadMaggie)
        {
            return "/images/apex-MadMaggie.webp";
        }

        return $"/images/apex-{legend.ToString()}.webp";
    }
}