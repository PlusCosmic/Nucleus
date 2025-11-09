using System.Text;
using Dapper;
using Npgsql;

namespace Nucleus.Data.Clips;

public class ClipsStatements(NpgsqlConnection connection)
{
    // Queries
    public async Task<ClipCollectionRow?> GetCollectionByOwnerAndCategory(Guid ownerId, int category)
    {
        const string sql = """

                                       SELECT id, owner_id, collection_id, category
                                       FROM clip_collection
                                       WHERE owner_id = @ownerId AND category = @category
                                       LIMIT 1
                           """;

        return await connection.QuerySingleOrDefaultAsync<ClipCollectionRow>(sql, new { ownerId, category });
    }

    public async Task<ClipCollectionRow> InsertCollection(Guid ownerId, Guid collectionId, int category)
    {
        const string sql = """

                                       INSERT INTO clip_collection (owner_id, collection_id, category)
                                       VALUES (@ownerId, @collectionId, @category)
                                       RETURNING id, owner_id, collection_id, category
                           """;

        return await connection.QuerySingleAsync<ClipCollectionRow>(sql, new { ownerId, collectionId, category });
    }

    public async Task<List<ClipWithTagsRow>> GetClipsWithTagsByOwnerAndCategory(Guid ownerId, int category,
        List<string>? tags = null)
    {
        StringBuilder sql = new("""

                                            SELECT
                                                c.id,
                                                c.owner_id,
                                                c.video_id,
                                                c.category,
                                                c.md5_hash,
                                                c.created_at,
                                                c.title,
                                                c.length,
                                                c.thumbnail_file_name,
                                                c.date_uploaded,
                                                c.storage_size,
                                                c.video_status,
                                                c.encode_progress,
                                                STRING_AGG(t.name, ',') as tag_names
                                            FROM clip c
                                            LEFT JOIN clip_tag ct ON c.id = ct.clip_id
                                            LEFT JOIN tag t ON ct.tag_id = t.id
                                            WHERE c.owner_id = @ownerId AND c.category = @category
                                """);

        // If tags filter is provided, add HAVING clause to filter by tags
        if (tags != null && tags.Any())
        {
            sql.Append("""

                                   GROUP BY c.id, c.owner_id, c.video_id, c.category, c.md5_hash, c.created_at, c.title, c.length, c.thumbnail_file_name, c.date_uploaded, c.storage_size, c.video_status, c.encode_progress
                                   HAVING
                       """);

            // For each tag, ensure it exists in the aggregated tag_names
            List<string> conditions = tags.Select((_, i) => $"STRING_AGG(t.name, ',') LIKE @tag{i}").ToList();
            sql.Append(" " + string.Join(" AND ", conditions));

            DynamicParameters parameters = new();
            parameters.Add("ownerId", ownerId);
            parameters.Add("category", category);
            for (int i = 0; i < tags.Count; i++)
            {
                parameters.Add($"tag{i}", $"%{tags[i]}%");
            }

            return (await connection.QueryAsync<ClipWithTagsRow>(sql.ToString(), parameters)).ToList();
        }

        sql.Append("""

                               GROUP BY c.id, c.owner_id, c.video_id, c.category, c.md5_hash, c.created_at, c.title, c.length, c.thumbnail_file_name, c.date_uploaded, c.storage_size, c.video_status, c.encode_progress
                   """);

        return (await connection.QueryAsync<ClipWithTagsRow>(sql.ToString(), new { ownerId, category })).ToList();
    }

    public async Task<HashSet<Guid>> GetViewedClipIds(Guid userId, List<Guid> clipIds)
    {
        if (!clipIds.Any())
        {
            return new HashSet<Guid>();
        }

        const string sql = """

                                       SELECT clip_id
                                       FROM clip_view
                                       WHERE user_id = @userId AND clip_id = ANY(@clipIds)
                           """;

        return (await connection.QueryAsync<Guid>(sql, new { userId, clipIds = clipIds.ToArray() })).ToHashSet();
    }

    public async Task<bool> ClipExistsByMd5Hash(Guid ownerId, int category, string md5Hash)
    {
        const string sql = """

                                       SELECT EXISTS(
                                           SELECT 1 FROM clip
                                           WHERE owner_id = @ownerId AND category = @category AND md5_hash = @md5Hash
                                       )
                           """;

        return await connection.QuerySingleAsync<bool>(sql, new { ownerId, category, md5Hash });
    }

