# Nucleus Authentication Implementation

## Overview

Nucleus implements a two-layer authentication and authorization system using Discord OAuth2 for identity verification and a whitelist-based system for access control. This document provides a comprehensive overview of the authentication flow, implementation details, and security considerations specific to this application.

## Architecture

### Authentication Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Authentication Flow                           │
└─────────────────────────────────────────────────────────────────────┘

1. User → Frontend: Clicks "Login"
                ↓
2. Frontend → Backend: GET /auth/discord/login
                ↓
3. Backend → Discord: Redirects to Discord OAuth2 authorization
                ↓
4. Discord → User: User authorizes application
                ↓
5. Discord → Backend: Redirects to /auth/discord/callback with auth code
                ↓
6. Backend → Discord: Exchanges code for access token
                ↓
7. Backend → Discord: Fetches user info from /users/@me
                ↓
8. Backend → User: Sets HttpOnly cookie (pcdash.auth)
                ↓
9. Backend → Frontend: Redirects to frontend with auth cookie
                ↓
10. Frontend → Backend: All subsequent requests include auth cookie
                ↓
11. Backend (Middleware): Validates cookie → Checks whitelist → Processes request
```

### Request Processing Pipeline

All authenticated requests pass through this middleware pipeline:

```
HTTP Request
    ↓
Reverse Proxy (Caddy) → Port 8080
    ↓
CORS Middleware
    ↓
Authentication Middleware (Cookie validation)
    ↓
Authorization Middleware
    ↓
WhitelistMiddleware (Discord ID verification)
    ↓
Endpoint Handler
    ↓
Response
```

## Implementation Components

### 1. OAuth Configuration ([CookieAuth.cs](../Nucleus/Auth/CookieAuth.cs))

**Cookie Settings:**
- **Name:** `pcdash.auth`
- **HttpOnly:** `true` (prevents JavaScript access)
- **Secure Policy:**
  - Development: `SameAsRequest` (allows HTTP)
  - Production: `Always` (requires HTTPS)
- **SameSite:**
  - Development: `Lax` (lenient cross-site)
  - Production: `None` (strict cross-origin)
- **Expiration:** 7 days with sliding expiration
- **Behavior:** Returns 401/403 for unauthorized access (no redirects)

**Discord OAuth Endpoints:**
- **Authorization:** `https://discord.com/api/oauth2/authorize`
- **Token Exchange:** `https://discord.com/api/oauth2/token`
- **User Info:** `https://discord.com/api/users/@me`
- **Callback Path:** `/auth/discord/callback`

**OAuth Scopes:**
- `identify` (required) - Retrieves user ID, username, discriminator, avatar

**Claims Mapping:**
| Discord Field | Claim Type | Description |
|---------------|------------|-------------|
| `id` | `ClaimTypes.NameIdentifier` | Discord user ID (snowflake) |
| `username` | `ClaimTypes.Name` | Discord username |
| `discriminator` | `urn:discord:discriminator` | Discord discriminator (legacy) |
| `avatar` | `urn:discord:avatar` | Avatar hash |

### 2. Authentication Endpoints ([AuthEndpoints.cs](../Nucleus/Auth/AuthEndpoints.cs))

#### `GET /auth/discord/login`
Initiates the Discord OAuth flow.

**Parameters:**
- `returnUrl` (optional): URL to redirect after successful login

**Security:**
- Validates return URL against trusted domains
- Prevents open redirect vulnerabilities

**Trusted Domains:**
- `localhost` / `127.0.0.1` (any port)
- `pluscosmic.dev` and `*.pluscosmic.dev`
- `*.pluscosmicdashboard.pages.dev` (Cloudflare Pages)

**Response:**
- 302 Redirect to Discord authorization page

---

#### `GET /auth/post-login-redirect`
Internal callback handler after Discord OAuth completes.

**Parameters:**
- `returnUrl` (optional): Final destination after login

**Process:**
1. Extracts Discord ID from `ClaimTypes.NameIdentifier`
2. Extracts username from `ClaimTypes.Name`
3. *(Currently commented out)* Upserts user into database
4. Validates return URL
5. Redirects to frontend or specified return URL

