# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Nucleus is a personal media and content management dashboard built with ASP.NET Core. It provides a backend API for managing video clips (primarily gaming footage), bookmarks, and tracking Apex Legends map rotations. The application is designed for a small whitelisted user base using Discord OAuth authentication.

## Technology Stack

- **.NET 10.0** (RC) with C# 14
- **ASP.NET Core** with Minimal APIs (no MVC controllers)
- **PostgreSQL 16** database
- **Dapper** for data access (micro-ORM with raw SQL)
- **Evolve** for database migrations (SQL-based, not EF Core)
- **xUnit** for testing
- **Docker & Docker Compose** for deployment
- **Bunny CDN** for video hosting
- **FFmpeg** (via linuxserver/ffmpeg container) for video transcoding
- **Discord OAuth** for authentication

## Development Commands

### Building and Running

```bash
# Build the solution
dotnet build

# Run the application (requires PostgreSQL)
dotnet run --project Nucleus/Nucleus.csproj

# Run with Docker Compose (recommended)
docker compose up --build

# Run tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~NamespaceOrClassName.TestMethodName"
```

### Database Management

Database migrations run automatically on startup when `ASPNETCORE_ENVIRONMENT` is set. Migrations are located in `Nucleus/db/migrations/` and follow Evolve versioning (e.g., `V1__baseline_schema.sql`).

To create a new migration:
1. Add a new SQL file: `Nucleus/db/migrations/V{number}__{description}.sql`
2. Write raw SQL DDL statements
3. Restart the application to apply migrations

### Docker Operations

```bash
# Build and start all services
docker compose up --build

# View logs
docker compose logs -f nucleus

# Stop all services
docker compose down

# Clean rebuild
docker compose down -v && docker compose up --build
```

## Architecture Overview

### Request Flow

```
HTTP Request
    ↓
Caddy (reverse proxy) → port 8080
    ↓
ASP.NET Core Minimal APIs
    ↓
WhitelistMiddleware (checks Discord ID against whitelist.json)
    ↓
Endpoint Handlers (Auth required)
    ↓
Service Layer (business logic)
    ↓
*Statements Classes (Dapper + raw SQL)
    ↓
PostgreSQL Database
```

### Code Organization Pattern

The codebase follows a consistent pattern across all modules:

- **{Module}Endpoints.cs**: Minimal API endpoint definitions using `MapGroup()` and extension methods
- **{Module}Service.cs**: Business logic and orchestration
- **{Module}Statements.cs**: Dapper-based data access with raw SQL queries
- **Models/DTOs**: Record types for data transfer objects

### Core Modules

