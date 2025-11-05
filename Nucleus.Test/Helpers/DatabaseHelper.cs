using Dapper;
using Npgsql;
using Nucleus.Clips;
using Nucleus.Discord;
using Nucleus.Links;

namespace Nucleus.Test.Helpers;

/// <summary>
/// Helper class for managing test database data.
/// Provides methods for seeding, clearing, and managing test data.
/// </summary>
public static class DatabaseHelper
{
    /// <summary>
    /// Clears all data from database tables while preserving schema.
    /// </summary>
    public static async Task ClearAllTablesAsync(NpgsqlConnection connection)
    {
        var tables = new[]
        {
            "clip_view",
            "clip_tag",
            "clip_collection",
            "clip",
            "tag",
            "user_frequent_link",
            "apex_map_rotation",
            "discord_user"
        };

        foreach (var table in tables)
        {
            await connection.ExecuteAsync($"TRUNCATE TABLE {table} CASCADE");
        }
    }

    /// <summary>
    /// Seeds a test Discord user into the database.
    /// Uses INSERT ... ON CONFLICT to handle duplicates gracefully.
    /// </summary>
    public static async Task<Guid> SeedDiscordUserAsync(
        NpgsqlConnection connection,
        string discordId = "123456789012345678",
        string username = "testuser",
        string globalName = "Test User",
        string? avatar = null)
    {
        const string sql = """
            INSERT INTO discord_user (id, discord_id, username, global_name, avatar)
            VALUES (gen_random_uuid(), @DiscordId, @Username, @GlobalName, @Avatar)
            ON CONFLICT (discord_id) DO UPDATE
                SET username = EXCLUDED.username,
                    global_name = EXCLUDED.global_name,
                    avatar = EXCLUDED.avatar
            RETURNING id
            """;

        var userId = await connection.QuerySingleAsync<Guid>(sql, new
        {
            DiscordId = discordId,
            Username = username,
            GlobalName = globalName,
            Avatar = avatar
        });

        return userId;
    }

    /// <summary>
    /// Seeds a test clip into the database.
    /// </summary>
    public static async Task<Guid> SeedClipAsync(
        NpgsqlConnection connection,
        Guid userId,
        string title = "Test Clip",
        string? description = null,
        string? bunnyVideoId = null,
        ClipCategoryEnum category = ClipCategoryEnum.ApexLegends,
        string? md5 = null,
        int durationSeconds = 30)
    {
        const string sql = """
            INSERT INTO clip (id, user_id, bunny_video_id, title, description, category, md5, duration_seconds, created_at)
            VALUES (gen_random_uuid(), @UserId, @BunnyVideoId, @Title, @Description, @Category, @Md5, @DurationSeconds, @CreatedAt)
            RETURNING id
            """;

        var clipId = await connection.QuerySingleAsync<Guid>(sql, new
        {
            UserId = userId,
            BunnyVideoId = bunnyVideoId ?? Guid.NewGuid().ToString(),
            Title = title,
            Description = description,
            Category = category.ToString(),
            Md5 = md5,
            DurationSeconds = durationSeconds,
            CreatedAt = DateTimeOffset.UtcNow
        });

        return clipId;
    }

    /// <summary>
    /// Seeds a tag for a clip.
    /// </summary>
    public static async Task SeedClipTagAsync(
        NpgsqlConnection connection,
        Guid clipId,
        string tagName)
    {
        // First, ensure the tag exists in the tag table
        const string ensureTagSql = """
            INSERT INTO tag (name)
            VALUES (@TagName)
            ON CONFLICT (name) DO NOTHING
            """;

        await connection.ExecuteAsync(ensureTagSql, new { TagName = tagName });

        // Then, link the clip to the tag
        const string linkSql = """
            INSERT INTO clip_tag (clip_id, tag_name)
            VALUES (@ClipId, @TagName)
            ON CONFLICT DO NOTHING
            """;

        await connection.ExecuteAsync(linkSql, new { ClipId = clipId, TagName = tagName });
    }

