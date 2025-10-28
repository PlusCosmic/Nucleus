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
                options.DefaultChallengeScheme =
                    hasDiscordOAuth ? "Discord" : CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "pcdash.auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest // Allow HTTP in development
                    : CookieSecurePolicy.Always; // Require HTTPS in production
                options.Cookie.SameSite = builder.Environment.IsDevelopment()
                    ? SameSiteMode.Lax // More lenient for development
                    : SameSiteMode.None; // Strict for production cross-origin
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(7);

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
            });
        builder.Services.AddAuthentication().AddOAuth("Discord", options =>
        {
            options.ClientId = discordClientId!;
            options.ClientSecret = discordClientSecret!;

            options.AuthorizationEndpoint = "https://discord.com/api/oauth2/authorize";
            options.TokenEndpoint = "https://discord.com/api/oauth2/token";
            options.UserInformationEndpoint = "https://discord.com/api/users/@me";

            options.CallbackPath = "/auth/discord/callback"; // Must match the Discord app redirect URI

            options.Scope.Clear();
            options.Scope.Add("identify");
            // options.Scope.Add("email"); // optional

            options.SaveTokens =
                false; // we use cookie session; no need to store provider tokens in auth properties

            // Claim mappings
            options.ClaimActions.Clear();
            options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
            options.ClaimActions.MapJsonKey(ClaimTypes.Name, "username");
            options.ClaimActions.MapJsonKey("urn:discord:discriminator", "discriminator");
            options.ClaimActions.MapJsonKey("urn:discord:avatar", "avatar");

            options.Events = new OAuthEvents
            {
                OnCreatingTicket = async ctx =>
                {
                    // Fetch user info from Discord and map claims
                    using var request = new HttpRequestMessage(HttpMethod.Get, ctx.Options.UserInformationEndpoint);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);
                    using var response = await ctx.Backchannel.SendAsync(request);
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadFromJsonAsync<JsonElement>();

                    ctx.RunClaimActions(json);

                    // OPTIONAL: Upsert the user in your database here using the Discord ID
                    // var discordId = json.GetProperty("id").GetString();
                    // await users.UpsertAsync(...);
                },
                OnRemoteFailure = ctx =>
                {
                    ctx.Response.Redirect("/auth/login-failed");
                    ctx.HandleResponse();
                    return Task.CompletedTask;
                }
            };
        });

        builder.Services.AddAuthorization();
    }
}