using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using EvolveDb;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Nucleus.ApexLegends;
using Nucleus.Auth;
using Nucleus.Clips;
using Nucleus.Clips.Bunny;
using Nucleus.Clips.FFmpeg;
using Nucleus.Data.ApexLegends;
using Nucleus.Data.Clips;
using Nucleus.Data.Discord;
using Nucleus.Data.Links;
using Nucleus.Discord;
using Nucleus.Links;

DefaultTypeMap.MatchNamesWithUnderscores = true;
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string? connectionString = builder.Configuration.GetConnectionString("DatabaseConnectionString")
                           ?? builder.Configuration["DatabaseConnectionString"];

// Run migrations automatically, but skip in Testing environment (tests handle migrations)
string? environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
if (environment != null && environment != "Testing")
{
    using NpgsqlConnection connection =
        new(connectionString ??
            "Host=localhost;Database=nucleus_db;Username=nucleus_user;Password=dummy");
    Evolve evolve = new(connection, Console.WriteLine)
    {
        Locations = ["db/migrations"],
        IsEraseDisabled = true
    };

    evolve.Migrate();
}

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.AddScoped<MapService>();
builder.Services.AddScoped<LinksService>();
builder.Services.AddScoped<ClipService>();
builder.Services.AddScoped<BunnyService>();
builder.Services.AddScoped<FFmpegService>();
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri))
                {
                    return false;
                }

                // Allow localhost on any port
                if (uri.Host == "localhost" || uri.Host == "127.0.0.1")
                {
                    return true;
                }

                // Allow *.pluscosmic.dev
                if (uri.Host == "pluscosmic.dev" || uri.Host.EndsWith(".pluscosmic.dev"))
                {
                    return true;
                }

                // Allow previews from cloudflare
                if (uri.Host.EndsWith("pluscosmicdashboard.pages.dev"))
                {
                    return true;
                }

                return false;
            })
            .AllowAnyMethod()
            .AllowCredentials()
            .AllowAnyHeader());
});

builder.ConfigureDiscordAuth();

// Register Dapper Statements classes
builder.Services.AddScoped(sp =>
    new NpgsqlConnection(connectionString ??
                         "Host=localhost;Database=nucleus_db;Username=nucleus_user;Password=dummy"));

builder.Services.AddScoped<ClipsStatements>();
builder.Services.AddScoped<ApexStatements>();
builder.Services.AddScoped<LinksStatements>();
builder.Services.AddScoped<DiscordStatements>();

IHealthChecksBuilder healthChecksBuilder = builder.Services.AddHealthChecks();

if (!string.IsNullOrEmpty(connectionString))
{
    healthChecksBuilder.AddNpgSql(
        connectionString,
        name: "database",
        timeout: TimeSpan.FromSeconds(3),
        tags: new[] { "ready" });
}

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
FileExtensionContentTypeProvider provider = new();
// Add new mappings
provider.Mappings[".avif"] = "image/avif";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<WhitelistMiddleware>();
app.MapUserEndpoints();
app.MapApexEndpoints();
app.MapAuthEndpoints();
app.MapLinksEndpoints();
app.MapClipsEndpoints();
app.MapFFmpegEndpoints();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    AllowCachingResponses = false,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.Run();