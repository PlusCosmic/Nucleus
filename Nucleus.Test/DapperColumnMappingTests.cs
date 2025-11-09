using System.ComponentModel.DataAnnotations.Schema;
using Dapper;
using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Nucleus.Test;

/// <summary>
/// Tests to verify whether [Column] attributes are necessary with DefaultTypeMap.MatchNamesWithUnderscores = true
/// </summary>
public class DapperColumnMappingTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .Build();

    private NpgsqlConnection? _connection;

    public async Task InitializeAsync()
    {
        // This is set globally in Program.cs - we need it here for the test
        DefaultTypeMap.MatchNamesWithUnderscores = true;

        await _postgres.StartAsync();
        _connection = new NpgsqlConnection(_postgres.GetConnectionString());
        await _connection.OpenAsync();

        // Create test table with snake_case columns
        await _connection.ExecuteAsync("""
            CREATE TABLE test_mapping (
                id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                test_value VARCHAR(100) NOT NULL,
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now()
            );
            """);

        // Insert test data
        await _connection.ExecuteAsync("""
            INSERT INTO test_mapping (test_value, created_at)
            VALUES ('Test Data', '2024-01-01 12:00:00Z');
            """);
    }

    public async Task DisposeAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task DapperMapsCorrectly_WithColumnAttributes()
    {
        // Arrange - Model WITH [Column] attributes
        var sql = "SELECT id, test_value, created_at FROM test_mapping LIMIT 1";

        // Act
        var result = await _connection!.QuerySingleAsync<TestRowWithColumnAttributes>(sql);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.TestValue.Should().Be("Test Data");
        result.CreatedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task DapperMapsCorrectly_WithoutColumnAttributes()
    {
        // Arrange - Model WITHOUT [Column] attributes, relying on DefaultTypeMap.MatchNamesWithUnderscores
        var sql = "SELECT id, test_value, created_at FROM test_mapping LIMIT 1";

        // Act
        var result = await _connection!.QuerySingleAsync<TestRowWithoutColumnAttributes>(sql);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.TestValue.Should().Be("Test Data");
        result.CreatedAt.Should().NotBe(default);
    }

    // Model WITH [Column] attributes (current codebase style)
    private class TestRowWithColumnAttributes
    {
        [Column("id")]
        public Guid Id { get; set; }

        [Column("test_value")]
        public string TestValue { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }
    }

    // Model WITHOUT [Column] attributes (testing if DefaultTypeMap.MatchNamesWithUnderscores handles this)
    private class TestRowWithoutColumnAttributes
    {
        public Guid Id { get; set; }
        public string TestValue { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
    }
}
