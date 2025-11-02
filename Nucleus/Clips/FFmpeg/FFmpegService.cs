using System.Diagnostics;
using System.Text;

namespace Nucleus.Clips.FFmpeg;

public class FFmpegService
{
    private readonly ILogger<FFmpegService> _logger;
    private readonly string _sharedVolumePath;

    public FFmpegService(ILogger<FFmpegService> logger, IConfiguration configuration)
    {
        _logger = logger;
        // This path should be accessible to both the nucleus container and shared with ffmpeg
        _sharedVolumePath = configuration["FFmpegSharedPath"] ?? "/tmp/ffmpeg-downloads";
    }

    /// <summary>
    /// Downloads a video from an HLS playlist URL and returns the file path
    /// </summary>
    /// <param name="videoId">The Bunny video ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to the downloaded video file</returns>
    public async Task<string> DownloadHlsVideoAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        string hlsUrl = $"https://vz-cd8f9809-39a.b-cdn.net/{videoId}/playlist.m3u8";
        string outputFileName = $"{videoId}.mp4";
        string hostOutputPath = Path.Combine(_sharedVolumePath, outputFileName);
        string containerOutputPath = $"/config/{outputFileName}";

        try
        {
            _logger.LogInformation("Starting HLS download for video {VideoId} from {HlsUrl}", videoId, hlsUrl);

            // Ensure output directory exists on the host
            Directory.CreateDirectory(_sharedVolumePath);

            // Build docker exec command to run ffmpeg in the container
            // The ffmpeg container mounts a volume at /config, so we output there
            var ffmpegCommand = new StringBuilder();
            ffmpegCommand.Append($"exec ffmpeg ");
            ffmpegCommand.Append($"-i \"{hlsUrl}\" ");
            ffmpegCommand.Append("-c copy ");
            ffmpegCommand.Append("-bsf:a aac_adtstoasc ");
            ffmpegCommand.Append($"\"{containerOutputPath}\"");

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = ffmpegCommand.ToString(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);
                    _logger.LogDebug("FFmpeg output: {Output}", e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    errorBuilder.AppendLine(e.Data);
                    _logger.LogDebug("FFmpeg stderr: {Error}", e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                string errorOutput = errorBuilder.ToString();
                _logger.LogError("FFmpeg process failed with exit code {ExitCode}. Error: {Error}",
                    process.ExitCode, errorOutput);
                throw new InvalidOperationException($"FFmpeg failed with exit code {process.ExitCode}: {errorOutput}");
            }

            // Wait a moment for the file to be fully written
            await Task.Delay(500, cancellationToken);

            if (!File.Exists(hostOutputPath))
            {
                _logger.LogError("FFmpeg completed but output file not found at {OutputPath}", hostOutputPath);
                throw new FileNotFoundException("Output file was not created", hostOutputPath);
            }

            _logger.LogInformation("Successfully downloaded video {VideoId} to {OutputPath}", videoId, hostOutputPath);
            return hostOutputPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading HLS video {VideoId}", videoId);

            // Clean up partial file if it exists
            if (File.Exists(hostOutputPath))
            {
                try
                {
                    File.Delete(hostOutputPath);
                }
                catch (Exception deleteEx)
                {
                    _logger.LogWarning(deleteEx, "Failed to delete partial file {OutputPath}", hostOutputPath);
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Deletes a downloaded video file
    /// </summary>
    /// <param name="filePath">Path to the file to delete</param>
    public void DeleteDownloadedVideo(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted video file {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete video file {FilePath}", filePath);
        }
    }
}
