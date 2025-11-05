# Testing Plan for Nucleus

## Current State
- Test project exists (`Nucleus.Test`) with xUnit configured
- Zero test coverage currently
- No test infrastructure or utilities in place

---

## Testing Strategy Overview

### Testing Pyramid Approach

```
        /\
       /E2E\         ← Few (Endpoint integration tests)
      /------\
     /  INT   \      ← Some (Database, External APIs)
    /----------\
   /   UNIT     \    ← Many (Services, Business Logic)
  /--------------\
```

---

## Phase 1: Test Infrastructure Setup (Priority: HIGH)

### 1.1 Add Required Testing Packages

```bash
dotnet add Nucleus.Test package Moq                      # Mocking framework
dotnet add Nucleus.Test package FluentAssertions         # Better assertions
dotnet add Nucleus.Test package Testcontainers.PostgreSql # Database integration tests
dotnet add Nucleus.Test package Microsoft.AspNetCore.Mvc.Testing # API testing
dotnet add Nucleus.Test package Bogus                    # Test data generation
dotnet add Nucleus.Test package WireMock.Net            # HTTP mocking for external APIs
```

### 1.2 Create Test Utilities

- `TestFixtures/DatabaseFixture.cs` - PostgreSQL test container setup
- `TestFixtures/WebApplicationFactory.cs` - Test server configuration
- `Builders/` - Test data builders for each domain model
- `Helpers/AuthenticationHelper.cs` - Mock authentication for tests
- `Helpers/DatabaseHelper.cs` - Database seeding and cleanup

---

## Phase 2: Unit Tests (Priority: HIGH)

### 2.1 Service Layer Tests (No external dependencies)

#### ClipService Tests (`Nucleus.Test/Clips/ClipServiceTests.cs`)
- Test tag limit enforcement (max 5 tags)
- Test MD5 deduplication logic
- Test clip creation with valid/invalid data
- Test clip filtering by category
- Test view tracking logic
- Test permission validation (user owns clip)

#### LinksService Tests (`Nucleus.Test/Links/LinksServiceTests.cs`)
- Test link creation/update/deletion
- Test duplicate URL handling
- Test pagination logic
- Test user isolation (users can't see others' links)

#### MapService Tests (`Nucleus.Test/ApexLegends/MapServiceTests.cs`)
- Test map rotation parsing
- Test current map calculation based on timestamps
- Test map rotation caching logic
- Test handling of malformed API responses

#### BunnyService Tests (`Nucleus.Test/Clips/Bunny/BunnyServiceTests.cs`)
- Mock HttpClient for Bunny CDN API calls
- Test video creation request formatting
- Test error handling for failed uploads
- Test collection management

#### PageMetadataFetcher Tests (`Nucleus.Test/Links/PageMetadataFetcherTests.cs`)
- Test HTML parsing for title/description/favicon
- Test handling of malformed HTML
- Test timeout handling
- Test different meta tag formats (Open Graph, Twitter Cards)

### 2.2 Middleware Tests

#### WhitelistMiddleware Tests (`Nucleus.Test/Auth/WhitelistMiddlewareTests.cs`)
- Test whitelist file loading
- Test authorized user passes through
- Test unauthorized user gets 403
- Test missing whitelist.json handling
- Test malformed whitelist.json handling
- Test public endpoints bypass whitelist

#### CookieAuth Tests (`Nucleus.Test/Auth/CookieAuthTests.cs`)
- Test cookie creation with Discord user info
- Test cookie expiration (7 days)
- Test cookie parsing and claims extraction
- Test HttpOnly and Secure flags

---

## Phase 3: Integration Tests (Priority: MEDIUM)

### 3.1 Database Integration Tests (Use Testcontainers)

#### ClipsStatements Tests (`Nucleus.Test/Clips/ClipsStatementsTests.cs`)
- Test CRUD operations with real PostgreSQL
- Test complex queries (filtering, sorting, pagination)
- Test foreign key constraints
- Test transaction rollback on errors
- Test MD5 uniqueness constraint
- Test cascade deletes

#### ApexStatements Tests (`Nucleus.Test/ApexLegends/ApexStatementsTests.cs`)
- Test map rotation upsert logic
- Test querying current/upcoming maps
- Test timestamp filtering
- Test data consistency after updates

#### LinksStatements Tests (`Nucleus.Test/Links/LinksStatementsTests.cs`)
- Test CRUD operations
- Test user isolation queries
- Test ordering by most recent

