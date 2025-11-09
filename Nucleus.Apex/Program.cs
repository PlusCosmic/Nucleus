using Dapper;
using Npgsql;
using Nucleus.Apex;
using Nucleus.Apex.CharacterDetection;
using Nucleus.Data.ApexLegends;
using StackExchange.Redis;

// Configure Dapper to match snake_case DB columns to PascalCase C# properties
DefaultTypeMap.MatchNamesWithUnderscores = true;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string? connectionString = builder.Configuration.GetConnectionString("DatabaseConnectionString")
                           ?? builder.Configuration["DatabaseConnectionString"];

string? redisConnectionString = builder.Configuration.GetConnectionString("RedisConnectionString")
                                ?? builder.Configuration["RedisConnectionString"]
                                ?? "localhost:6379";

// Register services
builder.Services.AddHttpClient();
builder.Services.AddScoped(_ =>
    new NpgsqlConnection(connectionString ??
                         "Host=localhost;Database=nucleus_db;Username=nucleus_user;Password=dummy"));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddScoped<ApexStatements>();
builder.Services.AddScoped<IApexDetectionQueueService, ApexDetectionQueueService>();
builder.Services.AddHostedService<MapRefreshService>();
builder.Services.AddHostedService<ApexDetectionBackgroundService>();

WebApplication app = builder.Build();

app.UseHttpsRedirection();
app.MapApexDetectionEndpoints();
app.Run();