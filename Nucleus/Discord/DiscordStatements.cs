using System.ComponentModel.DataAnnotations.Schema;
using Dapper;
using Npgsql;

namespace Nucleus.Discord;

public class DiscordStatements(NpgsqlConnection connection)
{
    // Database Models
    public class DiscordUserRow
    {
        [Column("id")]
        public Guid Id { get; set; }

        [Column("discord_id")]
        public string DiscordId { get; set; } = string.Empty;

        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Column("global_name")]
        public string? GlobalName { get; set; }

        [Column("avatar")]
        public string? Avatar { get; set; }
    }

    // Queries
    public async Task<DiscordUserRow?> GetUserByDiscordId(string discordId)
    {
        const string sql = @"
            SELECT id, discord_id, username, global_name, avatar
            FROM discord_user
            WHERE discord_id = @discordId
            LIMIT 1";

        return await connection.QuerySingleOrDefaultAsync<DiscordUserRow>(sql, new { discordId });
    }

    public async Task<DiscordUserRow?> GetUserById(Guid id)
    {
        const string sql = @"
            SELECT id, discord_id, username, global_name, avatar
            FROM discord_user
            WHERE id = @id
            LIMIT 1";

        return await connection.QuerySingleOrDefaultAsync<DiscordUserRow>(sql, new { id });
    }

    public async Task<DiscordUserRow> InsertUser(string discordId, string username, string? globalName, string? avatar)
    {
        const string sql = @"
            INSERT INTO discord_user (discord_id, username, global_name, avatar)
            VALUES (@discordId, @username, @globalName, @avatar)
            RETURNING id, discord_id, username, global_name, avatar";

        return await connection.QuerySingleAsync<DiscordUserRow>(sql, new { discordId, username, globalName, avatar });
    }

    public async Task UpdateUser(Guid id, string username, string? globalName, string? avatar)
    {
        const string sql = @"
            UPDATE discord_user
            SET username = @username, global_name = @globalName, avatar = @avatar
            WHERE id = @id";

        await connection.ExecuteAsync(sql, new { id, username, globalName, avatar });
    }

    public async Task<DiscordUserRow> UpsertUser(string discordId, string username, string? globalName, string? avatar)
    {
        const string sql = @"
            INSERT INTO discord_user (discord_id, username, global_name, avatar)
            VALUES (@discordId, @username, @globalName, @avatar)
            ON CONFLICT (discord_id)
            DO UPDATE SET
                username = EXCLUDED.username,
                global_name = EXCLUDED.global_name,
                avatar = EXCLUDED.avatar
            RETURNING id, discord_id, username, global_name, avatar";

        return await connection.QuerySingleAsync<DiscordUserRow>(sql, new { discordId, username, globalName, avatar });
    }
}
