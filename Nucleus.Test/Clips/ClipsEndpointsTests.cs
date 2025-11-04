using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nucleus.Clips;
using Nucleus.Test.Helpers;
using Nucleus.Test.TestFixtures;

namespace Nucleus.Test.Clips;

/// <summary>
/// Tests for Clips API endpoints.
/// </summary>
public class ClipsEndpointsTests : IClassFixture<WebApplicationFixture>, IAsyncLifetime
{
    private readonly WebApplicationFixture _fixture;
    private readonly string _testDiscordId = AuthHelper.DefaultTestDiscordId;
    private readonly string _secondaryDiscordId = AuthHelper.SecondaryTestDiscordId;

    public ClipsEndpointsTests(WebApplicationFixture fixture)
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

    #region GetCategories Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetCategories_WithAuthenticatedUser_ReturnsOk()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        var response = await client.GetAsync("/clips/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetCategories_ReturnsValidCategoryList()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        var response = await client.GetAsync("/clips/categories");
        var categories = await response.Content.ReadFromJsonAsync<List<ClipCategory>>();

        // Assert
        categories.Should().NotBeNull();
        categories.Should().HaveCountGreaterThan(0);
        categories.Should().Contain(c => c.categoryEnum == ClipCategoryEnum.ApexLegends);
        categories!.All(c => !string.IsNullOrEmpty(c.Name)).Should().BeTrue();
    }

    #endregion

    #region GetVideosByCategory Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetVideosByCategory_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/clips/categories/ApexLegends/videos?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetVideosByCategory_WithAuthentication_ReturnsOk()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        var response = await client.GetAsync("/clips/categories/ApexLegends/videos?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetVideosByCategory_ReturnsPagedResponse()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        var response = await client.GetAsync("/clips/categories/ApexLegends/videos?page=1&pageSize=10");
        var pagedResponse = await response.Content.ReadFromJsonAsync<PagedClipsResponse>();

        // Assert
        pagedResponse.Should().NotBeNull();
        pagedResponse!.Clips.Should().NotBeNull();
        pagedResponse.TotalPages.Should().BeGreaterOrEqualTo(0);
    }

    #endregion

    #region GetUnviewedVideosByCategory Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetUnviewedVideosByCategory_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();

        // Act
        var response = await client.GetAsync("/clips/categories/ApexLegends/videos/unviewed?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetUnviewedVideosByCategory_WithAuthentication_ReturnsOk()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        var response = await client.GetAsync("/clips/categories/ApexLegends/videos/unviewed?page=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region CreateVideo Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task CreateVideo_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();
        var request = new { videoTitle = "Test Video" };

        // Act
        var response = await client.PostAsJsonAsync("/clips/categories/ApexLegends/videos", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task CreateVideo_WithValidData_ReturnsOk()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var request = new { videoTitle = "Test Video " + Guid.NewGuid() };

        // Act
        var response = await client.PostAsJsonAsync("/clips/categories/ApexLegends/videos", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task CreateVideo_WithDuplicateMd5_ReturnsConflict()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var md5 = "duplicate_" + Guid.NewGuid().ToString("N");
        var request1 = new { videoTitle = "Video 1", md5Hash = md5 };
        var request2 = new { videoTitle = "Video 2", md5Hash = md5 };

        // Act - Create first video
        var response1 = await client.PostAsJsonAsync("/clips/categories/ApexLegends/videos", request1);
        response1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act - Try to create duplicate
        var response2 = await client.PostAsJsonAsync("/clips/categories/ApexLegends/videos", request2);

        // Assert
        response2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    #endregion

    #region GetVideoById Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetVideoById_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();
        var clipId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/clips/videos/{clipId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetVideoById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/clips/videos/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region MarkVideoAsViewed Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task MarkVideoAsViewed_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();
        var clipId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/clips/videos/{clipId}/view", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task MarkVideoAsViewed_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.PostAsync($"/clips/videos/{nonExistentId}/view", null);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region AddTagToClip Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task AddTagToClip_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();
        var clipId = Guid.NewGuid();
        var request = new { tag = "test-tag" };

        // Act
        var response = await client.PostAsJsonAsync($"/clips/videos/{clipId}/tags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task AddTagToClip_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var nonExistentId = Guid.NewGuid();
        var request = new { tag = "test-tag" };

        // Act
        var response = await client.PostAsJsonAsync($"/clips/videos/{nonExistentId}/tags", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region RemoveTagFromClip Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task RemoveTagFromClip_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();
        var clipId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/clips/videos/{clipId}/tags/test-tag");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task RemoveTagFromClip_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/clips/videos/{nonExistentId}/tags/test-tag");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region GetTopTags Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetTopTags_WithAuthenticatedUser_ReturnsOk()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        var response = await client.GetAsync("/clips/tags/top");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetTopTags_ReturnsListOfTags()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        var response = await client.GetAsync("/clips/tags/top");
        var tags = await response.Content.ReadFromJsonAsync<List<TopTag>>();

        // Assert
        tags.Should().NotBeNull();
    }

    #endregion

    #region UpdateClipTitle Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task UpdateClipTitle_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();
        var clipId = Guid.NewGuid();
        var request = new { title = "Updated Title" };

        // Act
        var response = await client.PatchAsync($"/clips/videos/{clipId}/title", JsonContent.Create(request));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task UpdateClipTitle_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var nonExistentId = Guid.NewGuid();
        var request = new { title = "Updated Title" };

        // Act
        var response = await client.PatchAsync($"/clips/videos/{nonExistentId}/title", JsonContent.Create(request));

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region DeleteClip Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task DeleteClip_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _fixture.CreateUnauthenticatedClient();
        var clipId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/clips/videos/{clipId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task DeleteClip_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await client.DeleteAsync($"/clips/videos/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Snake Case JSON Serialization Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task Endpoints_UseSnakeCaseJsonSerialization()
    {
        // Arrange
        var client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        var response = await client.GetAsync("/clips/categories/ApexLegends/videos?page=1&pageSize=10");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("total_pages"); // snake_case
        content.Should().NotContain("TotalPages"); // NOT PascalCase
    }

    #endregion
}