#### DiscordStatements Tests (`Nucleus.Test/Discord/DiscordStatementsTests.cs`)
- Test user upsert logic
- Test user lookup by Discord ID
- Test user profile updates

### 3.2 External API Integration Tests (Use WireMock)

#### Apex Legends API Tests (`Nucleus.Test/ApexLegends/MapRefreshServiceTests.cs`)
- Mock mozambiquehe.re API responses
- Test successful data refresh
- Test API failure handling
- Test rate limiting behavior
- Test background service lifecycle

#### Discord OAuth Tests (`Nucleus.Test/Auth/DiscordOAuthTests.cs`)
- Mock Discord OAuth endpoints
- Test authorization flow
- Test token exchange
- Test user profile fetching
- Test error responses (invalid code, expired token)

---

## Phase 4: Endpoint Tests (Priority: MEDIUM)

Use `WebApplicationFactory` to test full request/response cycle

### ClipsEndpoints Tests (`Nucleus.Test/Clips/ClipsEndpointsTests.cs`)
- Test GET /clips returns user's clips
- Test POST /clips creates clip (requires auth)
- Test PUT /clips/{id} updates clip (owner only)
- Test DELETE /clips/{id} deletes clip (owner only)
- Test tag management endpoints
- Test view tracking endpoints
- Test 401 for unauthenticated requests
- Test 403 for non-whitelisted users

### LinksEndpoints Tests (`Nucleus.Test/Links/LinksEndpointsTests.cs`)
- Test CRUD operations via HTTP
- Test authorization enforcement
- Test JSON serialization (snake_case)
- Test validation error responses

### ApexEndpoints Tests (`Nucleus.Test/ApexLegends/ApexEndpointsTests.cs`)
- Test GET /apex-legends/map-rotation
- Test response format matches API contract
- Test caching behavior

### AuthEndpoints Tests (`Nucleus.Test/Auth/AuthEndpointsTests.cs`)
- Test /auth/discord/login redirects to Discord
- Test /auth/discord/callback exchanges code for token
- Test cookie is set after successful auth
- Test /auth/logout clears cookie

### FFmpegEndpoints Tests (`Nucleus.Test/Clips/FFmpeg/FFmpegEndpointsTests.cs`)
- Test video download initiation
- Test file streaming with DeleteOnClose
- Test error handling for failed downloads

---

## Phase 5: Test Data Management (Priority: LOW)

### 5.1 Test Builders

Create fluent builders for test data:

```csharp
// Example: ClipBuilder
var clip = new ClipBuilder()
    .WithTitle("Test Clip")
    .WithCategory(ClipCategoryEnum.ApexLegends)
    .WithTags("ranked", "controller")
    .WithOwner(testUserId)
    .Build();
```

### 5.2 Database Seeders
- Seed realistic test data for integration tests
- Reset database state between tests
- Provide fixtures for common scenarios

---

## Phase 6: Special Test Cases (Priority: LOW)

