using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.HttpResults;
using Nucleus.Discord;

namespace Nucleus.Auth;

public static class AuthEndpoints
{
    private static string _frontendOrigin = "http://localhost:5173";

    public static void MapAuthEndpoints(this WebApplication app)
    {
        string? frontendOrigin = app.Configuration["FrontendOrigin"];
        if (frontendOrigin != null)
        {
            _frontendOrigin = frontendOrigin;
        }

        RouteGroupBuilder group = app.MapGroup("auth");

        group.MapGet("discord/login", Login).WithName("Login");
        group.MapGet("post-login-redirect", PostLoginRedirect).WithName("PostLoginRedirect");
        group.MapPost("logout", Logout).WithName("Logout");
        group.MapPost("refresh", RefreshToken).WithName("RefreshToken");
    }

    public static IResult Login(HttpContext ctx, string? returnUrl)
    {
        // Validate the return URL to prevent open redirect vulnerabilities
        if (!string.IsNullOrEmpty(returnUrl) && !IsValidReturnUrl(returnUrl))
        {
            returnUrl = null;
        }

        // Generate CSRF state parameter
        string state = GenerateSecureRandomState();

        AuthenticationProperties props = new()
        {
            RedirectUri = "/auth/post-login-redirect" +
                          (returnUrl != null ? $"?returnUrl={Uri.EscapeDataString(returnUrl)}" : "")
        };

        // Store state in AuthenticationProperties for validation
        // ASP.NET Core OAuth middleware will include this in the state parameter
        props.Items["state"] = state;

        return Results.Challenge(props, new[] { "Discord" });
    }

    public static async Task<Results<RedirectHttpResult, UnauthorizedHttpResult>> PostLoginRedirect(HttpContext ctx,
        string? returnUrl,
        ClaimsPrincipal user, DiscordStatements discordStatements)
    {
        // Upsert user to database on each login to keep profile updated
        string discordId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        string username = user.FindFirstValue(ClaimTypes.Name) ?? "";
        if (string.IsNullOrEmpty(discordId) || string.IsNullOrEmpty(username))
        {
            return TypedResults.Unauthorized();
        }

        string? globalName = user.FindFirstValue("urn:discord:global_name");
        string? avatar = user.FindFirstValue("urn:discord:avatar");

        await discordStatements.UpsertUser(discordId, username, globalName, avatar);
        // Validate return URL again for safety
        string redirect;
        if (!string.IsNullOrEmpty(returnUrl) && IsValidReturnUrl(returnUrl))
        {
            redirect = returnUrl;
        }
        else
        {
            redirect = _frontendOrigin;
        }

        ctx.Response.Redirect(redirect);
        return TypedResults.Redirect(redirect);
    }

    public static async Task Logout(HttpContext ctx)
    {
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public static async Task<IResult> RefreshToken(HttpContext ctx, IConfiguration config,
        DiscordStatements discordStatements)
    {
        // Get the current authentication result
        AuthenticateResult authenticateResult = await ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authenticateResult.Succeeded)
        {
            return Results.Unauthorized();
        }

        // Get the stored refresh token
        string? refreshToken = await ctx.GetTokenAsync("refresh_token");
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Results.BadRequest(new { error = "No refresh token available" });
        }

        string? discordClientId = config["DiscordClientId"];
        string? discordClientSecret = config["DiscordClientSecret"];

        if (string.IsNullOrWhiteSpace(discordClientId) || string.IsNullOrWhiteSpace(discordClientSecret))
        {
            return Results.Problem("Discord OAuth not configured");
        }

        // Exchange refresh token for new access token
        using HttpClient httpClient = new();
        FormUrlEncodedContent tokenRequest = new(new Dictionary<string, string>
        {
            ["client_id"] = discordClientId,
            ["client_secret"] = discordClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        HttpResponseMessage tokenResponse = await httpClient.PostAsync("https://discord.com/api/oauth2/token", tokenRequest);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            return Results.Problem("Failed to refresh token");
        }

        JsonElement tokenData = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        string? newAccessToken = tokenData.GetProperty("access_token").GetString();
        string? newRefreshToken = tokenData.GetProperty("refresh_token").GetString();
        int expiresIn = tokenData.GetProperty("expires_in").GetInt32();

        // Fetch updated user info from Discord
        using HttpRequestMessage userRequest = new(HttpMethod.Get, "https://discord.com/api/users/@me");
        userRequest.Headers.Add("Authorization", $"Bearer {newAccessToken}");
        HttpResponseMessage userResponse = await httpClient.SendAsync(userRequest);

        if (!userResponse.IsSuccessStatusCode)
        {
            return Results.Problem("Failed to fetch user info");
        }

        JsonElement userData = await userResponse.Content.ReadFromJsonAsync<JsonElement>();
        string discordId = userData.GetProperty("id").GetString()!;
        string username = userData.GetProperty("username").GetString()!;
        string? globalName = userData.TryGetProperty("global_name", out JsonElement gn) && gn.ValueKind != JsonValueKind.Null
            ? gn.GetString()
            : null;
        string? avatar = userData.TryGetProperty("avatar", out JsonElement av) && av.ValueKind != JsonValueKind.Null
            ? av.GetString()
            : null;

        // Update user in database
        await discordStatements.UpsertUser(discordId, username, globalName, avatar);

        // Create new claims identity
        List<Claim> claims = new()
        {
            new Claim(ClaimTypes.NameIdentifier, discordId),
            new Claim(ClaimTypes.Name, username)
        };

        if (globalName != null)
        {
            claims.Add(new Claim("urn:discord:global_name", globalName));
        }

        if (avatar != null)
        {
            claims.Add(new Claim("urn:discord:avatar", avatar));
        }

        ClaimsIdentity identity = new(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        ClaimsPrincipal principal = new(identity);

        // Create new authentication properties with updated tokens
        AuthenticationProperties authProps = new()
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn)
        };
        authProps.StoreTokens(new[]
        {
            new AuthenticationToken { Name = "access_token", Value = newAccessToken! },
            new AuthenticationToken { Name = "refresh_token", Value = newRefreshToken! }
        });

        // Sign in with new ticket
        await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProps);

        return Results.Ok(new { message = "Token refreshed successfully" });
    }

    private static bool IsValidReturnUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return false;
        }

        // Only allow redirect to trusted domains
        if (uri.Host == "localhost" || uri.Host == "127.0.0.1")
        {
            return true;
        }

        if (uri.Host == "pluscosmic.dev" || uri.Host.EndsWith(".pluscosmic.dev"))
        {
            return true;
        }

        if (uri.Host.EndsWith("pluscosmicdashboard.pages.dev"))
        {
            return true;
        }

        return false;
    }

    private static string GenerateSecureRandomState()
    {
        // Generate a cryptographically secure random state value (32 bytes = 64 hex chars)
        byte[] randomBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(randomBytes).ToLowerInvariant();
    }
}