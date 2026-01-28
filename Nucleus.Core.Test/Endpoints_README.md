# Endpoint Tests Implementation

This document summarizes the implemented endpoint tests for the Nucleus API.

## What Was Implemented

All endpoint test suites from Phase 4 of the testing plan have been implemented:

### 1. ClipsEndpoints Tests (`Clips/ClipsEndpointsTests.cs`)

**Coverage: 20+ tests**

- ✅ GET `/clips/categories` - Get clip categories
- ✅ GET `/clips/categories/{category}/videos` - Get videos by category with pagination
- ✅ GET `/clips/categories/{category}/videos/unviewed` - Get unviewed videos
- ✅ POST `/clips/categories/{category}/videos` - Create new video
- ✅ GET `/clips/videos/{clipId}` - Get video by ID
- ✅ POST `/clips/videos/{clipId}/view` - Mark video as viewed
- ✅ POST `/clips/videos/{clipId}/tags` - Add tag to clip
- ✅ DELETE `/clips/videos/{clipId}/tags/{tag}` - Remove tag from clip
- ✅ GET `/clips/tags/top` - Get top tags
- ✅ PATCH `/clips/videos/{clipId}/title` - Update clip title
- ✅ DELETE `/clips/videos/{clipId}` - Delete clip

**Test Scenarios:**
- Authentication/authorization checks
- Unauthenticated access returns 401
- Non-existent resources return 404
- MD5 hash duplicate detection (409 Conflict)
- JSON serialization (snake_case)
- Pagination behavior

### 2. LinksEndpoints Tests (`Links/LinksEndpointsTests.cs`)

**Coverage: 13+ tests**

- ✅ GET `/links` - Get user's links
- ✅ POST `/links` - Add new link
- ✅ DELETE `/links/{id}` - Delete link

**Test Scenarios:**
- Authentication requirements
- User isolation (users only see their own links)
- Link creation with metadata fetching
- Successful deletion returns 204 No Content
- Non-existent link deletion returns 404
- Database persistence verification
- JSON serialization

### 3. ApexEndpoints Tests (`ApexLegends/ApexEndpointsTests.cs`)

**Coverage: 7+ tests**

- ✅ GET `/apex-legends/map-rotation` - Get current map rotation

**Test Scenarios:**
- Public endpoint (no authentication required)
- Returns 200 OK or 503 Service Unavailable
- Valid response structure with all map data
- Error handling for external API failures
- JSON structure validation
- Caching behavior verification

### 4. AuthEndpoints Tests (`Auth/AuthEndpointsTests.cs`)

**Coverage: 19+ tests**

- ✅ GET `/auth/discord/login` - Initiate Discord OAuth
- ✅ GET `/auth/post-login-redirect` - Handle OAuth callback
- ✅ POST `/auth/logout` - Logout user
- ✅ POST `/auth/refresh` - Refresh authentication token

**Test Scenarios:**
- OAuth flow initiation
- Return URL handling
- **Security: Open redirect protection**
- Trusted domain validation
- Cookie management
- CSRF state parameter generation
- Refresh token validation
- Malicious URL rejection tests

### 5. FFmpegEndpoints Tests (`Clips/FFmpegEndpointsTests.cs`)

**Coverage: 11+ tests**

- ✅ GET `/ffmpeg/download/{videoId}` - Download video via FFmpeg

**Test Scenarios:**
- Authentication/authorization requirements
- Non-existent video returns 404
- Correct Content-Type (video/mp4)
- Content-Disposition header with filename
- Range request support (206 Partial Content)
- Cancellation token support
- Error handling for FFmpeg failures
- Error handling for Bunny CDN failures
- Invalid GUID handling

## Test Statistics

- **Total Test Files:** 5
- **Total Tests:** 70+
- **Coverage Areas:**
  - Authentication & Authorization
  - HTTP Status Codes
  - Request/Response Validation
  - JSON Serialization
  - Security (CSRF, Open Redirect)
  - Error Handling
  - Pagination
  - Resource Isolation

## Running the Tests

### Prerequisites

**Docker must be running** for endpoint tests to work. The tests use Testcontainers to spin up a PostgreSQL database.

### Run All Endpoint Tests

```bash
dotnet test --filter "Category=Endpoint"
```

### Run Specific Endpoint Test Suite

```bash
# Clips endpoints
dotnet test --filter "FullyQualifiedName~ClipsEndpointsTests"

# Links endpoints
dotnet test --filter "FullyQualifiedName~LinksEndpointsTests"

# Apex Legends endpoints
dotnet test --filter "FullyQualifiedName~ApexEndpointsTests"

# Auth endpoints
dotnet test --filter "FullyQualifiedName~AuthEndpointsTests"

# FFmpeg endpoints
dotnet test --filter "FullyQualifiedName~FFmpegEndpointsTests"
```