**Security:**
- Re-validates return URL to prevent manipulation
- Falls back to configured `FrontendOrigin` if invalid

---

#### `POST /auth/logout`
Signs the user out by clearing the authentication cookie.

**Response:**
- Clears `pcdash.auth` cookie
- 200 OK

### 3. Whitelist Middleware ([WhitelistMiddleware.cs](../Nucleus/Auth/WhitelistMiddleware.cs))

The whitelist middleware enforces access control by verifying authenticated users against a predefined list of Discord IDs.

**Initialization:**
- Loads `whitelist.json` from application base directory
- Parses JSON array of Discord user IDs (strings)
- Builds in-memory `HashSet<string>` for O(1) lookups
- Logs count of whitelisted users

**Whitelist JSON Format:**
```json
{
  "WhitelistedDiscordUserIds": [
    "201311542791241728",
    "620243863671537676",
    "271671707008368650"
  ]
}
```

**Request Processing Logic:**

1. **Skip whitelist check for public endpoints:**
   - `/health` - Health check endpoint
   - `/auth/*` - All authentication endpoints

2. **Check authentication:**
   - Returns `401 Unauthorized` if user is not authenticated
   - Error: `{"error": "Authentication required"}`

3. **Verify Discord ID claim exists:**
   - Extracts Discord ID from `ClaimTypes.NameIdentifier`
   - Returns `403 Forbidden` if claim is missing
   - Error: `{"error": "Invalid user claims"}`

4. **Verify user is whitelisted:**
   - Checks Discord ID against whitelist
   - Returns `403 Forbidden` if not whitelisted
   - Error: `{"error": "Access denied: User not whitelisted"}`
   - Logs warning with Discord ID

5. **Allow request:**
   - Passes control to next middleware/endpoint

**Error Handling:**
- Missing `whitelist.json`: Empty whitelist (denies all)
- Malformed JSON: Empty whitelist (denies all)
- Errors logged with details

### 4. Discord User Management

#### Database Schema ([V1__baseline_schema.sql](../Nucleus/db/migrations/V1__baseline_schema.sql#L10-L15))

```sql
CREATE TABLE discord_user (
    id uuid NOT NULL DEFAULT (gen_random_uuid()),
    discord_id text NOT NULL,
    username text NOT NULL,
    avatar text,
    CONSTRAINT discord_user_pkey PRIMARY KEY (id)
);

CREATE UNIQUE INDEX uq_discord_user_discord_id ON discord_user (discord_id);
```

**Key Points:**
- `id`: UUID primary key (auto-generated)
- `discord_id`: Discord snowflake ID (unique index)
- `username`: Discord username
- `avatar`: Avatar hash (optional)

#### Data Access ([DiscordStatements.cs](../Nucleus/Discord/DiscordStatements.cs))

**Available Operations:**
- `GetUserByDiscordId(string discordId)` - Find user by Discord ID
- `GetUserById(Guid id)` - Find user by internal UUID
- `InsertUser(...)` - Create new user record
- `UpdateUser(...)` - Update existing user
- `UpsertUser(...)` - Insert or update user atomically

**Upsert Logic:**
```sql
INSERT INTO discord_user (discord_id, username, avatar)
VALUES (@discordId, @username, @avatar)
ON CONFLICT (discord_id)
DO UPDATE SET username = EXCLUDED.username, avatar = EXCLUDED.avatar
RETURNING id, discord_id, username, avatar
```

#### User Endpoints ([DiscordUserEndpoints.cs](../Nucleus/Discord/DiscordUserEndpoints.cs))

**`GET /me`** (Requires Authorization)
Returns the authenticated user's profile.

