using Dapper;
using Npgsql;
using Nucleus.Data.ApexLegends.Models;

namespace Nucleus.Data.ApexLegends;

public class ApexStatements(NpgsqlConnection connection)
{
    // Queries
    public async Task<List<ApexMapRotationRow>> GetRecentRotations(DateTimeOffset since)
    {
        const string sql = """

                                       SELECT id, map, start_time, end_time, gamemode
                                       FROM apex_map_rotation
                                       WHERE start_time >= @since
                           """;

        return (await connection.QueryAsync<ApexMapRotationRow>(sql, new { since })).ToList();
    }

    public async Task<ApexMapRotationRow?> GetCurrentRotation(DateTimeOffset now, int gamemode)
    {
        const string sql = """

                                       SELECT id, map, start_time, end_time, gamemode
                                       FROM apex_map_rotation
                                       WHERE start_time <= @now AND end_time > @now AND gamemode = @gamemode
                                       LIMIT 1
                           """;

        return await connection.QuerySingleOrDefaultAsync<ApexMapRotationRow>(sql, new { now, gamemode });
    }

    public async Task InsertRotation(ApexMap map, DateTimeOffset startTime, DateTimeOffset endTime, ApexGamemode gamemode)
    {
        const string sql = """

                                       INSERT INTO apex_map_rotation (map, start_time, end_time, gamemode)
                                       VALUES (@map, @startTime, @endTime, @gamemode)
                           """;

        await connection.ExecuteAsync(sql, new { map = (int)map, startTime, endTime, gamemode = (int)gamemode });
    }

    public async Task DeleteOldRotations(DateTimeOffset before)
    {
        const string sql = "DELETE FROM apex_map_rotation WHERE end_time < @before";
        await connection.ExecuteAsync(sql, new { before });
    }

    public async Task<bool> RotationExists(DateTimeOffset startTime, DateTimeOffset endTime, int gamemode, int map)
    {
        const string sql = """

                                       SELECT EXISTS(
                                           SELECT 1 FROM apex_map_rotation
                                           WHERE start_time = @startTime AND end_time = @endTime AND gamemode = @gamemode AND map = @map
                                       )
                           """;

        return await connection.QuerySingleAsync<bool>(sql, new { startTime, endTime, gamemode, map });
    }

    public async Task InsertApexClipDetection(Guid clipId, int status)
    {
        const string sql = """

                                       INSERT INTO apex_clip_detection (clip_id, status, primary_detection, secondary_detection)
                                       VALUES (@clipId, @status, 27, 27)
                           """;
        await connection.ExecuteAsync(sql, new { clipId, status });
    }

    public async Task SetApexClipDetectionTaskId(Guid clipId, Guid taskId)
    {
        const string sql = """
                                       UPDATE apex_clip_detection
                                       SET task_id = @taskId
                                       WHERE clip_id = @clipId
                           """;
        await connection.ExecuteAsync(sql, new { clipId, taskId });
    }

    public async Task SetApexClipDetectionPrimaryDetection(Guid clipId, int detection)
    {
        const string sql = """
                                       UPDATE apex_clip_detection
                                       SET primary_detection = @detection
                                       WHERE clip_id = @clipId
                           """;
        await connection.ExecuteAsync(sql, new { clipId, detection });
    }

    public async Task SetApexClipDetectionSecondaryDetection(Guid clipId, int detection)
    {
        const string sql = """
                                       UPDATE apex_clip_detection
                                       SET secondary_detection = @detection
                                       WHERE clip_id = @clipId
                           """;
        await connection.ExecuteAsync(sql, new { clipId, detection });
    }

    public async Task SetApexClipDetectionStatus(Guid clipId, int status)
    {
        const string sql = """
                                       UPDATE apex_clip_detection
                                       SET status = @status
                                       WHERE clip_id = @clipId
                           """;
        await connection.ExecuteAsync(sql, new { clipId, status });
    }

    public async Task<ApexClipDetectionRow> GetApexClipDetection(Guid clipId)
    {
        const string sql = """
                                       SELECT clip_id, task_id, status, primary_detection, secondary_detection
                                       FROM apex_clip_detection
                                       WHERE clip_id = @clipId
                           """;
        return await connection.QuerySingleAsync<ApexClipDetectionRow>(sql, new { clipId });
    }

    public async Task<List<ApexClipDetectionRow>> GetAllApexClipDetections()
    {
        const string sql = """
                                       SELECT clip_id, task_id, status, primary_detection, secondary_detection
                                       FROM apex_clip_detection
                           """;
        return (await connection.QueryAsync<ApexClipDetectionRow>(sql)).ToList();
    }

    public async Task<List<ApexClipDetectionRow>> GetApexClipDetectionsByStatus(int status)
    {
        const string sql = """
                                       SELECT clip_id, task_id, status, primary_detection, secondary_detection
                                       FROM apex_clip_detection
                                       WHERE status = @status
                           """;
        return (await connection.QueryAsync<ApexClipDetectionRow>(sql, new { status })).ToList();
    }

    public async Task DeleteApexClipDetection(Guid clipId)
    {
        const string sql = "DELETE FROM apex_clip_detection WHERE clip_id = @clipId";
        await connection.ExecuteAsync(sql, new { clipId });
    }

    // Database Models (PascalCase properties auto-mapped to snake_case via DefaultTypeMap.MatchNamesWithUnderscores)
    public class ApexMapRotationRow
    {
        public Guid Id { get; set; }
        public int Map { get; set; } // stored as int in DB
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }
        public int Gamemode { get; set; } // stored as int in DB

        // Helper methods to convert to/from enums
        public ApexMap GetMap()
        {
            return (ApexMap)Map;
        }

        public ApexGamemode GetGamemode()
        {
            return (ApexGamemode)Gamemode;
        }
    }

    public class ApexClipDetectionRow
    {
        public Guid ClipId { get; set; }
        public Guid? TaskId { get; set; }
        public int Status { get; set; }
        public int PrimaryDetection { get; set; }
        public int SecondaryDetection { get; set; }

        public ClipDetectionStatus GetStatus()
        {
            return (ClipDetectionStatus)Status;
        }

        public ApexLegend GetPrimaryDetection()
        {
            return (ApexLegend)PrimaryDetection;
        }

        public ApexLegend GetSecondaryDetection()
        {
            return (ApexLegend)SecondaryDetection;
        }
    }
}