1. **Auth/** - Discord OAuth authentication and whitelist-based authorization
2. **Discord/** - User identity management and profile data
3. **Clips/** - Video clip management with Bunny CDN integration
   - **Clips/Bunny/** - CDN API client for video operations
   - **Clips/FFmpeg/** - Docker-based video transcoding and download
4. **Links/** - Bookmark management with metadata extraction
5. **ApexLegends/** - Map rotation tracking with background refresh service

### Key Architectural Patterns

**Minimal APIs**: Endpoints defined as extension methods returning typed results
```csharp
Results<Ok<T>, NotFound, BadRequest<string>, UnauthorizedHttpResult>
```

**Dapper with Raw SQL**: All database access uses hand-written SQL with Dapper
- `*Statements` classes contain SQL queries
- Column mapping handled automatically via `DefaultTypeMap.MatchNamesWithUnderscores = true` in [Program.cs](Nucleus/Program.cs#L22)
- This auto-maps PascalCase C# properties to snake_case database columns

**Authentication Flow**:
1. User initiates OAuth at `/auth/discord/login`
2. Discord redirects to callback with code
3. Cookie set with Discord user info (7-day expiration)
4. `WhitelistMiddleware` checks Discord ID against `whitelist.json`
5. Request proceeds if user is whitelisted

**Background Services**: `MapRefreshService` is an `IHostedService` that polls external APIs every 5 minutes to update Apex Legends map rotations

### Database Schema

Database uses snake_case naming convention:
- `discord_user` - User identities from Discord OAuth
- `clip`, `clip_collection`, `clip_tag`, `clip_view` - Video clip management
- `tag` - Normalized tag names (max 5 per clip)
- `user_frequent_link` - User bookmarks
- `apex_map_rotation` - Map schedule data

Primary keys are UUIDs generated with `gen_random_uuid()`. Timestamps use `timestamp with time zone` (UTC).

## Configuration Requirements

### Environment Variables (Docker Compose)

Required for production deployment:
- `POSTGRES_PASSWORD` - Database password
- `ApexLegendsApiKey` - API key for mozambiquehe.re
- `DiscordClientId` / `DiscordClientSecret` - Discord OAuth credentials
- `BunnyAccessKey` / `BunnyLibraryId` - Bunny CDN credentials
- `FrontendOrigin` - CORS-allowed frontend URL
- `BackendAddress` - Backend URL for asset URIs
- `FFmpegSharedPath` - Path for FFmpeg outputs (default: `/tmp/ffmpeg-downloads`)

### whitelist.json

Must exist in the application root directory. Contains an array of whitelisted Discord user IDs:
```json
["123456789012345678", "987654321098765432"]
```

Missing or empty whitelist will deny all authenticated requests.

## Important Implementation Details

### Naming Conventions

- **Database**: `snake_case` (enforced by EFCore.NamingConventions)
- **C# Code**: `PascalCase`
- **API Routes**: `kebab-case` (e.g., `/apex-legends/map-rotation`)
- **JSON API**: `snake_case_lower` (via `JsonNamingPolicy.SnakeCaseLower`)

### Service Registration

Services are registered in [Program.cs](Nucleus/Program.cs):
- Scoped services: `MapService`, `LinksService`, `ClipService`, `BunnyService`, `FFmpegService`, all `*Statements` classes
- Hosted service: `MapRefreshService`
- Singleton: `NpgsqlConnection` per request scope

### Endpoint Mapping

All endpoint groups are mapped in [Program.cs](Nucleus/Program.cs#L126-131):
```csharp
app.MapUserEndpoints();
app.MapApexEndpoints();
app.MapAuthEndpoints();
app.MapLinksEndpoints();
app.MapClipsEndpoints();
app.MapFFmpegEndpoints();
```

### CORS Configuration

CORS allows:
- `localhost` on any port
- `*.pluscosmic.dev` domains
- `*.pluscosmicdashboard.pages.dev` (Cloudflare previews)

Configured with `AllowCredentials()` for cookie-based auth.

### JSON Serialization

Configured with strict number handling to prevent security issues:
```csharp
JsonNumberHandling.Strict  // Rejects malformed numbers
```

### FFmpeg Integration

The FFmpeg service requires:
1. A running `linuxserver/ffmpeg` container
2. Shared volume mount between nucleus and ffmpeg containers
3. Coordinated path mapping via `FFmpegSharedPath` environment variable

Video downloads work by:
1. Executing `docker exec ffmpeg` commands from the nucleus container
2. FFmpeg downloads HLS playlists (`.m3u8`) and transcodes to MP4
3. Output files written to shared volume
4. Nucleus streams the file with `FileOptions.DeleteOnClose`

### Clip Management

Clips have these key features:
- **MD5 deduplication**: Optional MD5 hash prevents duplicate uploads
- **Tag limit**: Maximum 5 tags per clip (enforced in `ClipService`)
- **View tracking**: Per-user view status for each clip
- **Categories**: `ApexLegends`, `CallOfDutyWarzone`, `Snowboarding`

### Security Considerations

- **HttpOnly cookies**: Prevents JavaScript access to auth tokens
- **Whitelist middleware**: Runs after authentication, before endpoints
- **No EF Core**: Direct SQL reduces attack surface but requires careful parameterization
- **Strict JSON parsing**: `NumberHandling.Strict` prevents JSON-based attacks

## Testing

Test project: `Nucleus.Test/` using xUnit framework. Currently minimal test coverage.

Run all tests:
```bash
dotnet test
```

Run specific test:
```bash
dotnet test --filter "FullyQualifiedName~Namespace.Class.Method"
```

## Common Development Patterns

### Adding a New Endpoint

1. Create endpoint handler in `*Endpoints.cs`:
```csharp
public static class MyEndpoints
{
    public static void MapMyEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/my-feature").RequireAuthorization();
        group.MapGet("/", GetAll);
    }

    private static async Task<Results<Ok<List<MyDto>>, UnauthorizedHttpResult>> GetAll(
        MyService service,
        ClaimsPrincipal user)
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId is null) return TypedResults.Unauthorized();

        var results = await service.GetAllAsync(discordId);
        return TypedResults.Ok(results);
    }
}
```

2. Register in [Program.cs](Nucleus/Program.cs):
```csharp
app.MapMyEndpoints();
```

### Adding a Database Table

1. Create migration in `Nucleus/db/migrations/V{N}__{description}.sql`:
```sql
CREATE TABLE my_table (
    id UUID PRIMARY KEY DEFAULT (gen_random_uuid()),
    user_id UUID NOT NULL REFERENCES discord_user(id) ON DELETE CASCADE,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now()
);
```

2. Create model (PascalCase properties auto-map to snake_case columns):
```csharp
public record MyRow
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
```

3. Create `*Statements` class with Dapper queries:
```csharp
public class MyStatements(NpgsqlConnection connection)
{
    public async Task<List<MyRow>> GetByUserAsync(Guid userId)
    {
        const string sql = """
            SELECT id, user_id, created_at
            FROM my_table
            WHERE user_id = @UserId
            ORDER BY created_at DESC
            """;
        var results = await connection.QueryAsync<MyRow>(sql, new { UserId = userId });
        return results.ToList();
    }
}
```

4. Register in [Program.cs](Nucleus/Program.cs):
```csharp
builder.Services.AddScoped<MyStatements>();
```

### Extracting Discord User ID

All authenticated endpoints should extract the Discord user ID from claims:
```csharp
var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
if (discordId is null) return TypedResults.Unauthorized();
```

The `WhitelistMiddleware` ensures this claim exists and is whitelisted.

## Troubleshooting

**Migrations not running**: Ensure `ASPNETCORE_ENVIRONMENT` is set. Migrations only run when this environment variable exists.

**Whitelist not working**: Verify `whitelist.json` exists in the application root and contains valid Discord user IDs (numeric strings).

**FFmpeg errors**: Ensure the `ffmpeg` container is running and the shared volume is properly mounted. Check container logs: `docker compose logs ffmpeg`

**Database connection errors**: Verify PostgreSQL is healthy: `docker compose ps postgres`. Connection string is configured in compose.yaml.

**CORS errors**: Check that the frontend origin matches one of the allowed patterns in [Program.cs](Nucleus/Program.cs#L55-81). Localhost is allowed on any port during development.
- Use comments sparingly, only comment when the logic is sufficiently complex or non-obvious