**Response:**
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "username": "JohnDoe",
  "avatar": "https://cdn.discordapp.com/avatars/201311542791241728/abc123.png"
}
```

**Error Cases:**
- `401 Unauthorized`: No Discord ID claim
- `401 Unauthorized`: User not found in database

---

**`GET /user/{userId}`** (Requires Authorization)
Returns another user's profile by internal UUID.

**Parameters:**
- `userId`: Internal UUID (not Discord ID)

**Response:**
```json
{
  "id": "123e4567-e89b-12d3-a456-426614174000",
  "username": "JaneDoe",
  "avatar": "https://cdn.discordapp.com/avatars/620243863671537676/def456.png"
}
```

**Error Cases:**
- `404 Not Found`: User ID does not exist

## Security Considerations

### 1. Cookie Security

**HttpOnly Flag:**
- Prevents client-side JavaScript from accessing the cookie
- Mitigates XSS attacks stealing authentication tokens

**Secure Flag (Production):**
- Enforces HTTPS-only cookie transmission
- Prevents man-in-the-middle attacks

**SameSite Policy:**
- `Lax` (dev): Protects against CSRF while allowing top-level navigation
- `None` (prod): Required for cross-origin requests with credentials

### 2. CORS Configuration

**Allowed Origins:**
- `localhost` on any port (development)
- `*.pluscosmic.dev` (production domains)
- `*.pluscosmicdashboard.pages.dev` (Cloudflare previews)

**Credentials:**
- `AllowCredentials()` enabled for cookie-based auth
- Requires explicit origin matching (wildcards not allowed)

**Configuration:** [Program.cs:55-81](../Nucleus/Program.cs#L55-L81)

### 3. Open Redirect Prevention

Both login and post-login endpoints validate return URLs:

```csharp
private static bool IsValidReturnUrl(string url)
{
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        return false;

    // Only allow trusted domains
    if (uri.Host == "localhost" || uri.Host == "127.0.0.1")
        return true;

    if (uri.Host == "pluscosmic.dev" || uri.Host.EndsWith(".pluscosmic.dev"))
        return true;

    if (uri.Host.EndsWith("pluscosmicdashboard.pages.dev"))
        return true;

    return false;
}
```

### 4. Whitelist-Based Authorization

**Defense in Depth:**
- OAuth authentication verifies identity
- Whitelist authorization controls access
- Two-layer security model

**Fail-Safe Defaults:**
- Missing whitelist → deny all
- Malformed JSON → deny all
- Unknown user → deny access

**Audit Trail:**
- Logs all whitelist rejections with Discord ID
- Logs whitelist load count on startup

### 5. SQL Injection Prevention

**Parameterized Queries:**
All database queries use Dapper with parameterized inputs:

```csharp
const string sql = @"
    SELECT id, discord_id, username, avatar
    FROM discord_user
    WHERE discord_id = @discordId
    LIMIT 1";

return await connection.QuerySingleOrDefaultAsync<DiscordUserRow>(sql, new { discordId });
```

**No String Concatenation:**
- Never constructs SQL with string interpolation
- All user input passed as parameters

### 6. Strict JSON Parsing

**Number Handling:**
```csharp
JsonNumberHandling.Strict  // Rejects malformed numbers
```

Prevents JSON-based attacks exploiting number parsing vulnerabilities.

## Configuration

### Environment Variables

Required for Discord OAuth:

```bash
DiscordClientId=<your_client_id>
DiscordClientSecret=<your_client_secret>
FrontendOrigin=https://dashboard.pluscosmic.dev
```

### Discord Application Setup

1. **Create Application:** [Discord Developer Portal](https://discord.com/developers/applications)
2. **Enable OAuth2:** OAuth2 → General
3. **Add Redirect URI:** `https://your-domain.com/auth/discord/callback`
4. **Copy Credentials:** Client ID and Client Secret
5. **Set Scopes:** `identify` (minimum required)

### Whitelist Configuration

Create `whitelist.json` in application root:

```json
{
  "WhitelistedDiscordUserIds": [
    "201311542791241728",
    "620243863671537676"
  ]
}
```

**How to get Discord User IDs:**
1. Enable Developer Mode in Discord (User Settings → Advanced)
2. Right-click user → Copy ID

## Common Patterns

### Extracting Discord User ID in Endpoints

All protected endpoints should extract the Discord ID from claims:

```csharp
[Authorize]
public static async Task<Results<Ok<Data>, UnauthorizedHttpResult>> MyEndpoint(
    ClaimsPrincipal user,
    MyService service)
{
    var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (discordId is null)
        return TypedResults.Unauthorized();

    var data = await service.GetDataForUser(discordId);
    return TypedResults.Ok(data);
}
```

### Creating Endpoints that Require Authentication

Use `[Authorize]` attribute or `.RequireAuthorization()`:

