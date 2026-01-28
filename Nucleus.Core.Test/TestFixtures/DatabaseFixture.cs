using EvolveDb;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Nucleus.Test.TestFixtures;

/// <summary>
/// Provides a PostgreSQL test container for integration tests.
/// Implements IAsyncLifetime to manage container lifecycle.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private NpgsqlConnection? _connection;

    public DatabaseFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithDatabase("nucleus_test_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();
    }

    /// <summary>
    /// Gets the database connection. Available after InitializeAsync is called.
    /// </summary>
    public NpgsqlConnection Connection =>
        _connection ?? throw new InvalidOperationException("Database not initialized. Call InitializeAsync first.");

    /// <summary>
    /// Gets the connection string for the test database.
    /// </summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>
    /// Initializes the database container, runs migrations, and opens a connection.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _connection = new NpgsqlConnection(_container.GetConnectionString());
        await _connection.OpenAsync();

        // Run Evolve migrations to set up the database schema
        var evolve = new Evolve(_connection, msg => Console.WriteLine($"[Evolve] {msg}"))
        {
            Locations = ["db/migrations"], // Migrations are copied to output directory
            IsEraseDisabled = true,
        };

        try
        {
            evolve.Migrate();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Migration failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Cleans up the database connection and stops the container.
    /// </summary>
    public async Task DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    /// <summary>
    /// Creates a new scoped connection for a test. Caller is responsible for disposal.
    /// </summary>
    public async Task<NpgsqlConnection> CreateScopedConnectionAsync()
    {
        var connection = new NpgsqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>
    /// Clears all data from tables while preserving schema.
    /// Useful for resetting state between tests.
    /// </summary>
    public async Task ClearAllTablesAsync()
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
            await using var cmd = new NpgsqlCommand($"TRUNCATE TABLE {table} CASCADE", Connection);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
