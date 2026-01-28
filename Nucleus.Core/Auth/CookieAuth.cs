using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace Nucleus.Auth;

public static class CookieAuth
{
    public static void ConfigureDiscordAuth(this WebApplicationBuilder builder)
    {
        var discordClientId = builder.Configuration["DiscordClientId"];
        var discordClientSecret = builder.Configuration["DiscordClientSecret"];
        var hasDiscordOAuth = !string.IsNullOrWhiteSpace(discordClientId) &&
                              !string.IsNullOrWhiteSpace(discordClientSecret);

        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "pcdash.auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.Cookie.MaxAge = TimeSpan.FromDays(7);

                // Prevent automatic redirects for API calls - return 401 instead
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            })
            .AddOAuth("Discord", options =>
        {
            options.ClientId = discordClientId!;
            options.ClientSecret = discordClientSecret!;

            // Explicitly set the sign-in scheme to ensure OAuth uses Cookie authentication
            options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;

            options.AuthorizationEndpoint = "https://discord.com/api/oauth2/authorize";
            options.TokenEndpoint = "https://discord.com/api/oauth2/token";
            options.UserInformationEndpoint = "https://discord.com/api/users/@me";

            options.CallbackPath = "/auth/discord/callback"; // Must match the Discord app redirect URI

            options.Scope.Clear();
            options.Scope.Add("identify");
            // options.Scope.Add("email"); // optional

            options.SaveTokens = true; // Store tokens to enable refresh

            // Claim mappings
            options.ClaimActions.Clear();
            options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
            options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
            options.ClaimActions.MapJsonKey("urn:discord:global_name", "global_name");
            options.ClaimActions.MapJsonKey("urn:discord:discriminator", "discriminator");
            options.ClaimActions.MapJsonKey("urn:discord:avatar", "avatar");

            options.Events = new OAuthEvents
            {
                OnCreatingTicket = async ctx =>
                {
                    // NOTE: State parameter validation has already occurred by this point
                    // The ASP.NET Core OAuth middleware automatically validates the state parameter
                    // against the correlation cookie for CSRF protection

                    // Fetch user info from Discord and map claims
                    using var request = new HttpRequestMessage(HttpMethod.Get, ctx.Options.UserInformationEndpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);
                    using var response = await ctx.Backchannel.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                    ctx.RunClaimActions(json);

                    // User upsert is handled in PostLoginRedirect endpoint
                },
                OnRemoteFailure = ctx =>
                {
                    // Handle OAuth failures including state validation errors
                    var loggerFactory = ctx.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                    var logger = loggerFactory.CreateLogger("Nucleus.Auth.DiscordOAuth");

                    if (ctx.Failure?.Message?.Contains("correlation") == true ||
                        ctx.Failure?.Message?.Contains("state") == true)
                    {
                        logger.LogWarning("OAuth state validation failed - possible CSRF attack: {Error}", ctx.Failure.Message);
                    }
                    else
                    {
                        logger.LogError("OAuth authentication failed: {Error}", ctx.Failure?.Message ?? "Unknown error");
                    }

                    ctx.Response.Redirect("/auth/login-failed");
                    ctx.HandleResponse();
                    return Task.CompletedTask;
                }
            };
        });

        builder.Services.AddAuthorization();
    }
}