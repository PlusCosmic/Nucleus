using System.ComponentModel.DataAnnotations.Schema;
using Dapper;
using Npgsql;

namespace Nucleus.Links;

public class LinksStatements(NpgsqlConnection connection)
{
    // Database Models
    public class UserFrequentLinkRow
    {
        [Column("id")]
        public Guid Id { get; set; }

        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("url")]
        public string Url { get; set; } = string.Empty;

        [Column("thumbnail_url")]
        public string? ThumbnailUrl { get; set; }
    }

    // Queries
    public async Task<UserFrequentLinkRow> InsertLink(Guid userId, string title, string url, string? thumbnailUrl)
    {
        const string sql = @"
            INSERT INTO user_frequent_link (user_id, title, url, thumbnail_url)
            VALUES (@userId, @title, @url, @thumbnailUrl)
            RETURNING id, user_id, title, url, thumbnail_url";

        return await connection.QuerySingleAsync<UserFrequentLinkRow>(sql, new { userId, title, url, thumbnailUrl });
    }

    public async Task<List<UserFrequentLinkRow>> GetLinksByUserId(Guid userId)
    {
        const string sql = @"
            SELECT id, user_id, title, url, thumbnail_url
            FROM user_frequent_link
            WHERE user_id = @userId
            ORDER BY id DESC";

        return (await connection.QueryAsync<UserFrequentLinkRow>(sql, new { userId })).ToList();
    }

    public async Task<UserFrequentLinkRow?> GetLinkById(Guid id)
    {
        const string sql = @"
            SELECT id, user_id, title, url, thumbnail_url
            FROM user_frequent_link
            WHERE id = @id
            LIMIT 1";

        return await connection.QuerySingleOrDefaultAsync<UserFrequentLinkRow>(sql, new { id });
    }

    public async Task DeleteLink(Guid id)
    {
        const string sql = "DELETE FROM user_frequent_link WHERE id = @id";
        await connection.ExecuteAsync(sql, new { id });
    }
}
