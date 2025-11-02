using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Nucleus.Clips.FFmpeg;

public static class FFmpegEndpoints
{
    public static void MapFFmpegEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("ffmpeg").RequireAuthorization();
        group.MapGet("download/{videoId}", DownloadVideo).WithName("DownloadVideo");
    }

    /// <summary>
    /// Downloads a video from Bunny CDN HLS playlist and returns it as a file
    /// </summary>
    public static async Task<Results<UnauthorizedHttpResult, FileStreamHttpResult, NotFound<string>, ProblemHttpResult>> DownloadVideo(
        FFmpegService ffmpegService,
        ClaimsPrincipal user,
        Guid videoId,
        CancellationToken cancellationToken)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return TypedResults.Unauthorized();

        try
        {
            // Download the video using FFmpeg
            string filePath = await ffmpegService.DownloadHlsVideoAsync(videoId, cancellationToken);

            // Open the file stream
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.DeleteOnClose);

            // Return the file with proper content type
            return TypedResults.File(
                fileStream,
                contentType: "video/mp4",
                fileDownloadName: $"{videoId}.mp4",
                enableRangeProcessing: true);
        }
        catch (FileNotFoundException ex)
        {
            return TypedResults.NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            return TypedResults.Problem(
                detail: ex.Message,
                title: "Failed to download video",
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }
}