```csharp
public static void MapMyEndpoints(this WebApplication app)
{
    var group = app.MapGroup("/my-feature").RequireAuthorization();
    group.MapGet("/", GetData);
    group.MapPost("/", PostData);
}
```

**Note:** The `WhitelistMiddleware` runs after authorization, so all `[Authorize]` endpoints are automatically whitelist-protected.

## Troubleshooting

### Issue: "Authentication required" error

**Cause:** Cookie not being sent or invalid

**Solutions:**
- Verify cookie is set after login
- Check CORS allows credentials
- Ensure frontend sends `credentials: 'include'`
- Verify cookie domain matches request domain

### Issue: "Access denied: User not whitelisted"

**Cause:** Discord ID not in whitelist

**Solutions:**
- Add Discord ID to `whitelist.json`
- Verify ID is a string, not a number
- Restart application to reload whitelist
- Check logs for loaded whitelist count

### Issue: OAuth callback fails

**Cause:** Redirect URI mismatch

**Solutions:**
- Verify redirect URI in Discord application matches exactly
- Include protocol (https://)
- Check for trailing slashes
- Ensure callback path is `/auth/discord/callback`

### Issue: Cookie not persisting

**Cause:** SameSite or Secure policy issues

**Solutions:**
- Development: Set `ASPNETCORE_ENVIRONMENT=Development`
- Production: Ensure HTTPS is used
- Check SameSite policy matches deployment architecture
- Verify browser allows third-party cookies (if cross-origin)

## Testing

### Manual Testing Flow

1. **Start application:**
   ```bash
   dotnet run --project Nucleus/Nucleus.csproj
   ```

2. **Navigate to login:**
   ```
   GET http://localhost:5000/auth/discord/login
   ```

3. **Authorize on Discord**

4. **Verify cookie is set:**
   - Check browser DevTools → Application → Cookies
   - Cookie name: `pcdash.auth`

5. **Test protected endpoint:**
   ```bash
   curl -X GET http://localhost:5000/me \
     --cookie "pcdash.auth=<cookie_value>"
   ```

6. **Test whitelist enforcement:**
   - Remove Discord ID from `whitelist.json`
   - Restart application
   - Attempt to access `/me` → Should return 403

### Unit Testing Considerations

**WhitelistMiddleware:**
- Test missing whitelist file
- Test malformed JSON
- Test empty whitelist
- Test whitelisted user access
- Test non-whitelisted user rejection
- Test public endpoint bypass

**AuthEndpoints:**
- Test return URL validation
- Test trusted vs untrusted domains
- Test logout clears cookie

## Future Improvements

### Potential Enhancements

1. **Database User Upsert:**
   - Currently commented out in `PostLoginRedirect`
   - Should upsert user on every login to keep profile updated
   - See [AuthEndpoints.cs:48-52](../Nucleus/Auth/AuthEndpoints.cs#L48-L52)

2. **Admin Interface:**
   - Web UI for managing whitelist
   - Real-time whitelist updates without restart

3. **Role-Based Access Control (RBAC):**
   - Add roles/permissions beyond whitelist
   - Discord role integration via `guilds` scope

4. **Session Management:**
   - View active sessions
   - Force logout from all devices
   - Session revocation

5. **Audit Logging:**
   - Track login/logout events
   - Log access attempts
   - Integration with monitoring systems

6. **OAuth Scope Expansion:**
   - Add `email` scope for user contact
   - Add `guilds` scope for server membership checks

7. **Token Refresh:**
   - Currently doesn't store refresh tokens (`SaveTokens = false`)
   - Could implement to keep Discord data fresh

## References

- **Discord OAuth2 Documentation:** https://discord.com/developers/docs/topics/oauth2
- **ASP.NET Core Authentication:** https://learn.microsoft.com/en-us/aspnet/core/security/authentication/
- **Cookie Authentication:** https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie

## Related Documentation

- [CLAUDE.md](../CLAUDE.md) - Project overview and architecture
- [Discord OAuth2 Guide](discord-oauth2-guide.md) - Generic Discord OAuth2 implementation guide
- [FFmpeg Container Guide](ffmpeg-container-guide.md) - FFmpeg integration details
