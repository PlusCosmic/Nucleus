using Newtonsoft.Json;
using Nucleus.Data.ApexLegends;
using Nucleus.Data.ApexLegends.Models;
using StackExchange.Redis;

namespace Nucleus.Apex.CharacterDetection;

public interface IApexDetectionQueueService
{
    Task<Guid> QueueDetectionAsync(Guid clipId, List<string> screenshotUrls);
    Task<DetectionResult?> GetTaskResultAsync(Guid taskId);
}

public class ApexDetectionQueueService(IConnectionMultiplexer redis, ILogger<ApexDetectionQueueService> logger, ApexStatements apexStatements) : IApexDetectionQueueService
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<DetectionResult?> GetTaskResultAsync(Guid taskId)
    {
        RedisValue resultJson = await _db.StringGetAsync($"result:{taskId.ToString()}");
        return resultJson.IsNullOrEmpty ? null : JsonConvert.DeserializeObject<DetectionResult>(resultJson);
    }

    public async Task<Guid> QueueDetectionAsync(Guid clipId, List<string> screenshotUrls)
    {
        Guid taskId = Guid.NewGuid();

        var celeryMessage = new
        {
            body = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                JsonConvert.SerializeObject(new object[]
                {
                    new object[] { taskId.ToString(), clipId.ToString(), screenshotUrls },
                    new { },
                    new { callbacks = (object?)null, errbacks = (object?)null, chain = (object?)null, chord = (object?)null }
                })
            )),
            headers = new
            {
                lang = "c#",
                task = "tasks.process_video_screenshots",
                id = taskId.ToString(),
                root_id = taskId.ToString(),
                parent_id = (string?)null,
                group = (string?)null
            },
            properties = new
            {
                correlation_id = taskId.ToString(),
                reply_to = Guid.NewGuid().ToString(),
                delivery_mode = 2,
                delivery_info = new { exchange = "", routing_key = "apex_detection_queue" },
                priority = 0,
                body_encoding = "base64",
                delivery_tag = Guid.NewGuid().ToString()
            },
            content_encoding = "utf-8",
            content_type = "application/json"
        };

        await _db.ListLeftPushAsync("apex_detection_queue", JsonConvert.SerializeObject(celeryMessage));
        await apexStatements.SetApexClipDetectionTaskId(clipId, taskId);
        await apexStatements.SetApexClipDetectionStatus(clipId, (int)ClipDetectionStatus.InProgress);
        logger.LogInformation("Queued task {TaskId} for clip {ClipId}", taskId, clipId);
        return taskId;
    }
}