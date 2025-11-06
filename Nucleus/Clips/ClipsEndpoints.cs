using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Nucleus.Clips;

public static class ClipsEndpoints
{
    public static void MapClipsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("clips").RequireAuthorization();
        group.MapGet("categories", GetCategories).WithName("GetCategories");
        group.MapGet("categories/{category}/videos", GetVideosByCategory).WithName("GetVideosByCategory");
        group.MapGet("categories/{category}/videos/unviewed", GetUnviewedVideosByCategory)
            .WithName("GetUnviewedVideosByCategory");
        group.MapPost("categories/{category}/videos", CreateVideo).WithName("CreateVideo");
        group.MapGet("videos/{clipId}", GetVideoById).WithName("GetVideoById");
        group.MapPost("videos/{clipId}/view", MarkVideoAsViewed).WithName("MarkVideoAsViewed");
        group.MapPost("videos/{clipId}/tags", AddTagToClip).WithName("AddTagToClip");
        group.MapDelete("videos/{clipId}/tags/{tag}", RemoveTagFromClip).WithName("RemoveTagFromClip");
        group.MapGet("tags/top", GetTopTags).WithName("GetTopTags");
        group.MapPatch("videos/{clipId}/title", UpdateClipTitle).WithName("UpdateClipTitle");
        group.MapDelete("videos/{clipId}", DeleteClip).WithName("DeleteClip");
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
        string? tags = null,
        string? titleSearch = null)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();

        // Parse tags from comma-separated string to list
        List<string>? tagList = null;
        if (!string.IsNullOrWhiteSpace(tags))
            tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        return TypedResults.Ok(
            await clipService.GetClipsForCategory(category, discordId, page, pageSize, tagList, titleSearch));
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok<CreateClipResponse>, Conflict<string>>> CreateVideo(
        ClipService clipService,
        ClipCategoryEnum category, ClaimsPrincipal user, string videoTitle, DateTimeOffset? createdAt = null,
        string? md5Hash = null)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();

        var result = await clipService.CreateClip(category, videoTitle, discordId, createdAt ?? DateTimeOffset.UtcNow,
            md5Hash);
        if (result == null)
            return TypedResults.Conflict("A video with this MD5 hash already exists");

        return TypedResults.Ok(result);
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok<Clip>, NotFound>> GetVideoById(ClipService clipService,
        ClaimsPrincipal user, Guid clipId)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();
        var clip = await clipService.GetClipById(clipId, discordId);
        if (clip == null)
            return TypedResults.NotFound();
        return TypedResults.Ok(clip);
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok<Clip>, NotFound, BadRequest<string>>> AddTagToClip(
        ClipService clipService, ClaimsPrincipal user, Guid clipId, AddTagRequest request)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();
        try
        {
            var updated = await clipService.AddTagToClip(clipId, discordId, request.Tag);
            if (updated == null) return TypedResults.NotFound();
            return TypedResults.Ok(updated);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok<Clip>, NotFound>> RemoveTagFromClip(
        ClipService clipService, ClaimsPrincipal user, Guid clipId, string tag)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();
        var updated = await clipService.RemoveTagFromClip(clipId, discordId, tag);
        if (updated == null) return TypedResults.NotFound();
        return TypedResults.Ok(updated);
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok<Clip>, NotFound, BadRequest<string>>> UpdateClipTitle(
        ClipService clipService, ClaimsPrincipal user, Guid clipId, UpdateTitleRequest request)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();
        try
        {
            var updated = await clipService.UpdateClipTitle(clipId, discordId, request.Title);
            if (updated == null) return TypedResults.NotFound();
            return TypedResults.Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.BadRequest(ex.Message);
        }
    }

    public static async Task<Ok<List<TopTag>>> GetTopTags(ClipService clipService)
    {
        return TypedResults.Ok(await clipService.GetTopTags());
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok, NotFound>> MarkVideoAsViewed(ClipService clipService,
        ClaimsPrincipal user, Guid clipId)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();
        var success = await clipService.MarkClipAsViewed(clipId, discordId);
        if (!success) return TypedResults.NotFound();
        return TypedResults.Ok();
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok<PagedClipsResponse>>> GetUnviewedVideosByCategory(
        ClipService clipService,
        ClipCategoryEnum category,
        ClaimsPrincipal user,
        int page,
        int pageSize,
        string? tags = null,
        string? titleSearch = null)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();

        // Parse tags from comma-separated string to list
        List<string>? tagList = null;
        if (!string.IsNullOrWhiteSpace(tags))
            tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        return TypedResults.Ok(await clipService.GetClipsForCategory(category, discordId, page, pageSize, tagList,
            titleSearch, true));
    }


    public static async Task<Results<UnauthorizedHttpResult, Ok, NotFound>> DeleteClip(ClipService clipService,
        ClaimsPrincipal user, Guid clipId)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();
        var success = await clipService.DeleteClip(clipId, discordId);
        if (!success) return TypedResults.NotFound();
        return TypedResults.Ok();
    }

    public sealed record AddTagRequest(string Tag);

    public sealed record UpdateTitleRequest(string Title);
}