    public async Task<ClipRow> InsertClip(Guid ownerId, Guid videoId, int category, string? md5Hash,
        DateTimeOffset createdAt, string? title = null, int? length = null, string? thumbnailFileName = null,
        DateTimeOffset? dateUploaded = null, long? storageSize = null, int? videoStatus = null, int? encodeProgress = null)
    {
        const string sql = """

                                       INSERT INTO clip (owner_id, video_id, category, md5_hash, created_at, title, length, thumbnail_file_name, date_uploaded, storage_size, video_status, encode_progress)
                                       VALUES (@ownerId, @videoId, @category, @md5Hash, @createdAt, @title, @length, @thumbnailFileName, @dateUploaded, @storageSize, @videoStatus, @encodeProgress)
                                       RETURNING id, owner_id, video_id, category, md5_hash, created_at, title, length, thumbnail_file_name, date_uploaded, storage_size, video_status, encode_progress
                           """;

        return await connection.QuerySingleAsync<ClipRow>(sql, new { ownerId, videoId, category, md5Hash, createdAt, title, length, thumbnailFileName, dateUploaded, storageSize, videoStatus, encodeProgress });
    }

    public async Task<ClipWithTagsRow?> GetClipWithTagsById(Guid clipId)
    {
        const string sql = """

                                       SELECT
                                           c.id,
                                           c.owner_id,
                                           c.video_id,
                                           c.category,
                                           c.md5_hash,
                                           c.created_at,
                                           c.title,
                                           c.length,
                                           c.thumbnail_file_name,
                                           c.date_uploaded,
                                           c.storage_size,
                                           c.video_status,
                                           c.encode_progress,
                                           STRING_AGG(t.name, ',') as tag_names
                                       FROM clip c
                                       LEFT JOIN clip_tag ct ON c.id = ct.clip_id
                                       LEFT JOIN tag t ON ct.tag_id = t.id
                                       WHERE c.id = @clipId
                                       GROUP BY c.id, c.owner_id, c.video_id, c.category, c.md5_hash, c.created_at, c.title, c.length, c.thumbnail_file_name, c.date_uploaded, c.storage_size, c.video_status, c.encode_progress
                           """;

        return await connection.QuerySingleOrDefaultAsync<ClipWithTagsRow>(sql, new { clipId });
    }

    public async Task<bool> IsClipViewed(Guid userId, Guid clipId)
    {
        const string sql = """

                                       SELECT EXISTS(
                                           SELECT 1 FROM clip_view
                                           WHERE user_id = @userId AND clip_id = @clipId
                                       )
                           """;

        return await connection.QuerySingleAsync<bool>(sql, new { userId, clipId });
    }

    public async Task<TagRow?> GetTagByName(string name)
    {
        const string sql = "SELECT id, name FROM tag WHERE name = @name LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<TagRow>(sql, new { name });
    }

    public async Task<TagRow> InsertTag(string name)
    {
        const string sql = """

                                       INSERT INTO tag (name)
                                       VALUES (@name)
                                       RETURNING id, name
                           """;

        return await connection.QuerySingleAsync<TagRow>(sql, new { name });
    }

    public async Task<int> GetTagCountForClip(Guid clipId)
    {
        const string sql = "SELECT COUNT(*) FROM clip_tag WHERE clip_id = @clipId";
        return await connection.QuerySingleAsync<int>(sql, new { clipId });
    }

    public async Task<bool> ClipHasTag(Guid clipId, Guid tagId)
    {
        const string sql = """

                                       SELECT EXISTS(
                                           SELECT 1 FROM clip_tag
                                           WHERE clip_id = @clipId AND tag_id = @tagId
                                       )
                           """;

        return await connection.QuerySingleAsync<bool>(sql, new { clipId, tagId });
    }

    public async Task InsertClipTag(Guid clipId, Guid tagId)
    {
        const string sql = """

                                       INSERT INTO clip_tag (clip_id, tag_id)
                                       VALUES (@clipId, @tagId)
                           """;

        await connection.ExecuteAsync(sql, new { clipId, tagId });
    }

    public async Task DeleteClipTag(Guid clipId, Guid tagId)
    {
        const string sql = "DELETE FROM clip_tag WHERE clip_id = @clipId AND tag_id = @tagId";
        await connection.ExecuteAsync(sql, new { clipId, tagId });
    }

    public async Task<List<TopTagRow>> GetTopTags(int limit)
    {
        const string sql = """

                                       SELECT t.name, COUNT(ct.clip_id)::int as count
                                       FROM clip_tag ct
                                       JOIN tag t ON ct.tag_id = t.id
                                       GROUP BY t.name
                                       ORDER BY count DESC, t.name ASC
                                       LIMIT @limit
                           """;

        return (await connection.QueryAsync<TopTagRow>(sql, new { limit })).ToList();
    }

    public async Task<ClipViewRow> InsertClipView(Guid userId, Guid clipId)
    {
        const string sql = """

                                       INSERT INTO clip_view (user_id, clip_id, viewed_at)
                                       VALUES (@userId, @clipId, @viewedAt)
                                       RETURNING user_id, clip_id, viewed_at
                           """;

        return await connection.QuerySingleAsync<ClipViewRow>(sql, new { userId, clipId, viewedAt = DateTime.UtcNow });
    }