### Run Security Tests Only

```bash
dotnet test --filter "Category=Security"
```

## Known Limitations

### 1. Docker Dependency

Endpoint tests require Docker to be running because they use Testcontainers to spin up PostgreSQL. If Docker is not available:

- Tests will fail with connection errors
- Consider using a shared test database for CI/CD environments
- Or use Docker-in-Docker for containerized CI

### 2. External API Dependencies

Some tests interact with external services:

- **Apex Legends API**: Tests handle both success (200) and failure (503) scenarios
- **Bunny CDN**: Tests expect 404 or 500 when service is unavailable
- **Discord OAuth**: Full OAuth flow testing is limited (requires mocking)

### 3. Test Isolation

Current endpoint tests share the same database instance. For better isolation:

- Consider using DatabaseFixture with cleanup between tests
- Or use transaction rollback after each test
- Database Helper methods are available for seeding test data

### 4. FFmpeg/Docker Integration

FFmpeg endpoint tests are limited without:

- A running FFmpeg container
- Shared volume configuration
- Actual video files in Bunny CDN

Tests verify endpoint behavior but cannot test full video download functionality.

## Test Patterns Used

### 1. AAA Pattern (Arrange-Act-Assert)

```csharp
[Fact]
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
}
```

### 2. Fixture-Based Setup

All endpoint tests use `WebApplicationFixture` for:
- Test server setup
- Database container management
- Authentication simulation
- Whitelist management

### 3. FluentAssertions

All assertions use FluentAssertions for readable test code:

```csharp
response.StatusCode.Should().Be(HttpStatusCode.OK);
response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
content.Should().Contain("expected_value");
```

### 4. Test Traits

Tests are categorized for filtering:

```csharp
[Trait("Category", "Endpoint")]
[Trait("Category", "Security")]
```

## Future Improvements

### 1. Database Seeding

Add database seeding to test more realistic scenarios:

```csharp
public async Task InitializeAsync()
{
    var userId = await DatabaseHelper.SeedDiscordUserAsync(_connection, _testDiscordId);
    await DatabaseHelper.SeedClipAsync(_connection, userId, "Test Clip");
}
```

### 2. External API Mocking

Use WireMock.Net to mock external APIs:

```csharp
_apiFixture.MockApexMapRotation("Kings Canyon", "Worlds Edge");
_apiFixture.MockDiscordOAuth("test_token");
```

### 3. Response Schema Validation

Add JSON schema validation for response bodies:

```csharp
var schema = JsonSchema.FromType<PagedClipsResponse>();
schema.Validate(responseContent);
```

### 4. Performance Testing

Add performance benchmarks:

```csharp
[Fact]
public async Task GetClips_CompletesWithin500ms()
{
    var stopwatch = Stopwatch.StartNew();
    await client.GetAsync("/clips/categories/ApexLegends/videos");
    stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);
}
```

### 5. Integration with Real OAuth

Mock Discord OAuth for more complete auth testing:

```csharp
_apiFixture.MockDiscordTokenExchange("auth_code", "access_token");
_apiFixture.MockDiscordUserProfile("123456", "testuser");
```

## Troubleshooting

### "Container not started" Error

**Cause:** Docker is not running or Testcontainers cannot start PostgreSQL.

**Solution:**
1. Start Docker Desktop
2. Verify Docker is running: `docker ps`
3. Check Docker has sufficient resources (2GB+ RAM)

### Connection Timeout Errors

**Cause:** Database container is slow to start.

**Solution:**
- Increase test timeout
- Check Docker performance
- Use a persistent test database instead of containers

### "Whitelist not found" Errors

**Cause:** Tests create `whitelist.json` but cleanup fails.

**Solution:**
- Check test cleanup in `DisposeAsync`
- Manually delete `whitelist.json` from test output directory
- Ensure test isolation

### Migration Errors

**Cause:** Database migrations fail to run.

**Solution:**
- Check migration files are copied to output directory
- Verify connection string is correct
- Check PostgreSQL container logs

## Best Practices

1. ✅ **Always clean up** - Use `IAsyncLifetime` for fixture setup/teardown
2. ✅ **Test isolation** - Each test should be independent
3. ✅ **Meaningful names** - Test names describe what they verify
4. ✅ **One assertion focus** - Each test verifies one behavior
5. ✅ **Use test categories** - Tag tests for easy filtering
6. ✅ **Handle async properly** - Always await async operations
7. ✅ **Test error cases** - Don't just test happy paths
8. ✅ **Security testing** - Test authentication, authorization, and input validation

## Additional Resources

- Main Testing Plan: `/TESTING.md`
- Test Infrastructure Guide: `/Nucleus.Test/README.md`
- Example Tests: `/Nucleus.Test/Examples/ExampleTests.cs`
