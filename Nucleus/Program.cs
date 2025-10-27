using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Nucleus.ApexLegends;
using Nucleus.Discord;
using Nucleus.Links;
using Nucleus.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<MapService>();
builder.Services.AddScoped<LinksService>();
builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

var frontendOrigin = builder.Configuration["FrontendOrigin"] ?? "http://localhost:5173";

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return false;
                
                // Allow localhost on any port
                if (uri.Host == "localhost" || uri.Host == "127.0.0.1")
                    return true;
                
                // Allow *.pluscosmic.dev
                if (uri.Host == "pluscosmic.dev" || uri.Host.EndsWith(".pluscosmic.dev"))
                    return true;
                
                // Allow previews from cloudflare
                if (uri.Host.EndsWith("pluscosmicdashboard.pages.dev"))
                    return true;
                
                return false;
            })
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
    options.ClientId = builder.Configuration["DiscordClientId"]!;
    options.ClientSecret = builder.Configuration["DiscordClientSecret"]!;

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

builder.Services.AddDbContextPool<NucleusDbContext>(opt => 
    opt.UseNpgsql(builder.Configuration["DatabaseConnectionString"] ?? throw new InvalidOperationException("DatabaseConnectionString not configured")));

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
app.MapGet("/auth/discord/login", (HttpContext http, string? returnUrl) =>
{
    // Validate the return URL to prevent open redirect vulnerabilities
    if (!string.IsNullOrEmpty(returnUrl) && !IsValidReturnUrl(returnUrl))
    {
        returnUrl = null;
    }
    
    var props = new AuthenticationProperties
    {
        RedirectUri = "/auth/post-login-redirect" + (returnUrl != null ? $"?returnUrl={Uri.EscapeDataString(returnUrl)}" : "")
    };
    return Results.Challenge(props, new[] { "Discord" });
});

// Optional landing after successful auth cookie issued
app.MapGet("/auth/post-login-redirect", async (HttpContext ctx, string? returnUrl, NucleusDbContext dbContext, ClaimsPrincipal user) =>
{
    // Check if user is already in database
    var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("No Discord ID");
    var username = user.FindFirstValue(ClaimTypes.Name) ?? throw new InvalidOperationException("No Discord username");
    if (!dbContext.DiscordUsers.Any(u => u.DiscordId == discordId))
    {
        await dbContext.DiscordUsers.AddAsync(new DiscordUser { DiscordId = discordId, Username = username });
    }
    // Validate return URL again for safety
    string redirect;
    if (!string.IsNullOrEmpty(returnUrl) && IsValidReturnUrl(returnUrl))
    {
        redirect = returnUrl;
    }
    else
    {
        redirect = frontendOrigin;
    }
    
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
app.MapGet("/me", [Authorize] (ClaimsPrincipal user) => new User(user.FindFirstValue(ClaimTypes.NameIdentifier), user.Identity?.Name,
    user.FindFirst("urn:discord:avatar")?.Value));

app.MapGet("/apex-legends/map-rotation", (MapService mapService) => mapService.GetMapRotation())
    .WithName("GetApexMapRotation");

app.MapGet("/links", [Authorize] async (LinksService linksService, ClaimsPrincipal user) =>
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return Results.Unauthorized();
    
        var links = await linksService.GetLinksForUser(discordId);
        return Results.Ok(links);
    })
    .WithName("GetLinksForUser");

app.MapDelete("/links/{id:guid}", [Authorize] async (LinksService linksService, Guid id, ClaimsPrincipal user) =>
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return Results.Unauthorized();
    
        var result = await linksService.DeleteLink(id, discordId);
        return result ? Results.NoContent() : Results.NotFound();
    })
    .WithName("DeleteLink");

app.MapPost("/links", [Authorize] async (LinksService linksService, Link link, ClaimsPrincipal user) =>
    {
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(discordId))
            return Results.Unauthorized();
    
        await linksService.AddLink(discordId, link);
        return Results.Created();
    })
    .WithName("AddLink");

app.Run();

bool IsValidReturnUrl(string url)
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