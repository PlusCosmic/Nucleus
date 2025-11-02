using System.ComponentModel.DataAnnotations.Schema;
using Dapper;
using Npgsql;
using Nucleus.Repository;

namespace Nucleus.ApexLegends;

public class ApexStatements(NpgsqlConnection connection)
{
    // Database Models
    public class ApexMapRotationRow
    {
        [Column("id")]
        public Guid Id { get; set; }

        [Column("map")]
        public int Map { get; set; } // stored as int in DB

        [Column("start_time")]
        public DateTimeOffset StartTime { get; set; }

        [Column("end_time")]
        public DateTimeOffset EndTime { get; set; }

        [Column("gamemode")]
        public int Gamemode { get; set; } // stored as int in DB

        // Helper methods to convert to/from enums
        public ApexMap GetMap() => (ApexMap)Map;
        public ApexGamemode GetGamemode() => (ApexGamemode)Gamemode;
    }

    // Queries
    public async Task<List<ApexMapRotationRow>> GetRecentRotations(DateTimeOffset since)
    {
        const string sql = @"
            SELECT id, map, start_time, end_time, gamemode
            FROM apex_map_rotation
            WHERE start_time >= @since";

        return (await connection.QueryAsync<ApexMapRotationRow>(sql, new { since })).ToList();
    }

    public async Task<ApexMapRotationRow?> GetCurrentRotation(DateTimeOffset now, int gamemode)
    {
        const string sql = @"
            SELECT id, map, start_time, end_time, gamemode
            FROM apex_map_rotation
            WHERE start_time <= @now AND end_time > @now AND gamemode = @gamemode
            LIMIT 1";

        return await connection.QuerySingleOrDefaultAsync<ApexMapRotationRow>(sql, new { now, gamemode });
    }

    public async Task InsertRotation(ApexMap map, DateTimeOffset startTime, DateTimeOffset endTime, ApexGamemode gamemode)
    {
        const string sql = @"
            INSERT INTO apex_map_rotation (map, start_time, end_time, gamemode)
            VALUES (@map, @startTime, @endTime, @gamemode)";

        await connection.ExecuteAsync(sql, new { map = (int)map, startTime, endTime, gamemode = (int)gamemode });
    }

    public async Task DeleteOldRotations(DateTimeOffset before)
    {
        const string sql = "DELETE FROM apex_map_rotation WHERE end_time < @before";
        await connection.ExecuteAsync(sql, new { before });
    }

    public async Task<bool> RotationExists(DateTimeOffset startTime, DateTimeOffset endTime, int gamemode, int map)
    {
        const string sql = @"
            SELECT EXISTS(
                SELECT 1 FROM apex_map_rotation
                WHERE start_time = @startTime AND end_time = @endTime AND gamemode = @gamemode AND map = @map
            )";

        return await connection.QuerySingleAsync<bool>(sql, new { startTime, endTime, gamemode, map });
    }
}
