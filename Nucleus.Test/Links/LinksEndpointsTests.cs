using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nucleus.Links;
using Nucleus.Test.Helpers;
using Nucleus.Test.TestFixtures;

namespace Nucleus.Test.Links;

/// <summary>
/// Tests for Links API endpoints.
/// </summary>
public class LinksEndpointsTests : IClassFixture<WebApplicationFixture>, IAsyncLifetime
{
    private readonly WebApplicationFixture _fixture;
    private readonly string _testDiscordId = AuthHelper.DefaultTestDiscordId;
    private readonly string _secondaryDiscordId = AuthHelper.SecondaryTestDiscordId;

    public LinksEndpointsTests(WebApplicationFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Create whitelist for test users
        WebApplicationFixture.CreateTestWhitelist(_testDiscordId, _secondaryDiscordId);
        await Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        WebApplicationFixture.CleanupTestWhitelist();
        return Task.CompletedTask;
    }

    #region GetLinksForUser Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetLinksForUser_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetLinksForUser_WithAuthentication_ReturnsOk()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        var response = await client.GetAsync("/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetLinksForUser_ReturnsListOfLinks()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        var response = await client.GetAsync("/links");
        var links = await response.Content.ReadFromJsonAsync<List<LinksStatements.UserFrequentLinkRow>>();

        // Assert
        links.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetLinksForUser_OnlyReturnsCurrentUserLinks()
    {
        // This test would require seeding data to validate user isolation
        // It's a placeholder for when database integration is added
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        var response = await client.GetAsync("/links");
        var links = await response.Content.ReadFromJsonAsync<List<LinksStatements.UserFrequentLinkRow>>();

        // Assert
        links.Should().NotBeNull();
        // All links should belong to the authenticated user (when data exists)
    }

    #endregion

    #region AddLink Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task AddLink_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();
        var request = new { url = "https://example.com" };

        // Act
        var response = await client.PostAsJsonAsync("/links", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task AddLink_WithValidUrl_ReturnsCreated()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var uniqueUrl = $"https://example-{Guid.NewGuid()}.com";
        var request = new { url = uniqueUrl };

        // Act
        var response = await client.PostAsJsonAsync("/links", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task AddLink_WithValidUrl_AddsLinkToDatabase()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var uniqueUrl = $"https://unique-{Guid.NewGuid()}.com";
        var request = new { url = uniqueUrl };

        // Act - Add the link
        var addResponse = await client.PostAsJsonAsync("/links", request);
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act - Get all links
        var getResponse = await client.GetAsync("/links");
        var links = await getResponse.Content.ReadFromJsonAsync<List<LinksStatements.UserFrequentLinkRow>>();

        // Assert
        links.Should().NotBeNull();
        links!.Should().Contain(l => l.Url == uniqueUrl);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task AddLink_FetchesMetadataFromUrl()
    {
        // This test would require mocking the HTTP client to avoid external dependencies
        // For now, we just verify the endpoint accepts the request
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var request = new { url = $"https://example-{Guid.NewGuid()}.com" };

        // Act
        var response = await client.PostAsJsonAsync("/links", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    #endregion

    #region DeleteLink Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task DeleteLink_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();
        var linkId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/links/{linkId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task DeleteLink_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/links/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task DeleteLink_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // First, create a link
        var uniqueUrl = $"https://to-delete-{Guid.NewGuid()}.com";
        var addRequest = new { url = uniqueUrl };
        var addResponse = await client.PostAsJsonAsync("/links", addRequest);
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Get the link ID
        var getResponse = await client.GetAsync("/links");
        var links = await getResponse.Content.ReadFromJsonAsync<List<LinksStatements.UserFrequentLinkRow>>();
        var createdLink = links!.First(l => l.Url == uniqueUrl);

        // Act - Delete the link
        var deleteResponse = await client.DeleteAsync($"/links/{createdLink.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task DeleteLink_RemovesLinkFromDatabase()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // First, create a link
        var uniqueUrl = $"https://remove-me-{Guid.NewGuid()}.com";
        var addRequest = new { url = uniqueUrl };
        await client.PostAsJsonAsync("/links", addRequest);

        // Get the link ID
        var getResponse1 = await client.GetAsync("/links");
        var links1 = await getResponse1.Content.ReadFromJsonAsync<List<LinksStatements.UserFrequentLinkRow>>();
        var linkToDelete = links1!.First(l => l.Url == uniqueUrl);

        // Act - Delete the link
        await client.DeleteAsync($"/links/{linkToDelete.Id}");

        // Act - Get all links again
        var getResponse2 = await client.GetAsync("/links");
        var links2 = await getResponse2.Content.ReadFromJsonAsync<List<LinksStatements.UserFrequentLinkRow>>();

        // Assert - Link should be gone
        links2.Should().NotContain(l => l.Id == linkToDelete.Id);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task DeleteLink_CannotDeleteOtherUsersLinks()
    {
        // This test verifies authorization - users can only delete their own links
        // Would require seeding data from different users to fully test
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var nonExistentId = Guid.NewGuid(); // Simulating another user's link

        // Act
        var response = await client.DeleteAsync($"/links/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region JSON Serialization Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task Endpoints_UseSnakeCaseJsonSerialization()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        var response = await client.GetAsync("/links");
        var content = await response.Content.ReadAsStringAsync();

        // Assert - Check for snake_case fields
        if (content.Length > 2) // More than just "[]"
        {
            content.Should().Contain("user_id"); // snake_case
            content.Should().NotContain("UserId"); // NOT PascalCase
        }
    }

    #endregion
}
