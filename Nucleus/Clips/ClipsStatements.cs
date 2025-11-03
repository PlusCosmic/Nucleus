using System.ComponentModel.DataAnnotations.Schema;
using Dapper;
using Npgsql;

namespace Nucleus.Clips;

public class ClipsStatements(NpgsqlConnection connection)
{
    // Database Models (PascalCase properties with Column attributes for snake_case mapping)
    public class ClipRow
    {
        [Column("id")]
        public Guid Id { get; set; }

        [Column("owner_id")]
        public Guid OwnerId { get; set; }

        [Column("video_id")]
        public Guid VideoId { get; set; }

        [Column("category")]
        public int Category { get; set; }

        [Column("md5_hash")]
        public string? Md5Hash { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }
    }

    public class TagRow
    {
        [Column("id")]
        public Guid Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }

    public class ClipTagRow
    {
        [Column("clip_id")]
        public Guid ClipId { get; set; }

        [Column("tag_id")]
        public Guid TagId { get; set; }
    }

    public class ClipCollectionRow
    {
        [Column("id")]
        public Guid Id { get; set; }

        [Column("owner_id")]
        public Guid OwnerId { get; set; }

        [Column("collection_id")]
        public Guid CollectionId { get; set; }

        [Column("category")]
        public int Category { get; set; }
    }

    public class ClipViewRow
    {
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("clip_id")]
        public Guid ClipId { get; set; }

