using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Nucleus.Clips;

public static class ClipsEndpoints
{
    public static void MapClipsEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("clips").RequireAuthorization();
        group.MapGet("categories",  GetCategories).WithName("GetCategories");
        group.MapGet("categories/{category}/videos", GetVideosByCategory).WithName("GetVideosByCategory");
        group.MapPost("categories/{category}/videos", CreateVideo).WithName("CreateVideo");
        group.MapGet("videos/{clipId}", GetVideoById).WithName("GetVideoById");
        group.MapPost("videos/{clipId}/tags", AddTagToClip).WithName("AddTagToClip");
        group.MapDelete("videos/{clipId}/tags/{tag}", RemoveTagFromClip).WithName("RemoveTagFromClip");
        group.MapGet("tags/top", GetTopTags).WithName("GetTopTags");
    }
    
    public static Ok<List<ClipCategory>> GetCategories(ClipService clipService)
    {
        return TypedResults.Ok(clipService.GetCategories());
    }
    
    public static async Task<Results<UnauthorizedHttpResult, Ok<List<Clip>>>> GetVideosByCategory(ClipService clipService, ClipCategoryEnum category, ClaimsPrincipal user, int page)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();
        return TypedResults.Ok(await clipService.GetClipsForCategory(category, discordId, page));
    }

    public static async Task<Results<UnauthorizedHttpResult, Ok<CreateClipResponse>>> CreateVideo(ClipService clipService,
        ClipCategoryEnum category, ClaimsPrincipal user, string videoTitle)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();

        return TypedResults.Ok(await clipService.CreateClip(category, videoTitle, discordId));
    }
    
    public static async Task<Results<UnauthorizedHttpResult, Ok<Clip>, NotFound>> GetVideoById(ClipService clipService, ClaimsPrincipal user, Guid clipId)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();
        Clip? clip = await clipService.GetClipById(clipId, discordId);
        if (clip == null)
            return TypedResults.NotFound();
        return TypedResults.Ok(clip);
    }

    public sealed record AddTagRequest(string Tag);
    
    public static async Task<Results<UnauthorizedHttpResult, Ok<Clip>, NotFound, BadRequest<string>>> AddTagToClip(ClipService clipService, ClaimsPrincipal user, Guid clipId, AddTagRequest request)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();
        try
        {
            Clip? updated = await clipService.AddTagToClip(clipId, discordId, request.Tag);
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
    
    public static async Task<Results<UnauthorizedHttpResult, Ok<Clip>, NotFound>> RemoveTagFromClip(ClipService clipService, ClaimsPrincipal user, Guid clipId, string tag)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();
        Clip? updated = await clipService.RemoveTagFromClip(clipId, discordId, tag);
        if (updated == null) return TypedResults.NotFound();
        return TypedResults.Ok(updated);
    }

    public static async Task<Ok<List<TopTag>>> GetTopTags(ClipService clipService)
    {
        return TypedResults.Ok(await clipService.GetTopTags());
    }
}