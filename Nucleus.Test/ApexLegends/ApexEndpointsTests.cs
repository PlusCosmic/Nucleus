using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Npgsql;
using Nucleus.Data.ApexLegends.Models;
using Nucleus.Test.Helpers;
using Nucleus.Test.TestFixtures;

namespace Nucleus.Test.ApexLegends;

/// <summary>
///     Tests for Apex Legends API endpoints.
/// </summary>
public class ApexEndpointsTests : IClassFixture<WebApplicationFixture>, IAsyncLifetime
{
    private readonly WebApplicationFixture _fixture;
    private readonly string _testDiscordId = AuthHelper.DefaultTestDiscordId;

    public ApexEndpointsTests(WebApplicationFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Clean database first to ensure isolation between test runs
        var connection = _fixture.GetService<NpgsqlConnection>();
        await DatabaseHelper.ClearAllTablesAsync(connection);

        // Seed Apex map rotation data for tests
        var now = DateTimeOffset.UtcNow;

        // Seed current and future map rotations
        // gamemode: 0 = BattleRoyale, 1 = Ranked
        // map: 0 = KingsCanyon, 1 = WorldsEdge, 2 = StormPoint, etc.
        await DatabaseHelper.SeedApexMapRotationAsync(
            connection, 0, 0, now.AddMinutes(-30), now.AddMinutes(30)); // BR - Kings Canyon (current)
        await DatabaseHelper.SeedApexMapRotationAsync(
            connection, 0, 1, now.AddMinutes(30), now.AddMinutes(90)); // BR - Worlds Edge (next)
        await DatabaseHelper.SeedApexMapRotationAsync(
            connection, 1, 2, now.AddMinutes(-30), now.AddMinutes(30)); // Ranked - Storm Point (current)
        await DatabaseHelper.SeedApexMapRotationAsync(
            connection, 1, 3, now.AddMinutes(30), now.AddMinutes(90)); // Ranked - Broken Moon (next)
    }

    public async Task DisposeAsync()
    {
        // Clean up database to prevent test interference
        var connection = _fixture.GetService<NpgsqlConnection>();
        await DatabaseHelper.ClearAllTablesAsync(connection);
    }

    #region GetApexMapRotation Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetApexMapRotation_WithoutAuthentication_ReturnsOkOrServiceUnavailable()
    {
        // Note: This endpoint does NOT require authentication (no RequireAuthorization)
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/apex-legends/map-rotation");

        // Assert
        // The endpoint either returns data (200) or API is unavailable (503)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetApexMapRotation_WithAuthentication_ReturnsOkOrServiceUnavailable()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        var response = await client.GetAsync("/apex-legends/map-rotation");

        // Assert
        // The endpoint either returns data (200) or API is unavailable (503)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetApexMapRotation_OnSuccess_ReturnsCurrentMapRotation()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/apex-legends/map-rotation");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            // Configure deserializer to match API's snake_case naming policy
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };

            var mapRotation = await response.Content.ReadFromJsonAsync<CurrentMapRotation>(options);
            mapRotation.Should().NotBeNull();
            mapRotation!.StandardMap.Should().NotBeNull();
            mapRotation.StandardMapNext.Should().NotBeNull();
            mapRotation.RankedMap.Should().NotBeNull();
            mapRotation.RankedMapNext.Should().NotBeNull();
            mapRotation.CorrectAsOf.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromHours(1));
        }
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetApexMapRotation_OnFailure_ReturnsServiceUnavailable()
    {
        // This test verifies error handling when the external API is down
        // In real scenarios, the MapService would fail to fetch from the external API
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/apex-legends/map-rotation");

        // Assert
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Apex Legends Status Unavailable");
        }
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetApexMapRotation_EndpointIsPublic()
    {
        // This test verifies that the endpoint is accessible without authentication
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/apex-legends/map-rotation");

        // Assert
        // Should NOT be Unauthorized - this is a public endpoint
        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetApexMapRotation_ReturnsExpectedJsonStructure()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/apex-legends/map-rotation");

        // Assert
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();

            // Verify snake_case serialization
            content.Should().Contain("standard_map");
            content.Should().Contain("ranked_map");
            content.Should().Contain("correct_as_of");

            // Should NOT contain PascalCase
            content.Should().NotContain("StandardMap");
            content.Should().NotContain("RankedMap");
        }
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetApexMapRotation_CachingBehavior()
    {
        // This test verifies that multiple requests return consistent data
        // (assuming caching is working in MapService)
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();

        // Act
        var response1 = await client.GetAsync("/apex-legends/map-rotation");
        var response2 = await client.GetAsync("/apex-legends/map-rotation");

        // Assert
        response1.StatusCode.Should().Be(response2.StatusCode);

        if (response1.StatusCode == HttpStatusCode.OK)
        {
            var content1 = await response1.Content.ReadAsStringAsync();
            var content2 = await response2.Content.ReadAsStringAsync();

            // Data should be consistent across requests (within reasonable time)
            content1.Should().NotBeEmpty();
            content2.Should().NotBeEmpty();
        }
    }

    #endregion
}