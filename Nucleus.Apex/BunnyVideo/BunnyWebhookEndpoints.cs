using Microsoft.AspNetCore.Http.HttpResults;
using Nucleus.Apex.CharacterDetection;
using Nucleus.Data.ApexLegends;
using Nucleus.Data.Clips;

namespace Nucleus.Apex.BunnyVideo;

public static class BunnyWebhookEndpoints
{
    public static void MapBunnyWebhookEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("webhooks/bunny");
        group.MapPost("video-progress", ReceiveVideoProgress).WithName("ReceiveVideoProgress");
    }

    public static async Task<Ok> ReceiveVideoProgress(VideoProgressUpdate update, ClipsStatements clipsStatements, ApexStatements apexStatements, IApexDetectionQueueService queueService)
    {
        if (update.Status == 3)
        {
            ClipsStatements.ClipRow? clip = await clipsStatements.GetClipByVideoId(update.VideoGuid);
            if (clip == null)
            {
                return TypedResults.Ok();
            }

            await apexStatements.InsertApexClipDetection(clip.Id, 0);

            await queueService.QueueDetectionAsync(
                clip.Id,
                GetScreenshotUrlsForVideo(clip.VideoId));
        }


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