using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Json;
using Nucleus.ApexLegends;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<MapService>();
builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

var frontendOrigin = builder.Configuration["FrontendOrigin"] ?? "http://localhost:5173";

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .WithOrigins(frontendOrigin)
            .AllowAnyMethod()
            .AllowCredentials()
            .AllowAnyHeader());
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "Discord"; // so Challenge() uses Discord by default
})
.AddCookie(options =>
{
    options.Cookie.Name = "pcdash.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.None; // if frontend and backend are on different sites, use None + HTTPS
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
})
.AddOAuth("Discord", options =>
{
    options.ClientId = builder.Configuration["Discord:ClientId"]!;
    options.ClientSecret = builder.Configuration["Discord:ClientSecret"]!;

    options.AuthorizationEndpoint = "https://discord.com/api/oauth2/authorize";
    options.TokenEndpoint = "https://discord.com/api/oauth2/token";
    options.UserInformationEndpoint = "https://discord.com/api/users/@me";

    options.CallbackPath = "/auth/discord/callback"; // Must match the Discord app redirect URI

    options.Scope.Clear();
    options.Scope.Add("identify");
    // options.Scope.Add("email"); // optional

    options.SaveTokens = false; // we use cookie session; no need to store provider tokens in auth properties

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Initiate login: redirects to Discord
app.MapGet("/auth/discord/login", (HttpContext http) =>
{
    var props = new AuthenticationProperties
    {
        RedirectUri = "/auth/post-login-redirect" // where to go after cookie is set
    };
    return Results.Challenge(props, new[] { "Discord" });
});

// Optional landing after successful auth cookie issued
app.MapGet("/auth/post-login-redirect", (HttpContext ctx, IConfiguration cfg) =>
{
    // Typically redirect to SPA root
    var redirect = cfg["Auth:PostLoginRedirect"] ?? "/";
    ctx.Response.Redirect(redirect);
    return Results.Empty;
});

// Logout: clears cookie
app.MapPost("/auth/logout", async (HttpContext http) =>
{
    await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Ok();
});

// Current user endpoint
app.MapGet("/me", [Authorize] (ClaimsPrincipal user) =>
{
    return Results.Ok(new
    {
        id = user.FindFirstValue(ClaimTypes.NameIdentifier),
        username = user.Identity?.Name,
        avatar = user.FindFirst("urn:discord:avatar")?.Value,
    });
});

app.MapGet("/apex-legends/map-rotation", (MapService mapService) => mapService.GetMapRotation())
    .WithName("GetApexMapRotation");

app.Run();