        [Column("viewed_at")]
        public DateTime ViewedAt { get; set; }
    }

    public class ClipWithTagsRow
    {
        [Column("id")]
        public Guid Id { get; set; }

        [Column("owner_id")]
        public Guid OwnerId { get; set; }

        [Column("video_id")]
        public Guid VideoId { get; set; }

        [Column("category")]
        public int Category { get; set; }

        [Column("md5_hash")]
        public string? Md5Hash { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("tag_names")]
        public string? TagNames { get; set; } // comma-separated tags
    }

    public class TopTagRow
    {
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("count")]
        public int Count { get; set; }
    }

    // Queries
    public async Task<ClipCollectionRow?> GetCollectionByOwnerAndCategory(Guid ownerId, int category)
    {
        const string sql = @"
            SELECT id, owner_id, collection_id, category
            FROM clip_collection
            WHERE owner_id = @ownerId AND category = @category
            LIMIT 1";

        return await connection.QuerySingleOrDefaultAsync<ClipCollectionRow>(sql, new { ownerId, category });
    }

    public async Task<ClipCollectionRow> InsertCollection(Guid ownerId, Guid collectionId, int category)
    {
        const string sql = @"
            INSERT INTO clip_collection (owner_id, collection_id, category)
            VALUES (@ownerId, @collectionId, @category)
            RETURNING id, owner_id, collection_id, category";

        return await connection.QuerySingleAsync<ClipCollectionRow>(sql, new { ownerId, collectionId, category });
    }

    public async Task<List<ClipWithTagsRow>> GetClipsWithTagsByOwnerAndCategory(Guid ownerId, int category)
    {
        const string sql =
            """

                        SELECT
                            c.id,
                            c.owner_id,
                            c.video_id,
                            c.category,
                            c.md5_hash,
                            c.created_at,
                            STRING_AGG(t.name, ',') as tag_names
                        FROM clip c
                        LEFT JOIN clip_tag ct ON c.id = ct.clip_id
                        LEFT JOIN tag t ON ct.tag_id = t.id
                        WHERE c.owner_id = @ownerId AND c.category = @category
                        GROUP BY c.id, c.owner_id, c.video_id, c.category, c.md5_hash, c.created_at
            """;

        return (await connection.QueryAsync<ClipWithTagsRow>(sql, new { ownerId, category })).ToList();
    }

    public async Task<HashSet<Guid>> GetViewedClipIds(Guid userId, List<Guid> clipIds)
    {
        if (!clipIds.Any()) return new HashSet<Guid>();

        const string sql = @"
            SELECT clip_id
            FROM clip_view
            WHERE user_id = @userId AND clip_id = ANY(@clipIds)";

        return (await connection.QueryAsync<Guid>(sql, new { userId, clipIds = clipIds.ToArray() })).ToHashSet();
    }

    public async Task<bool> ClipExistsByMd5Hash(Guid ownerId, int category, string md5Hash)
    {
        const string sql = @"
            SELECT EXISTS(
                SELECT 1 FROM clip
                WHERE owner_id = @ownerId AND category = @category AND md5_hash = @md5Hash
            )";

        return await connection.QuerySingleAsync<bool>(sql, new { ownerId, category, md5Hash });
    }

    public async Task<ClipRow> InsertClip(Guid ownerId, Guid videoId, int category, string? md5Hash, DateTimeOffset createdAt)
    {
        const string sql = @"
            INSERT INTO clip (owner_id, video_id, category, md5_hash, created_at)
            VALUES (@ownerId, @videoId, @category, @md5Hash, @createdAt)
            RETURNING id, owner_id, video_id, category, md5_hash, created_at";

        return await connection.QuerySingleAsync<ClipRow>(sql, new { ownerId, videoId, category, md5Hash, createdAt });
    }

    public async Task<ClipWithTagsRow?> GetClipWithTagsById(Guid clipId)
    {
        const string sql = @"
            SELECT
                c.id,
                c.owner_id,
                c.video_id,
                c.category,
                c.md5_hash,
                c.created_at,
                STRING_AGG(t.name, ',') as tag_names
            FROM clip c
            LEFT JOIN clip_tag ct ON c.id = ct.clip_id
            LEFT JOIN tag t ON ct.tag_id = t.id
            WHERE c.id = @clipId
            GROUP BY c.id, c.owner_id, c.video_id, c.category, c.md5_hash, c.created_at";

        return await connection.QuerySingleOrDefaultAsync<ClipWithTagsRow>(sql, new { clipId });
    }

    public async Task<bool> IsClipViewed(Guid userId, Guid clipId)
    {
        const string sql = @"
            SELECT EXISTS(
                SELECT 1 FROM clip_view
                WHERE user_id = @userId AND clip_id = @clipId
            )";

        return await connection.QuerySingleAsync<bool>(sql, new { userId, clipId });
    }

    public async Task<TagRow?> GetTagByName(string name)
    {
        const string sql = "SELECT id, name FROM tag WHERE name = @name LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<TagRow>(sql, new { name });
    }

    public async Task<TagRow> InsertTag(string name)
    {
        const string sql = @"
            INSERT INTO tag (name)
            VALUES (@name)
            RETURNING id, name";

        return await connection.QuerySingleAsync<TagRow>(sql, new { name });
    }

    public async Task<int> GetTagCountForClip(Guid clipId)
    {
        const string sql = "SELECT COUNT(*) FROM clip_tag WHERE clip_id = @clipId";
        return await connection.QuerySingleAsync<int>(sql, new { clipId });
    }

    public async Task<bool> ClipHasTag(Guid clipId, Guid tagId)
    {
        const string sql = @"
            SELECT EXISTS(
                SELECT 1 FROM clip_tag
                WHERE clip_id = @clipId AND tag_id = @tagId
            )";

        return await connection.QuerySingleAsync<bool>(sql, new { clipId, tagId });
    }

    public async Task InsertClipTag(Guid clipId, Guid tagId)
    {
        const string sql = @"
            INSERT INTO clip_tag (clip_id, tag_id)
            VALUES (@clipId, @tagId)";

        await connection.ExecuteAsync(sql, new { clipId, tagId });
    }

    public async Task DeleteClipTag(Guid clipId, Guid tagId)
    {
        const string sql = "DELETE FROM clip_tag WHERE clip_id = @clipId AND tag_id = @tagId";
        await connection.ExecuteAsync(sql, new { clipId, tagId });
    }

    public async Task<List<TopTagRow>> GetTopTags(int limit)
    {
        const string sql = @"
            SELECT t.name, COUNT(ct.clip_id)::int as count
            FROM clip_tag ct
            JOIN tag t ON ct.tag_id = t.id
            GROUP BY t.name
            ORDER BY count DESC, t.name ASC
            LIMIT @limit";

        return (await connection.QueryAsync<TopTagRow>(sql, new { limit })).ToList();
    }

    public async Task<ClipViewRow> InsertClipView(Guid userId, Guid clipId)
    {
        const string sql = @"
            INSERT INTO clip_view (user_id, clip_id, viewed_at)
            VALUES (@userId, @clipId, @viewedAt)
            RETURNING user_id, clip_id, viewed_at";

        return await connection.QuerySingleAsync<ClipViewRow>(sql, new { userId, clipId, viewedAt = DateTime.UtcNow });
    }

    public async Task<ClipRow?> GetClipById(Guid clipId)
    {
        const string sql = "SELECT id, owner_id, video_id, category, md5_hash, created_at FROM clip WHERE id = @clipId LIMIT 1";
        return await connection.QuerySingleOrDefaultAsync<ClipRow>(sql, new { clipId });
    }

    public async Task<ClipWithTagsRow?> GetClipWithTagsByIdAndOwner(Guid clipId, Guid ownerId)
    {
        const string sql = @"
            SELECT
                c.id,
                c.owner_id,
                c.video_id,
                c.category,
                c.md5_hash,
                c.created_at,
                STRING_AGG(t.name, ',') as tag_names
            FROM clip c
            LEFT JOIN clip_tag ct ON c.id = ct.clip_id
            LEFT JOIN tag t ON ct.tag_id = t.id
            WHERE c.id = @clipId AND c.owner_id = @ownerId
            GROUP BY c.id, c.owner_id, c.video_id, c.category, c.md5_hash, c.created_at";

        return await connection.QuerySingleOrDefaultAsync<ClipWithTagsRow>(sql, new { clipId, ownerId });
    }

    public async Task DeleteClip(Guid clipId)
    {
        const string sql = "DELETE FROM clip WHERE id = @clipId";
        await connection.ExecuteAsync(sql, new { clipId });
    }

    public async Task<TagRow?> GetTagByNameForClip(Guid clipId, string tagName)
    {
        const string sql = @"
            SELECT t.id, t.name
            FROM tag t
            INNER JOIN clip_tag ct ON t.id = ct.tag_id
            WHERE ct.clip_id = @clipId AND t.name = @tagName
            LIMIT 1";

        return await connection.QuerySingleOrDefaultAsync<TagRow>(sql, new { clipId, tagName });
    }
}