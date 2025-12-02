using Dapper;
using Npgsql;
using Nucleus.Minecraft.Models;

namespace Nucleus.Minecraft;

public class MinecraftStatements(NpgsqlConnection connection)
{
    public async Task LogCommand(Guid userId, string command, string? response, bool success, string? error)
    {
        const string sql = """
            INSERT INTO minecraft_command_log (user_id, command, response, success, error, executed_at)
            VALUES (@UserId, @Command, @Response, @Success, @Error, @ExecutedAt)
            """;

        await connection.ExecuteAsync(sql, new
        {
            UserId = userId,
            Command = command,
            Response = response,
            Success = success,
            Error = error,
            ExecutedAt = DateTimeOffset.UtcNow
        });
    }

    public async Task LogFileOperation(Guid userId, string operation, string filePath, bool success, string? error)
    {
        const string sql = """
            INSERT INTO minecraft_file_log (user_id, operation, file_path, success, error, executed_at)
            VALUES (@UserId, @Operation, @FilePath, @Success, @Error, @ExecutedAt)
            """;

        await connection.ExecuteAsync(sql, new
        {
            UserId = userId,
            Operation = operation,
            FilePath = filePath,
            Success = success,
            Error = error,
            ExecutedAt = DateTimeOffset.UtcNow
        });
    }

    public async Task<List<CommandLogEntry>> GetRecentCommands(Guid userId, int limit = 50)
    {
        const string sql = """
            SELECT id, user_id, command, response, success, error, executed_at
            FROM minecraft_command_log
            WHERE user_id = @UserId
            ORDER BY executed_at DESC
            LIMIT @Limit
            """;

        IEnumerable<CommandLogEntry> results = await connection.QueryAsync<CommandLogEntry>(sql, new
        {
            UserId = userId,
            Limit = limit
        });

        return results.ToList();
    }

    public async Task<List<FileLogEntry>> GetRecentFileOperations(Guid userId, int limit = 50)
    {
        const string sql = """
            SELECT id, user_id, operation, file_path, success, error, executed_at
            FROM minecraft_file_log
            WHERE user_id = @UserId
            ORDER BY executed_at DESC
            LIMIT @Limit
            """;

        IEnumerable<FileLogEntry> results = await connection.QueryAsync<FileLogEntry>(sql, new
        {
            UserId = userId,
            Limit = limit
        });

        return results.ToList();
    }
}
