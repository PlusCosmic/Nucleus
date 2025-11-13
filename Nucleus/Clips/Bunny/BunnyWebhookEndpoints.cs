using Microsoft.AspNetCore.Http.HttpResults;
using Nucleus.Apex.BunnyVideo;
using Nucleus.ApexLegends;
using Nucleus.ApexLegends.LegendDetection;

namespace Nucleus.Clips.Bunny;

public static class BunnyWebhookEndpoints
{
    public static void MapBunnyWebhookEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("webhooks/bunny");
        group.MapPost("video-progress", ReceiveVideoProgress).WithName("ReceiveVideoProgress");
    }

    public static async Task<Results<Ok, UnauthorizedHttpResult>> ReceiveVideoProgress(
        VideoProgressUpdate update,
        ClipsStatements clipsStatements,
        ApexStatements apexStatements,
        IApexDetectionQueueService queueService,
        BunnyService bunnyService,
        ClipsBackfillStatements backfillStatements,
        IConfiguration configuration,
        HttpContext context)
    {
        // Validate webhook secret for security
        var expectedSecret = configuration["BunnyWebhookSecret"];
        var providedSecret = context.Request.Headers["X-Webhook-Secret"].FirstOrDefault()
                           ?? context.Request.Query["secret"].FirstOrDefault();

        if (!string.IsNullOrEmpty(expectedSecret) && expectedSecret != providedSecret)
        {
            return TypedResults.Unauthorized();
        }

        ClipsStatements.ClipRow? clip = await clipsStatements.GetClipByVideoId(update.VideoGuid);
        if (clip == null)
        {
            return TypedResults.Ok();
        }

        if (update.Status == 3)
        {
            await apexStatements.InsertApexClipDetection(clip.Id, 0);

            await queueService.QueueDetectionAsync(
                clip.Id,
                GetScreenshotUrlsForVideo(clip.VideoId));
        }

        // fetch clip from bunny and update our model in the db
        Clips.Bunny.Models.BunnyVideo? video = await bunnyService.GetVideoByIdAsync(update.VideoGuid);
        if (video == null)
        {
            return TypedResults.Ok();
        }

        await backfillStatements.UpdateClipMetadataAsync(clip.Id, clip?.Title ?? video.Title, video.Length, video.ThumbnailFileName, video.DateUploaded, video.StorageSize, video.Status,
            video.EncodeProgress);


        return TypedResults.Ok();
    }

    private static List<string> GetScreenshotUrlsForVideo(Guid videoId)
    {
        return
        [
            $"https://vz-cd8f9809-39a.b-cdn.net/{videoId.ToString()}/thumbnail.jpg",
            $"https://vz-cd8f9809-39a.b-cdn.net/{videoId.ToString()}/thumbnail_1.jpg",
            $"https://vz-cd8f9809-39a.b-cdn.net/{videoId.ToString()}/thumbnail_2.jpg",
            $"https://vz-cd8f9809-39a.b-cdn.net/{videoId.ToString()}/thumbnail_3.jpg",
            $"https://vz-cd8f9809-39a.b-cdn.net/{videoId.ToString()}/thumbnail_4.jpg",
            $"https://vz-cd8f9809-39a.b-cdn.net/{videoId.ToString()}/thumbnail_5.jpg"
        ];
    }
}