    /// <summary>
    /// Seeds a clip with tags.
    /// </summary>
    public static async Task<Guid> SeedClipWithTagsAsync(
        NpgsqlConnection connection,
        Guid userId,
        string title,
        string[] tags,
        ClipCategoryEnum category = ClipCategoryEnum.ApexLegends)
    {
        var clipId = await SeedClipAsync(connection, userId, title, category: category);

        foreach (var tag in tags)
        {
            await SeedClipTagAsync(connection, clipId, tag);
        }

        return clipId;
    }

    /// <summary>
    /// Seeds a user frequent link into the database.
    /// </summary>
    public static async Task<Guid> SeedUserLinkAsync(
        NpgsqlConnection connection,
        Guid userId,
        string url = "https://example.com",
        string? title = null,
        string? description = null,
        string? faviconUrl = null)
    {
        const string sql = """
            INSERT INTO user_frequent_link (id, user_id, url, title, description, favicon_url, created_at)
            VALUES (gen_random_uuid(), @UserId, @Url, @Title, @Description, @FaviconUrl, @CreatedAt)
            RETURNING id
            """;

        var linkId = await connection.QuerySingleAsync<Guid>(sql, new
        {
            UserId = userId,
            Url = url,
            Title = title ?? "Test Link",
            Description = description,
            FaviconUrl = faviconUrl,
            CreatedAt = DateTimeOffset.UtcNow
        });

        return linkId;
    }

    /// <summary>
    /// Seeds an Apex Legends map rotation entry.
    /// </summary>
    public static async Task SeedApexMapRotationAsync(
        NpgsqlConnection connection,
        int gamemode = 0, // 0 = BattleRoyale
        int map = 0,      // 0 = KingsCanyon
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null)
    {
        const string sql = """
            INSERT INTO apex_map_rotation (id, gamemode, map, start_time, end_time)
            VALUES (gen_random_uuid(), @Gamemode, @Map, @StartTime, @EndTime)
            """;

        await connection.ExecuteAsync(sql, new
        {
            Gamemode = gamemode,
            Map = map,
            StartTime = startTime ?? DateTimeOffset.UtcNow,
            EndTime = endTime ?? DateTimeOffset.UtcNow.AddHours(1)
        });
    }

    /// <summary>
    /// Gets a count of records in a table.
    /// </summary>
    public static async Task<int> GetTableCountAsync(NpgsqlConnection connection, string tableName)
    {
        return await connection.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM {tableName}");
    }

    /// <summary>
    /// Verifies that a record exists in a table by ID.
    /// </summary>
    public static async Task<bool> RecordExistsAsync(NpgsqlConnection connection, string tableName, Guid id)
    {
        var count = await connection.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM {tableName} WHERE id = @Id",
            new { Id = id });
        return count > 0;
    }

    /// <summary>
    /// Creates a complete test environment with a user and sample data.
    /// </summary>
    public static async Task<TestDataContext> SeedCompleteTestEnvironmentAsync(
        NpgsqlConnection connection,
        string discordId = "123456789012345678")
    {
        var userId = await SeedDiscordUserAsync(connection, discordId);

        var clip1Id = await SeedClipWithTagsAsync(
            connection,
            userId,
            "Test Clip 1",
            new[] { "ranked", "controller" },
            ClipCategoryEnum.ApexLegends);

        var clip2Id = await SeedClipWithTagsAsync(
            connection,
            userId,
            "Test Clip 2",
            new[] { "pubs", "mnk" },
            ClipCategoryEnum.ApexLegends);

        var link1Id = await SeedUserLinkAsync(
            connection,
            userId,
            "https://example.com",
            "Example Site");

        var link2Id = await SeedUserLinkAsync(
            connection,
            userId,
            "https://github.com",
            "GitHub");

        await SeedApexMapRotationAsync(connection, 0, 0); // BattleRoyale - Kings Canyon
        await SeedApexMapRotationAsync(connection, 1, 1); // Ranked - Worlds Edge

        return new TestDataContext
        {
            UserId = userId,
            ClipIds = new[] { clip1Id, clip2Id },
            LinkIds = new[] { link1Id, link2Id }
        };
    }
}

/// <summary>
/// Contains IDs of seeded test data.
/// </summary>
public class TestDataContext
{
    public Guid UserId { get; init; }
    public Guid[] ClipIds { get; init; } = Array.Empty<Guid>();
    public Guid[] LinkIds { get; init; } = Array.Empty<Guid>();
}
