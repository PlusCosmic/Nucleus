# Nucleus Test Infrastructure

This directory contains the test infrastructure for the Nucleus project. The infrastructure is fully set up and ready to use.

## Quick Start

Run all tests:
```bash
dotnet test
```

Run tests from a specific file:
```bash
dotnet test --filter "FullyQualifiedName~ExampleTests"
```

## Infrastructure Components

### Test Fixtures

Located in `TestFixtures/`:

- **DatabaseFixture.cs** - PostgreSQL Testcontainers setup for database integration tests
  - Automatically starts a PostgreSQL container
  - Runs Evolve migrations
  - Provides connection for tests
  - Supports cleanup between tests

- **WebApplicationFixture.cs** - Test server for endpoint integration tests
  - Creates a test web application with test database
  - Provides authenticated and unauthenticated HTTP clients
  - Supports test authentication headers

- **ExternalApiFixture.cs** - WireMock server for mocking external APIs
  - Mock Discord OAuth endpoints
  - Mock Apex Legends API
  - Mock Bunny CDN endpoints

### Test Helpers

Located in `Helpers/`:

- **AuthHelper.cs** - Authentication test utilities
  - Create test ClaimsPrincipals
  - Manage whitelist.json for tests
  - Standard test user IDs

- **DatabaseHelper.cs** - Database seeding and cleanup
  - Clear all tables
  - Seed test data (users, clips, links)
  - Complete test environment setup

### Test Builders

Located in `Builders/`:

- **ClipBuilder.cs** - Fluent builder for Clip objects
- **LinkBuilder.cs** - Fluent builder for Link objects
- **DiscordUserBuilder.cs** - Fluent builder for DiscordUser objects

## Usage Examples

### Unit Test with Builder

```csharp
[Fact]
public void TestClipCreation()
{
    // Arrange
    var clip = new ClipBuilder()
        .WithOwnerId(Guid.NewGuid())
        .AsApexRanked()
        .WithTag("ranked")
        .Build();

    // Act & Assert
    clip.Tags.Should().Contain("ranked");
}
```

### Database Integration Test

```csharp
public class MyDatabaseTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public MyDatabaseTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TestDatabaseOperation()
    {
        // Arrange
        await DatabaseHelper.ClearAllTablesAsync(_fixture.Connection);
        var userId = await DatabaseHelper.SeedDiscordUserAsync(_fixture.Connection);

        // Act
        var statements = new ClipsStatements(_fixture.Connection);
        // ... test your database code

        // Assert
        // ... verify results
    }
}
```

### Endpoint Integration Test

```csharp
public class MyEndpointTests : IClassFixture<WebApplicationFixture>
{
    private readonly WebApplicationFixture _fixture;

    public MyEndpointTests(WebApplicationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TestAuthenticatedEndpoint()
    {
        // Arrange
        WebApplicationFixture.CreateTestWhitelist("123456789012345678");
        var client = _fixture.CreateAuthenticatedClient(
            discordId: "123456789012345678",
            username: "testuser"
        );

        // Act
        var response = await client.GetAsync("/clips");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Cleanup
        WebApplicationFixture.CleanupTestWhitelist();
    }
}
```

### External API Mock Test

```csharp
[Fact]
public void TestExternalApiCall()
{
    // Arrange
    using var apiFixture = new ExternalApiFixture();
    apiFixture.StartServer();
    apiFixture.MockApexMapRotation("Kings Canyon", "Worlds Edge");

    // Act
    // ... make HTTP call to apiFixture.BaseUrl

    // Assert
    // ... verify behavior
}
```

## Test Categories

Use `[Trait("Category", "...")]` to categorize tests:

```csharp
[Fact]
[Trait("Category", "Unit")]
public void MyUnitTest() { }

[Fact]
[Trait("Category", "Integration")]
public void MyIntegrationTest() { }
```

Run by category:
```bash
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"
```

## Best Practices

1. **Use Builders** - Prefer builders over manual object construction
2. **Clean up** - Use `DatabaseHelper.ClearAllTablesAsync()` between tests
3. **Standard test users** - Use `AuthHelper.DefaultTestDiscordId` for consistency
4. **Fluent assertions** - Use FluentAssertions for readable test assertions
5. **Test isolation** - Each test should be independent and not rely on others

## Packages Included

- **xUnit** - Test framework
- **Moq** - Mocking framework
- **FluentAssertions** - Assertion library
- **Testcontainers.PostgreSql** - Database containers
- **Microsoft.AspNetCore.Mvc.Testing** - API testing
- **Bogus** - Test data generation (ready to use)
- **WireMock.Net** - HTTP mocking

## Next Steps

Refer to `/TESTING.md` in the project root for the complete testing plan and implementation roadmap.

Example tests are available in `Examples/ExampleTests.cs` to see the infrastructure in action.
