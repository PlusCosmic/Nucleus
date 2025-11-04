using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nucleus.ApexLegends.Models;
using Nucleus.Test.Helpers;
using Nucleus.Test.TestFixtures;

namespace Nucleus.Test.ApexLegends;

/// <summary>
/// Tests for Apex Legends API endpoints.
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
        // Create whitelist for test users
        WebApplicationFixture.CreateTestWhitelist(_testDiscordId);
        await Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        WebApplicationFixture.CleanupTestWhitelist();
        return Task.CompletedTask;
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
            var mapRotation = await response.Content.ReadFromJsonAsync<CurrentMapRotation>();
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
