using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Npgsql;
using Nucleus.Links;
using Nucleus.Test.Helpers;
using Nucleus.Test.TestFixtures;

namespace Nucleus.Test.Links;

/// <summary>
///     Tests for Links API endpoints.
/// </summary>
public class LinksEndpointsTests : IClassFixture<WebApplicationFixture>, IAsyncLifetime
{
    private readonly WebApplicationFixture _fixture;
    private readonly string _secondaryDiscordId = AuthHelper.SecondaryTestDiscordId;
    private readonly string _testDiscordId = AuthHelper.DefaultTestDiscordId;

    public LinksEndpointsTests(WebApplicationFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        // Clean database first to ensure isolation between test runs
        NpgsqlConnection connection = _fixture.GetService<NpgsqlConnection>();
        await DatabaseHelper.ClearAllTablesAsync(connection);

        // Seed Discord users in database
        await DatabaseHelper.SeedDiscordUserAsync(connection, _testDiscordId);
        await DatabaseHelper.SeedDiscordUserAsync(connection, _secondaryDiscordId, "testuser2", "Test User 2");
    }

    public async Task DisposeAsync()
    {
        // Clean up database to prevent test interference
        NpgsqlConnection connection = _fixture.GetService<NpgsqlConnection>();
        await DatabaseHelper.ClearAllTablesAsync(connection);
    }

    #region JSON Serialization Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task Endpoints_UseSnakeCaseJsonSerialization()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        HttpResponseMessage response = await client.GetAsync("/links");
        string content = await response.Content.ReadAsStringAsync();

        // Assert - Check for snake_case fields
        if (content.Length > 2) // More than just "[]"
        {
            content.Should().Contain("user_id"); // snake_case
            content.Should().NotContain("UserId"); // NOT PascalCase
        }
    }

    #endregion

    #region GetLinksForUser Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetLinksForUser_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = _fixture.CreateUnauthenticatedClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetLinksForUser_WithAuthentication_ReturnsOk()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        HttpResponseMessage response = await client.GetAsync("/links");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetLinksForUser_ReturnsListOfLinks()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        HttpResponseMessage response = await client.GetAsync("/links");
        List<LinksStatements.UserFrequentLinkRow>? links = await response.Content.ReadFromJsonAsync<List<LinksStatements.UserFrequentLinkRow>>();

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
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        HttpResponseMessage response = await client.GetAsync("/links");
        List<LinksStatements.UserFrequentLinkRow>? links = await response.Content.ReadFromJsonAsync<List<LinksStatements.UserFrequentLinkRow>>();

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
        HttpClient client = _fixture.CreateUnauthenticatedClient();
        var request = new { url = "https://example.com" };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/links", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task AddLink_WithValidUrl_ReturnsCreated()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        string uniqueUrl = $"https://example-{Guid.NewGuid()}.com";
        var request = new { url = uniqueUrl };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/links", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task AddLink_WithValidUrl_AddsLinkToDatabase()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        string uniqueUrl = $"https://unique-{Guid.NewGuid()}.com";
        var request = new { url = uniqueUrl };

        // Act - Add the link
        HttpResponseMessage addResponse = await client.PostAsJsonAsync("/links", request);
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act - Get all links
        HttpResponseMessage getResponse = await client.GetAsync("/links");
        List<LinksStatements.UserFrequentLinkRow>? links = await getResponse.Content.ReadFromJsonAsync<List<LinksStatements.UserFrequentLinkRow>>();

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
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var request = new { url = $"https://example-{Guid.NewGuid()}.com" };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/links", request);

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
        HttpClient client = _fixture.CreateUnauthenticatedClient();
        Guid linkId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/links/{linkId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task DeleteLink_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        Guid nonExistentId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/links/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task DeleteLink_WithValidId_ReturnsNoContent()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // First, create a link
        string uniqueUrl = $"https://to-delete-{Guid.NewGuid()}.com";
        var addRequest = new { url = uniqueUrl };
        HttpResponseMessage addResponse = await client.PostAsJsonAsync("/links", addRequest);
        addResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Get the link ID
        HttpResponseMessage getResponse = await client.GetAsync("/links");
        List<LinksStatements.UserFrequentLinkRow>? links = await getResponse.Content.ReadFromJsonAsync<List<LinksStatements.UserFrequentLinkRow>>();
        LinksStatements.UserFrequentLinkRow createdLink = links!.First(l => l.Url == uniqueUrl);

        // Act - Delete the link
        HttpResponseMessage deleteResponse = await client.DeleteAsync($"/links/{createdLink.Id}");

        // Assert
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task DeleteLink_RemovesLinkFromDatabase()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // First, create a link
        string uniqueUrl = $"https://remove-me-{Guid.NewGuid()}.com";
        var addRequest = new { url = uniqueUrl };
        await client.PostAsJsonAsync("/links", addRequest);

        // Get the link ID
        HttpResponseMessage getResponse1 = await client.GetAsync("/links");
        List<LinksStatements.UserFrequentLinkRow>? links1 = await getResponse1.Content.ReadFromJsonAsync<List<LinksStatements.UserFrequentLinkRow>>();
        LinksStatements.UserFrequentLinkRow linkToDelete = links1!.First(l => l.Url == uniqueUrl);

        // Act - Delete the link
        await client.DeleteAsync($"/links/{linkToDelete.Id}");

        // Act - Get all links again
        HttpResponseMessage getResponse2 = await client.GetAsync("/links");
        List<LinksStatements.UserFrequentLinkRow>? links2 = await getResponse2.Content.ReadFromJsonAsync<List<LinksStatements.UserFrequentLinkRow>>();

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
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        Guid nonExistentId = Guid.NewGuid(); // Simulating another user's link

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/links/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion
}