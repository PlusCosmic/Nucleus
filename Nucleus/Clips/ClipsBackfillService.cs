using Nucleus.Clips.Bunny;

namespace Nucleus.Clips;

public class ClipsBackfillService(
    ClipsBackfillStatements backfillStatements,
    BunnyService bunnyService,
    ILogger<ClipsBackfillService> logger)
{
    public async Task<BackfillResult> BackfillClipMetadataAsync(int batchSize = 100)
    {
        var clipsNeedingBackfill = await backfillStatements.GetClipsNeedingBackfillAsync(batchSize);

        if (clipsNeedingBackfill.Count == 0)
        {
            logger.LogInformation("No clips need backfilling");
            return new BackfillResult(0, 0, 0);
        }

        logger.LogInformation("Found {Count} clips needing backfill", clipsNeedingBackfill.Count);

        var successCount = 0;
        var failureCount = 0;

        foreach (var clip in clipsNeedingBackfill)
        {
            try
            {
                var bunnyVideo = await bunnyService.GetVideoByIdAsync(clip.VideoId);

                if (bunnyVideo is null)
                {
                    logger.LogWarning("Video {VideoId} for clip {ClipId} not found in Bunny CDN",
                        clip.VideoId, clip.Id);
                    failureCount++;
                    continue;
                }

                await backfillStatements.UpdateClipMetadataAsync(
                    clip.Id,
                    bunnyVideo.Title,
                    bunnyVideo.Length,
                    bunnyVideo.ThumbnailFileName,
                    bunnyVideo.DateUploaded,
                    bunnyVideo.StorageSize,
                    bunnyVideo.Status,
                    bunnyVideo.EncodeProgress
                );

                successCount++;
                logger.LogInformation("Backfilled clip {ClipId} with video {VideoId}",
                    clip.Id, clip.VideoId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to backfill clip {ClipId} with video {VideoId}",
                    clip.Id, clip.VideoId);
                failureCount++;
            }
        }

        logger.LogInformation("Backfill completed: {SuccessCount} succeeded, {FailureCount} failed",
            successCount, failureCount);

        return new BackfillResult(clipsNeedingBackfill.Count, successCount, failureCount);
    }
}

public record BackfillResult(int TotalProcessed, int SuccessCount, int FailureCount);
