using System.Text.Json;
using System.Text.Json.Serialization;
using Discord;
using Discord.WebSocket;
using Microsoft.AspNetCore.Http.Json;
using MongoDB.Driver;
using Npgsql;
using Nucleus.ApexLegends;
using Nucleus.ApexLegends.LegendDetection;
using Nucleus.Clips;
using Nucleus.Clips.Bunny;
using Nucleus.Clips.FFmpeg;
using Nucleus.Discord;
using Nucleus.Dropzone;
using Nucleus.Exceptions;
using Nucleus.Links;
using Nucleus.Auth;
using Nucleus.Minecraft;
using Nucleus.Minecraft.Models;
using StackExchange.Redis;

namespace Nucleus;

public static class BuilderRegistry
{
    public static void RegisterServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi();
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<ClipsStatements>();
        builder.Services.AddScoped<ClipsBackfillStatements>();
        builder.Services.AddScoped<ApexStatements>();
        builder.Services.AddScoped<LinksStatements>();
        builder.Services.AddScoped<DiscordStatements>();
        builder.Services.AddScoped<PlaylistStatements>();
        builder.Services.AddScoped<MinecraftStatements>();
        builder.Services.AddScoped<MapService>();
        builder.Services.AddScoped<LinksService>();
        builder.Services.AddScoped<ClipService>();
        builder.Services.AddScoped<ClipsBackfillService>();
        builder.Services.AddScoped<BunnyService>();
        builder.Services.AddScoped<FFmpegService>();
        builder.Services.AddScoped<PlaylistService>();
        builder.Services.AddScoped<DiscordBotService>();
        builder.Services.AddSingleton<WhitelistService>();
        builder.Services.AddSingleton<DiscordRoleMapping>();
        builder.Services.AddSingleton<RconService>();
        builder.Services.AddSingleton<DockerContainerService>();
        builder.Services.AddScoped<MinecraftStatusService>();
        builder.Services.AddScoped<FileService>();
        builder.Services.AddSingleton<LogTailerService>();
        builder.Services.AddScoped<ConsoleWebSocketHandler>();
        builder.Services.AddScoped<BackupService>();
        builder.Services.AddHostedService<BackupSyncBackgroundService>();
        builder.Services.AddScoped<IApexMapCacheService, ApexMapCacheService>();
        builder.Services.AddScoped<IApexDetectionQueueService, ApexDetectionQueueService>();
        builder.Services.AddHostedService<MapRefreshService>();
        builder.Services.AddHostedService<ApexDetectionBackgroundService>();
        builder.Services.AddHostedService<ClipStatusRefreshService>();
        builder.Services.AddHostedService<DiscordBotHostedService>();
        builder.Services.AddHostedService<GuildMemberSyncService>();
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

                        if (uri.Host == "localhost" || uri.Host == "127.0.0.1")
                        {
                            return true;
                        }

                        return uri.Host == "pluscosmic.dev" || uri.Host.EndsWith(".pluscosmic.dev");
                    })
                    .AllowAnyMethod()
                    .AllowCredentials()
                    .AllowAnyHeader());
        });

        DiscordSocketConfig discordConfig = new DiscordSocketConfig()
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers
        };

        builder.Services.AddSingleton(discordConfig);
        builder.Services.AddSingleton(provider => new DiscordSocketClient(provider.GetRequiredService<DiscordSocketConfig>()));
    }

    public static void RegisterDatabases(this WebApplicationBuilder builder)
    {
        string? connectionString = builder.Configuration.GetConnectionString("DatabaseConnectionString")
                                   ?? builder.Configuration["DatabaseConnectionString"];

        string? redisConnectionString = builder.Configuration.GetConnectionString("RedisConnectionString")
                                        ?? builder.Configuration["RedisConnectionString"]
                                        ?? "localhost:6379";

        string? mongoConnectionString = builder.Configuration.GetConnectionString("MongoConnectionString")
                                        ?? builder.Configuration["MongoConnectionString"];

        builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect($"{redisConnectionString},abortConnect=false"));

        builder.Services.AddSingleton<MongoClient>(_ => new MongoClient(mongoConnectionString));
        builder.Services.AddSingleton<IMongoDatabase>(provider => provider.GetRequiredService<MongoClient>().GetDatabase("dropzone"));
        builder.Services.AddSingleton<IMongoCollection<ShareGroup>>(provider => provider.GetRequiredService<IMongoDatabase>().GetCollection<ShareGroup>("share-groups"));

        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionString ??
            "Host=localhost;Database=nucleus_db;Username=nucleus_user;Password=dummy");
        dataSourceBuilder.MapEnum<MinecraftServerType>("minecraft_server_type");
        NpgsqlDataSource dataSource = dataSourceBuilder.Build();

        builder.Services.AddSingleton(dataSource);
        builder.Services.AddScoped(_ => dataSource.CreateConnection());

        IHealthChecksBuilder healthChecksBuilder = builder.Services.AddHealthChecks();

        if (!string.IsNullOrEmpty(connectionString))
        {
            healthChecksBuilder.AddNpgSql(
                connectionString,
                name: "database",
                timeout: TimeSpan.FromSeconds(3),
                tags: ["ready"]);
        }
    }
}