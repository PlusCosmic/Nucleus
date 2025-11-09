using Microsoft.AspNetCore.Http.HttpResults;

namespace Nucleus.Apex.CharacterDetection;

public static class ApexDetectionEndpoints
{
    public static void MapApexDetectionEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("api/apexdetection");

        group.MapPost("enqueue", QueueDetection).WithName("QueueDetection");
    }

    public static async Task<Results<Ok, BadRequest<string>>> QueueDetection(
        IApexDetectionQueueService queueService,
        VideoDetectionRequest request)
    {
        if (request.ScreenshotUrls?.Any() != true)
        {
            return TypedResults.BadRequest("No screenshot URLs provided");
        }

        if (request.ScreenshotUrls.Count > 10)
        {
            return TypedResults.BadRequest("Maximum 10 screenshots allowed per request");
        }

        await queueService.QueueDetectionAsync(
            request.ClipId,
            request.ScreenshotUrls);

        return TypedResults.Ok();
    }
}

public sealed record VideoDetectionRequest(Guid ClipId, List<string> ScreenshotUrls);