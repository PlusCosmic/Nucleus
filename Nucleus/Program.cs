using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using EvolveDb;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Nucleus.Apex;
using Nucleus.ApexLegends;
using Nucleus.ApexLegends.LegendDetection;
using Nucleus.Auth;
using Nucleus.Clips;
using Nucleus.Clips.Bunny;
using Nucleus.Clips.FFmpeg;
using Nucleus.Discord;
using Nucleus.Exceptions;
using Nucleus.Links;
using StackExchange.Redis;

DefaultTypeMap.MatchNamesWithUnderscores = true;
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string? connectionString = builder.Configuration.GetConnectionString("DatabaseConnectionString")
                           ?? builder.Configuration["DatabaseConnectionString"];

string? redisConnectionString = builder.Configuration.GetConnectionString("RedisConnectionString")
                                ?? builder.Configuration["RedisConnectionString"]
                                ?? "localhost:6379";

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
builder.Services.AddScoped<ClipsStatements>();
builder.Services.AddScoped<ClipsBackfillStatements>();
builder.Services.AddScoped<ApexStatements>();
builder.Services.AddScoped<LinksStatements>();
builder.Services.AddScoped<DiscordStatements>();
builder.Services.AddScoped<MapService>();
builder.Services.AddScoped<LinksService>();
builder.Services.AddScoped<ClipService>();
builder.Services.AddScoped<ClipsBackfillService>();
builder.Services.AddScoped<BunnyService>();
builder.Services.AddScoped<FFmpegService>();
builder.Services.AddScoped<IApexMapCacheService, ApexMapCacheService>();
builder.Services.AddScoped<IApexDetectionQueueService, ApexDetectionQueueService>();
builder.Services.AddHostedService<MapRefreshService>();
builder.Services.AddHostedService<ApexDetectionBackgroundService>();
builder.Services.AddHostedService<ClipStatusRefreshService>();

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect($"{redisConnectionString},abortConnect=false"));

// Add global exception handling
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
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
app.UseExceptionHandler(); // Global exception handling

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
app.MapBunnyWebhookEndpoints();
app.MapApexDetectionEndpoints();
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

public partial class Program
{
}