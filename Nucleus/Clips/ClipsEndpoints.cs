using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Nucleus.Clips;

public static class ClipsEndpoints
{
    public static void MapClipsEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("clips").RequireAuthorization();
        group.MapGet("categories", GetCategories).WithName("GetCategories");
        group.MapGet("categories/{category}/videos", GetVideosByCategory).WithName("GetVideosByCategory");
        group.MapPost("categories/{category}/videos", CreateVideo).WithName("CreateVideo");
        group.MapGet("videos/{clipId}", GetVideoById).WithName("GetVideoById");
        group.MapPost("videos/{clipId}/view", MarkVideoAsViewed).WithName("MarkVideoAsViewed");
        group.MapPost("videos/{clipId}/tags", AddTagToClip).WithName("AddTagToClip");
        group.MapDelete("videos/{clipId}/tags/{tag}", RemoveTagFromClip).WithName("RemoveTagFromClip");
        group.MapGet("tags/top", GetTopTags).WithName("GetTopTags");
        group.MapPatch("videos/{clipId}/title", UpdateClipTitle).WithName("UpdateClipTitle");
        group.MapDelete("videos/{clipId}", DeleteClip).WithName("DeleteClip");
        group.MapPost("backfill-metadata", BackfillClipMetadata).WithName("BackfillClipMetadata");
    }

    public static Ok<List<ClipCategory>> GetCategories(ClipService clipService)
    {
        return TypedResults.Ok(clipService.GetCategories());
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok<PagedClipsResponse>>> GetVideosByCategory(
        ClipService clipService,
        ClipCategoryEnum category,
        ClaimsPrincipal user,
        int page,
        int pageSize,
        string[]? tags = null,
        string? titleSearch = null,
        bool unviewedOnly = false,
        ClipSortOrder sortOrder = ClipSortOrder.DateDescending,
        DateTimeOffset? startDate = null,
        DateTimeOffset? endDate = null)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        List<string>? tagList = tags?.ToList();
        return TypedResults.Ok(
            await clipService.GetClipsForCategory(category, discordId, page, pageSize, tagList, titleSearch, unviewedOnly, sortOrder, startDate, endDate));
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok<CreateClipResponse>, Conflict<string>>> CreateVideo(
        ClipService clipService,
        ClipCategoryEnum category, ClaimsPrincipal user, string videoTitle, DateTimeOffset? createdAt = null,
        string? md5Hash = null)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        CreateClipResponse? result = await clipService.CreateClip(category, videoTitle, discordId, createdAt ?? DateTimeOffset.UtcNow,
            md5Hash);
        if (result == null)
        {
            return TypedResults.Conflict("A video with this MD5 hash already exists");
        }

        return TypedResults.Ok(result);
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok<Clip>, NotFound>> GetVideoById(ClipService clipService,
        ClaimsPrincipal user, Guid clipId)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        Clip? clip = await clipService.GetClipById(clipId, discordId);
        if (clip == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(clip);
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok<Clip>, NotFound>> AddTagToClip(
        ClipService clipService, ClaimsPrincipal user, Guid clipId, AddTagRequest request)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        Clip? updated = await clipService.AddTagToClip(clipId, discordId, request.Tag);
        if (updated == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(updated);
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok<Clip>, NotFound>> RemoveTagFromClip(
        ClipService clipService, ClaimsPrincipal user, Guid clipId, string tag)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        Clip? updated = await clipService.RemoveTagFromClip(clipId, discordId, tag);
        if (updated == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(updated);
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok<Clip>, NotFound>> UpdateClipTitle(
        ClipService clipService, ClaimsPrincipal user, Guid clipId, UpdateTitleRequest request)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        Clip? updated = await clipService.UpdateClipTitle(clipId, discordId, request.Title);
        if (updated == null)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(updated);
    }

    public static async Task<Ok<List<TopTag>>> GetTopTags(ClipService clipService)
    {
        return TypedResults.Ok(await clipService.GetTopTags());
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok, NotFound>> MarkVideoAsViewed(ClipService clipService,
        ClaimsPrincipal user, Guid clipId)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        bool success = await clipService.MarkClipAsViewed(clipId, discordId);
        if (!success)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok();
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok, NotFound>> DeleteClip(ClipService clipService,
        ClaimsPrincipal user, Guid clipId)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        bool success = await clipService.DeleteClip(clipId, discordId);
        if (!success)
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok();
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok<BackfillResult>>> BackfillClipMetadata(
        ClipsBackfillService backfillService,
        ClaimsPrincipal user)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
        {
            return TypedResults.Unauthorized();
        }

        BackfillResult result = await backfillService.BackfillClipMetadataAsync();
        return TypedResults.Ok(result);
    }

    public sealed record AddTagRequest(string Tag);

    public sealed record UpdateTitleRequest(string Title);
}

public enum ClipSortOrder
{
    DateDescending,
    DateAscending
}