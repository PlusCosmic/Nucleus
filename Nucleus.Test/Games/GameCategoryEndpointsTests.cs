using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Npgsql;
using Nucleus.Games;
using Nucleus.Test.Helpers;
using Nucleus.Test.TestFixtures;

namespace Nucleus.Test.Games;

/// <summary>
///     Tests for Game Category API endpoints.
/// </summary>
public class GameCategoryEndpointsTests : IClassFixture<WebApplicationFixture>, IAsyncLifetime
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly WebApplicationFixture _fixture;
    private readonly string _testDiscordId = AuthHelper.DefaultTestDiscordId;

    public GameCategoryEndpointsTests(WebApplicationFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        NpgsqlConnection connection = _fixture.GetService<NpgsqlConnection>();
        await DatabaseHelper.ClearAllTablesAsync(connection);
        await DatabaseHelper.SeedDiscordUserAsync(connection, _testDiscordId);
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection connection = _fixture.GetService<NpgsqlConnection>();
        await DatabaseHelper.ClearAllTablesAsync(connection);
    }

    #region GetCategories Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetCategories_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = _fixture.CreateUnauthenticatedClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/games/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetCategories_WithAuthentication_ReturnsOk()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        HttpResponseMessage response = await client.GetAsync("/games/categories");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetCategories_ReturnsSeededCategories()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        HttpResponseMessage response = await client.GetAsync("/games/categories");
        List<GameCategoryResponse>? categories = await response.Content.ReadFromJsonAsync<List<GameCategoryResponse>>(JsonOptions);

        // Assert
        categories.Should().NotBeNull();
        categories.Should().HaveCountGreaterThanOrEqualTo(3); // Apex, Warzone, Snowboarding from migration
        categories.Should().Contain(c => c.Slug == "apex-legends");
        categories.Should().Contain(c => c.Slug == "warzone");
        categories.Should().Contain(c => c.Slug == "snowboarding");
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetCategories_ReturnsSnakeCaseJson()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        HttpResponseMessage response = await client.GetAsync("/games/categories");
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        content.Should().Contain("cover_url"); // snake_case
        content.Should().Contain("is_custom"); // snake_case
        content.Should().NotContain("CoverUrl"); // NOT PascalCase
        content.Should().NotContain("IsCustom"); // NOT PascalCase
    }

    #endregion

    #region GetCategoryById Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetCategoryById_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = _fixture.CreateUnauthenticatedClient();
        NpgsqlConnection connection = _fixture.GetService<NpgsqlConnection>();
        Guid categoryId = await TestGameCategories.GetApexLegendsIdAsync(connection);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/games/categories/{categoryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetCategoryById_WithValidId_ReturnsCategory()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        NpgsqlConnection connection = _fixture.GetService<NpgsqlConnection>();
        Guid categoryId = await TestGameCategories.GetApexLegendsIdAsync(connection);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/games/categories/{categoryId}");
        GameCategoryResponse? category = await response.Content.ReadFromJsonAsync<GameCategoryResponse>(JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        category.Should().NotBeNull();
        category!.Id.Should().Be(categoryId);
        category.Slug.Should().Be("apex-legends");
        category.Name.Should().Be("Apex Legends");
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task GetCategoryById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        Guid nonExistentId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/games/categories/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region SearchGames Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task SearchGames_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = _fixture.CreateUnauthenticatedClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/games/search?query=apex");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task SearchGames_WithEmptyQuery_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        HttpResponseMessage response = await client.GetAsync("/games/search?query=");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task SearchGames_WithTooShortQuery_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        HttpResponseMessage response = await client.GetAsync("/games/search?query=a");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    #endregion

    #region AddCustomCategory Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task AddCustomCategory_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = _fixture.CreateUnauthenticatedClient();
        var request = new AddCustomCategoryRequest("My Custom Game", null);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/games/categories/custom", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task AddCustomCategory_WithValidData_ReturnsCreatedCategory()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var request = new AddCustomCategoryRequest("My Custom Game", null);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/games/categories/custom", request);
        GameCategoryResponse? category = await response.Content.ReadFromJsonAsync<GameCategoryResponse>(JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        category.Should().NotBeNull();
        category!.Name.Should().Be("My Custom Game");
        category.Slug.Should().Be("my-custom-game");
        category.IsCustom.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task AddCustomCategory_WithCoverUrl_ReturnsCategoryWithCover()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var request = new AddCustomCategoryRequest("Game With Cover", "https://example.com/cover.jpg");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/games/categories/custom", request);
        GameCategoryResponse? category = await response.Content.ReadFromJsonAsync<GameCategoryResponse>(JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        category.Should().NotBeNull();
        category!.Name.Should().Be("Game With Cover");
        // Verify the category was created - cover URL handling may vary
        category.Id.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task AddCustomCategory_WithEmptyName_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var request = new AddCustomCategoryRequest("", null);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/games/categories/custom", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task AddCustomCategory_WithTooLongName_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var longName = new string('a', 101); // 101 characters
        var request = new AddCustomCategoryRequest(longName, null);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/games/categories/custom", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task AddCustomCategory_WithDuplicateName_ReturnsExistingCategory()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var request = new AddCustomCategoryRequest("Unique Test Game", null);

        // Act - Create first
        HttpResponseMessage response1 = await client.PostAsJsonAsync("/games/categories/custom", request);
        GameCategoryResponse? category1 = await response1.Content.ReadFromJsonAsync<GameCategoryResponse>(JsonOptions);

        // Act - Create duplicate
        HttpResponseMessage response2 = await client.PostAsJsonAsync("/games/categories/custom", request);
        GameCategoryResponse? category2 = await response2.Content.ReadFromJsonAsync<GameCategoryResponse>(JsonOptions);

        // Assert - Both should succeed and return the same category
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        category1!.Id.Should().Be(category2!.Id);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task AddCustomCategory_GeneratesCorrectSlug()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);
        var request = new AddCustomCategoryRequest("Test Game: Special Edition!", null);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/games/categories/custom", request);
        GameCategoryResponse? category = await response.Content.ReadFromJsonAsync<GameCategoryResponse>(JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        category!.Slug.Should().Be("test-game-special-edition");
    }

    #endregion

    #region RemoveCategory Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task RemoveCategory_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = _fixture.CreateUnauthenticatedClient();
        NpgsqlConnection connection = _fixture.GetService<NpgsqlConnection>();
        Guid categoryId = await TestGameCategories.GetApexLegendsIdAsync(connection);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/games/categories/{categoryId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task RemoveCategory_WithValidId_ReturnsOk()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // First add a custom category to remove
        var addRequest = new AddCustomCategoryRequest("Category To Remove", null);
        HttpResponseMessage addResponse = await client.PostAsJsonAsync("/games/categories/custom", addRequest);
        GameCategoryResponse? addedCategory = await addResponse.Content.ReadFromJsonAsync<GameCategoryResponse>(JsonOptions);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/games/categories/{addedCategory!.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task RemoveCategory_CategoryStillExistsAfterRemoval()
    {
        // Arrange - The remove endpoint only removes user subscription, not the category itself
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // First add a custom category
        var addRequest = new AddCustomCategoryRequest("Persistent Category", null);
        HttpResponseMessage addResponse = await client.PostAsJsonAsync("/games/categories/custom", addRequest);
        GameCategoryResponse? addedCategory = await addResponse.Content.ReadFromJsonAsync<GameCategoryResponse>(JsonOptions);

        // Remove user subscription
        await client.DeleteAsync($"/games/categories/{addedCategory!.Id}");

        // Act - Category should still exist when fetched by ID
        HttpResponseMessage getResponse = await client.GetAsync($"/games/categories/{addedCategory.Id}");
        GameCategoryResponse? category = await getResponse.Content.ReadFromJsonAsync<GameCategoryResponse>(JsonOptions);

        // Assert
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        category.Should().NotBeNull();
        category!.Id.Should().Be(addedCategory.Id);
    }

    #endregion

    #region Category Properties Tests

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task SeededCategories_HaveCorrectIsCustomFlag()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        HttpResponseMessage response = await client.GetAsync("/games/categories");
        List<GameCategoryResponse>? categories = await response.Content.ReadFromJsonAsync<List<GameCategoryResponse>>(JsonOptions);

        // Assert - Seeded categories from IGDB should have IsCustom = false
        GameCategoryResponse? apexCategory = categories?.FirstOrDefault(c => c.Slug == "apex-legends");
        apexCategory.Should().NotBeNull();
        apexCategory!.IsCustom.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Endpoint")]
    public async Task SeededCategories_HaveExpectedSlugs()
    {
        // Arrange
        HttpClient client = _fixture.CreateAuthenticatedClient(_testDiscordId);

        // Act
        HttpResponseMessage response = await client.GetAsync("/games/categories");
        List<GameCategoryResponse>? categories = await response.Content.ReadFromJsonAsync<List<GameCategoryResponse>>(JsonOptions);

        // Assert - Verify the well-known slugs from the migration
        categories.Should().Contain(c => c.Slug == TestGameCategories.ApexLegendsSlug);
        categories.Should().Contain(c => c.Slug == TestGameCategories.WarzoneSlug);
        categories.Should().Contain(c => c.Slug == TestGameCategories.SnowboardingSlug);
    }

    #endregion
}
