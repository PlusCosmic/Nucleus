using Nucleus.ApexLegends.Models;

namespace Nucleus.Apex.CharacterDetection;

public class DetectionResult
{
    public string TaskId { get; set; }
    public string VideoId { get; set; }
    public string Status { get; set; } // pending, processing, completed, failed
    public List<CharacterDetection> Detections { get; set; } = new();
    public CharacterDetection? BestOverall { get; set; }
    public List<string> UniqueCharacters { get; set; } = new();
    public int TotalScreenshots { get; set; }
    public int SuccessfulDetections { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Error { get; set; }

    public ClipDetectionStatus GetStatus()
    {
        switch (Status)
        {
            case "pending":
                return ClipDetectionStatus.NotStarted;
            case "processing":
                return ClipDetectionStatus.InProgress;
            case "completed":
                return ClipDetectionStatus.Completed;
            case "failed":
                return ClipDetectionStatus.Failed;
            default:
                throw new ArgumentException("Invalid status value");
        }
    }
}

public class CharacterDetection
{
    public string CharacterName { get; set; }
    public float Confidence { get; set; }
    public int ScreenshotIndex { get; set; }
    public string ScreenshotUrl { get; set; }
}