### 6.1 Security Tests
- Test SQL injection prevention in raw queries
- Test XSS prevention in link metadata
- Test CORS policy enforcement
- Test authentication bypass attempts
- Test authorization bypass (user A accessing user B's data)

### 6.2 Edge Cases
- Test handling of null/empty inputs
- Test boundary values (max tag length, max title length)
- Test concurrent modifications
- Test large result sets
- Test malformed JSON inputs

### 6.3 Background Service Tests
- Test MapRefreshService runs on schedule
- Test exception handling doesn't crash service
- Test graceful shutdown

---

## Phase 7: Test Organization (Priority: MEDIUM)

### Directory Structure

```
Nucleus.Test/
├── TestFixtures/
│   ├── DatabaseFixture.cs
│   ├── WebApplicationFixture.cs
│   └── ExternalApiFixture.cs
├── Builders/
│   ├── ClipBuilder.cs
│   ├── LinkBuilder.cs
│   └── UserBuilder.cs
├── Helpers/
│   ├── AuthHelper.cs
│   ├── DatabaseHelper.cs
│   └── HttpHelper.cs
├── ApexLegends/
│   ├── MapServiceTests.cs
│   ├── ApexStatementsTests.cs
│   └── MapRefreshServiceTests.cs
├── Auth/
│   ├── WhitelistMiddlewareTests.cs
│   └── AuthEndpointsTests.cs
├── Clips/
│   ├── ClipServiceTests.cs
│   ├── ClipsStatementsTests.cs
│   ├── ClipsEndpointsTests.cs
│   └── Bunny/
│       └── BunnyServiceTests.cs
├── Discord/
│   └── DiscordStatementsTests.cs
├── Links/
│   ├── LinksServiceTests.cs
│   ├── LinksStatementsTests.cs
│   └── PageMetadataFetcherTests.cs
└── Transport/ (if needed)
```

---

## Testing Best Practices for This Project

1. **Use Testcontainers for database tests** - Ensures tests run against real PostgreSQL
2. **Mock HttpClient for external APIs** - Prevents rate limiting and flaky tests
3. **Create test-specific whitelist.json** - Use predictable test user IDs
4. **Use xUnit's IClassFixture** - Share expensive setup (DB, test server)
5. **Tag tests by type** - `[Trait("Category", "Integration")]` for filtering
6. **Test Dapper mapping** - Ensure snake_case → PascalCase mapping works
7. **Mock FFmpeg container** - Don't require Docker-in-Docker for tests
8. **Test migrations** - Verify Evolve migrations apply cleanly

---

## Recommended Implementation Order

1. **Week 1**: Infrastructure setup + Unit tests for services
2. **Week 2**: Database integration tests (Statements classes)
3. **Week 3**: Endpoint tests + Authentication tests
4. **Week 4**: External API mocking + Background service tests
5. **Week 5**: Edge cases + Security tests + CI/CD integration

---

## CI/CD Integration

Add to `.github/workflows/test.yml`:

```yaml
name: Test

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --configuration Release --no-restore

    - name: Run tests
      run: dotnet test --configuration Release --no-build --logger "trx" /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura

    - name: Generate coverage report
      run: |
        dotnet tool install -g dotnet-reportgenerator-globaltool
        reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage -reporttypes:Html

    - name: Upload coverage report
      uses: actions/upload-artifact@v4
      with:
        name: coverage-report
        path: coverage
```

---

## Success Metrics

- **Code Coverage**: Aim for 70%+ overall, 90%+ for business logic
- **Test Execution Time**: < 2 minutes for full suite
- **Flakiness**: < 1% test failure rate on clean runs
- **Maintainability**: New features require corresponding tests

---

## Running Tests

### Run all tests
```bash
dotnet test
```

### Run specific test class
```bash
dotnet test --filter "FullyQualifiedName~Nucleus.Test.Clips.ClipServiceTests"
```

### Run specific test method
```bash
dotnet test --filter "FullyQualifiedName~Nucleus.Test.Clips.ClipServiceTests.TestTagLimitEnforcement"
```

### Run tests by category
```bash
# Unit tests only
dotnet test --filter "Category=Unit"

# Integration tests only
dotnet test --filter "Category=Integration"
```

### Run tests with coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

---

## Test Writing Guidelines

### Example Unit Test Structure

```csharp
public class ClipServiceTests
{
    [Fact]
    public async Task CreateClip_WithMoreThan5Tags_ReturnsError()
    {
        // Arrange
        var mockStatements = new Mock<ClipsStatements>();
        var service = new ClipService(mockStatements.Object);
        var tags = new[] { "tag1", "tag2", "tag3", "tag4", "tag5", "tag6" };

        // Act
        var result = await service.CreateClipAsync(userId, title, tags);

        // Assert
        result.Should().BeNull();
        // or assert error message
    }
}
```

### Example Integration Test Structure

```csharp
public class ClipsStatementsTests : IClassFixture<DatabaseFixture>
{
    private readonly NpgsqlConnection _connection;

    public ClipsStatementsTests(DatabaseFixture fixture)
    {
        _connection = fixture.Connection;
    }

    [Fact]
    public async Task InsertClip_WithValidData_ReturnsClipId()
    {
        // Arrange
        var statements = new ClipsStatements(_connection);
        var testClip = new ClipBuilder().Build();

        // Act
        var clipId = await statements.InsertClipAsync(testClip);

        // Assert
        clipId.Should().NotBeEmpty();
    }
}
```

### Example Endpoint Test Structure

```csharp
public class ClipsEndpointsTests : IClassFixture<WebApplicationFixture>
{
    private readonly HttpClient _client;

    public ClipsEndpointsTests(WebApplicationFixture fixture)
    {
        _client = fixture.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task GetClips_AsAuthenticatedUser_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/clips");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var clips = await response.Content.ReadFromJsonAsync<List<Clip>>();
        clips.Should().NotBeNull();
    }
}
```

---

## Additional Resources

- [xUnit Documentation](https://xunit.net/)
- [Testcontainers for .NET](https://dotnet.testcontainers.org/)
- [Moq Documentation](https://github.com/moq/moq4)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [ASP.NET Core Testing](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
