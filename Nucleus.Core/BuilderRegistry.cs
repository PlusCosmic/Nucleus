using System.Text.Json;
using System.Text.Json.Serialization;
using Discord;
using Discord.WebSocket;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.OpenApi;
using MongoDB.Driver;
using Npgsql;
using Nucleus.Discord;
using Nucleus.Exceptions;
using Nucleus.Links;
using Nucleus.Auth;
using StackExchange.Redis;

namespace Nucleus;

public static class BuilderRegistry
{
    public static void RegisterServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddOpenApi(options =>
        {
            // Fix for OpenAPI 3.1 nullable type arrays that break typescript-fetch generator.
            // When a schema has type: ["null", "object"], the generator incorrectly creates
            // references to a non-existent "Null" type. This transformer removes the Null flag
            // from schema definitions so they generate as pure object types.
            options.AddSchemaTransformer((schema, context, cancellationToken) =>
            {
                if (schema.Type.HasValue &&
                    schema.Type.Value.HasFlag(JsonSchemaType.Null) &&
                    schema.Type.Value.HasFlag(JsonSchemaType.Object))
                {
                    schema.Type = JsonSchemaType.Object;
                }

                return Task.CompletedTask;
            });
        });
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<LinksStatements>();
        builder.Services.AddScoped<DiscordStatements>();
        builder.Services.AddScoped<LinksService>();
        builder.Services.AddScoped<DiscordBotService>();
        builder.Services.AddSingleton<WhitelistService>();
        builder.Services.AddSingleton<Nucleus.Shared.Discord.DiscordRoleMapping>();
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

        NpgsqlDataSourceBuilder dataSourceBuilder = new(connectionString ??
            "Host=localhost;Database=nucleus_db;Username=nucleus_user;Password=dummy");
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