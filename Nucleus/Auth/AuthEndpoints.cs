using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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

        var group = app.MapGroup("auth");

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
        var state = GenerateSecureRandomState();

        var props = new AuthenticationProperties
        {
            RedirectUri = "/auth/post-login-redirect" + (returnUrl != null ? $"?returnUrl={Uri.EscapeDataString(returnUrl)}" : "")
        };

        // Store state in AuthenticationProperties for validation
        // ASP.NET Core OAuth middleware will include this in the state parameter
        props.Items["state"] = state;

        return Results.Challenge(props, new[] { "Discord" });
    }

    public static async Task<IResult> PostLoginRedirect(HttpContext ctx, string? returnUrl,
        ClaimsPrincipal user, DiscordStatements discordStatements)
    {
        // Upsert user to database on each login to keep profile updated
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("No Discord ID");
        var username = user.FindFirstValue(ClaimTypes.Name) ?? throw new InvalidOperationException("No Discord username");
        var globalName = user.FindFirstValue("urn:discord:global_name");
        var avatar = user.FindFirstValue("urn:discord:avatar");

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
        return Results.Empty;
    }

    public static async Task Logout(HttpContext ctx)
    {
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    public static async Task<IResult> RefreshToken(HttpContext ctx, IConfiguration config, DiscordStatements discordStatements)
    {
        // Get the current authentication result
        var authenticateResult = await ctx.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!authenticateResult.Succeeded)
        {
            return Results.Unauthorized();
        }

        // Get the stored refresh token
        var refreshToken = await ctx.GetTokenAsync("refresh_token");
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Results.BadRequest(new { error = "No refresh token available" });
        }

        var discordClientId = config["DiscordClientId"];
        var discordClientSecret = config["DiscordClientSecret"];

        if (string.IsNullOrWhiteSpace(discordClientId) || string.IsNullOrWhiteSpace(discordClientSecret))
        {
            return Results.Problem("Discord OAuth not configured");
        }

        // Exchange refresh token for new access token
        using var httpClient = new HttpClient();
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = discordClientId,
            ["client_secret"] = discordClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        var tokenResponse = await httpClient.PostAsync("https://discord.com/api/oauth2/token", tokenRequest);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            return Results.Problem("Failed to refresh token");
        }

        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
        var newAccessToken = tokenData.GetProperty("access_token").GetString();
        var newRefreshToken = tokenData.GetProperty("refresh_token").GetString();
        var expiresIn = tokenData.GetProperty("expires_in").GetInt32();

        // Fetch updated user info from Discord
        using var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://discord.com/api/users/@me");
        userRequest.Headers.Add("Authorization", $"Bearer {newAccessToken}");
        var userResponse = await httpClient.SendAsync(userRequest);

        if (!userResponse.IsSuccessStatusCode)
        {
            return Results.Problem("Failed to fetch user info");
        }

        var userData = await userResponse.Content.ReadFromJsonAsync<JsonElement>();
        var discordId = userData.GetProperty("id").GetString()!;
        var username = userData.GetProperty("username").GetString()!;
        var globalName = userData.TryGetProperty("global_name", out var gn) && gn.ValueKind != JsonValueKind.Null ? gn.GetString() : null;
        var avatar = userData.TryGetProperty("avatar", out var av) && av.ValueKind != JsonValueKind.Null ? av.GetString() : null;

        // Update user in database
        await discordStatements.UpsertUser(discordId, username, globalName, avatar);

        // Create new claims identity
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, discordId),
            new(ClaimTypes.Name, username)
        };

        if (globalName != null)
            claims.Add(new Claim("urn:discord:global_name", globalName));
        if (avatar != null)
            claims.Add(new Claim("urn:discord:avatar", avatar));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        // Create new authentication properties with updated tokens
        var authProps = new AuthenticationProperties
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
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        // Only allow redirect to trusted domains
        if (uri.Host == "localhost" || uri.Host == "127.0.0.1")
            return true;

        if (uri.Host == "pluscosmic.dev" || uri.Host.EndsWith(".pluscosmic.dev"))
            return true;

        if (uri.Host.EndsWith("pluscosmicdashboard.pages.dev"))
            return true;

        return false;
    }

    private static string GenerateSecureRandomState()
    {
        // Generate a cryptographically secure random state value (32 bytes = 64 hex chars)
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(randomBytes).ToLowerInvariant();
    }
}