    public async Task<ClipRow?> GetClipById(Guid clipId)
    {
        const string sql =
            "SELECT id, owner_id, video_id, category, md5_hash, created_at, title, length, thumbnail_file_name, date_uploaded, storage_size, video_status, encode_progress FROM clip WHERE id = @clipId LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<ClipRow>(sql, new { clipId });
    }

    public async Task<ClipRow?> GetClipByVideoId(Guid videoId)
    {
        const string sql =
            "SELECT id, owner_id, video_id, category, md5_hash, created_at, title, length, thumbnail_file_name, date_uploaded, storage_size, video_status, encode_progress FROM clip WHERE video_id = @videoId LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<ClipRow>(sql, new { videoId });
    }

    public async Task<ClipWithTagsRow?> GetClipWithTagsByIdAndOwner(Guid clipId, Guid ownerId)
    {
        const string sql = """

                                       SELECT
                                           c.id,
                                           c.owner_id,
                                           c.video_id,
                                           c.category,
                                           c.md5_hash,
                                           c.created_at,
                                           c.title,
                                           c.length,
                                           c.thumbnail_file_name,
                                           c.date_uploaded,
                                           c.storage_size,
                                           c.video_status,
                                           c.encode_progress,
                                           STRING_AGG(t.name, ',') as tag_names
                                       FROM clip c
                                       LEFT JOIN clip_tag ct ON c.id = ct.clip_id
                                       LEFT JOIN tag t ON ct.tag_id = t.id
                                       WHERE c.id = @clipId AND c.owner_id = @ownerId
                                       GROUP BY c.id, c.owner_id, c.video_id, c.category, c.md5_hash, c.created_at, c.title, c.length, c.thumbnail_file_name, c.date_uploaded, c.storage_size, c.video_status, c.encode_progress
                           """;

        return await connection.QuerySingleOrDefaultAsync<ClipWithTagsRow>(sql, new { clipId, ownerId });
    }

    public async Task DeleteClip(Guid clipId)
    {
        const string sql = "DELETE FROM clip WHERE id = @clipId";
        await connection.ExecuteAsync(sql, new { clipId });
    }

    public async Task<TagRow?> GetTagByNameForClip(Guid clipId, string tagName)
    {
        const string sql = """

                                       SELECT t.id, t.name
                                       FROM tag t
                                       INNER JOIN clip_tag ct ON t.id = ct.tag_id
                                       WHERE ct.clip_id = @clipId AND t.name = @tagName
                                       LIMIT 1
                           """;

        return await connection.QuerySingleOrDefaultAsync<TagRow>(sql, new { clipId, tagName });
    }

    public async Task<List<TagRow>> GetTagsForClip(Guid clipId)
    {
        const string sql = """

                                       SELECT t.id, t.name
                                       FROM tag t
                                       INNER JOIN clip_tag ct ON t.id = ct.tag_id
                                       WHERE ct.clip_id = @clipId
                                       ORDER BY t.name
                           """;

        return (await connection.QueryAsync<TagRow>(sql, new { clipId })).ToList();
    }

    public async Task<List<ClipRow>> GetAllClipsForCategory(int category)
    {
        const string sql = "SELECT id, owner_id, video_id, category, md5_hash, created_at, title, length, thumbnail_file_name, date_uploaded, storage_size, video_status, encode_progress FROM clip WHERE category = @category";
        return (await connection.QueryAsync<ClipRow>(sql, new { category })).ToList();
    }

    public class ClipRow
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public Guid VideoId { get; set; }
        public int Category { get; set; }
        public string? Md5Hash { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string? Title { get; set; }
        public int? Length { get; set; }
        public string? ThumbnailFileName { get; set; }
        public DateTimeOffset? DateUploaded { get; set; }
        public long? StorageSize { get; set; }
        public int? VideoStatus { get; set; }
        public int? EncodeProgress { get; set; }
    }

    public class TagRow
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class ClipTagRow
    {
        public Guid ClipId { get; set; }
        public Guid TagId { get; set; }
    }

    public class ClipCollectionRow
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public Guid CollectionId { get; set; }
        public int Category { get; set; }
    }

    public class ClipViewRow
    {
        public Guid UserId { get; set; }
        public Guid ClipId { get; set; }
        public DateTime ViewedAt { get; set; }
    }

    public class ClipWithTagsRow
    {
        public Guid Id { get; set; }
        public Guid OwnerId { get; set; }
        public Guid VideoId { get; set; }
        public int Category { get; set; }
        public string? Md5Hash { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public string? TagNames { get; set; } // comma-separated tags
        public string? Title { get; set; }
        public int? Length { get; set; }
        public string? ThumbnailFileName { get; set; }
        public DateTimeOffset? DateUploaded { get; set; }
        public long? StorageSize { get; set; }
        public int? VideoStatus { get; set; }
        public int? EncodeProgress { get; set; }
    }

    public class TopTagRow
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}