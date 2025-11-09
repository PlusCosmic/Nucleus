using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using EvolveDb;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using Npgsql;
using Nucleus.ApexLegends;
using Nucleus.Clips.Bunny;
using Testcontainers.PostgreSql;

namespace Nucleus.Test.TestFixtures;

/// <summary>
///     Provides a test web application factory for integration testing endpoints.
///     Sets up test authentication and uses a test database.
/// </summary>
public class WebApplicationFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container;
    private string? _connectionString;

    public WebApplicationFixture()
    {
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithDatabase("nucleus_test_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();
    }

    /// <summary>
    ///     Gets the database connection string.
    /// </summary>
    public string ConnectionString =>
        _connectionString ?? throw new InvalidOperationException("Container not started");

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();

        // Set environment to Testing, but Program.cs will skip migrations for "Testing" environment
        // We'll run migrations manually here instead
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        // Run migrations manually with the test container's connection string
        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync();

        Evolve evolve = new(connection, msg => Console.WriteLine($"[Evolve Test] {msg}"))
        {
            Locations = ["db/migrations"],
            IsEraseDisabled = true
        };

        evolve.Migrate();

        // Create whitelist ONCE for all tests to prevent race conditions
        // Include all test user IDs that any test might use
        CreateTestWhitelist(
            "123456789012345678", // AuthHelper.DefaultTestDiscordId
            "987654321098765432" // AuthHelper.SecondaryTestDiscordId
        );
    }

    public new async Task DisposeAsync()
    {
        // Don't clean up whitelist file here - multiple test class fixtures share the same file
        // and may dispose at different times when running in parallel. The file will be cleaned
        // up automatically when the test process exits.

        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    ///     Creates an authenticated HTTP client with a test Discord user.
    /// </summary>
    /// <param name="discordId">Discord user ID to use for authentication</param>
    /// <param name="username">Discord username</param>
    /// <param name="globalName">Discord global name</param>
    public HttpClient CreateAuthenticatedClient(
        string discordId = "123456789012345678",
        string username = "testuser",
        string globalName = "Test User")
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        }).WithTestAuthentication(discordId, username, globalName);
    }

    /// <summary>
    ///     Creates an unauthenticated HTTP client.
    /// </summary>
    public HttpClient CreateUnauthenticatedClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    /// <summary>
    ///     Gets a scoped service from the test application.
    /// </summary>
    public T GetService<T>() where T : notnull
    {
        IServiceScope scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment first
        builder.UseEnvironment("Testing");

        // Override configuration BEFORE services are built
        builder.UseSetting("DatabaseConnectionString", _connectionString);
        builder.UseSetting("DiscordClientId", "test_discord_client_id_12345");
        builder.UseSetting("DiscordClientSecret", "test_discord_client_secret_67890");
        builder.UseSetting("ApexLegendsApiKey", "test_apex_api_key");
        builder.UseSetting("FrontendOrigin", "http://localhost:5173");
        builder.UseSetting("BackendAddress", "http://localhost:5000");
        builder.UseSetting("BunnyAccessKey", "test_bunny_access_key");
        builder.UseSetting("BunnyLibraryId", "12345");
        builder.UseSetting("RedisConnectionString", "localhost:6379");

        builder.ConfigureTestServices(services =>
        {
            // Replace the database connection with test container
            ServiceDescriptor? descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(NpgsqlConnection));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            services.AddScoped(_ =>
            {
                NpgsqlConnection connection = new(_connectionString);
                connection.Open();
                return connection;
            });

            // Replace BunnyService with mock implementation
            ServiceDescriptor? bunnyServiceDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(BunnyService));

            if (bunnyServiceDescriptor != null)
            {
                services.Remove(bunnyServiceDescriptor);
            }

            services.AddScoped<BunnyService, MockBunnyService>();

            // Replace IApexMapCacheService with mock implementation
            ServiceDescriptor? apexCacheDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IApexMapCacheService));

            if (apexCacheDescriptor != null)
            {
                services.Remove(apexCacheDescriptor);
            }

            services.AddSingleton<IApexMapCacheService, MockApexMapCacheService>();

            // Override authentication to use TestScheme
            // Configure test authentication to be the default scheme
            services.Configure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = "TestScheme";
                options.DefaultAuthenticateScheme = "TestScheme";
                options.DefaultChallengeScheme = "TestScheme";
            });

            // Add test authentication scheme
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", _ => { });
        });
    }

    /// <summary>
    ///     Creates a test whitelist.json file for authentication tests.
    /// </summary>
    public static void CreateTestWhitelist(params string[] discordIds)
    {
        var whitelistConfig = new { WhitelistedDiscordUserIds = discordIds };
        string json = JsonSerializer.Serialize(whitelistConfig, new JsonSerializerOptions { WriteIndented = true });

        // Write to AppContext.BaseDirectory so WhitelistMiddleware can find it
        string whitelistPath = Path.Combine(AppContext.BaseDirectory, "whitelist.json");
        File.WriteAllText(whitelistPath, json);
    }

    /// <summary>
    ///     Cleans up test whitelist file.
    /// </summary>
    public static void CleanupTestWhitelist()
    {
        string whitelistPath = Path.Combine(AppContext.BaseDirectory, "whitelist.json");
        if (File.Exists(whitelistPath))
        {
            File.Delete(whitelistPath);
        }
    }
}

/// <summary>
///     Test authentication handler for integration tests.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check for test authentication header
        if (!Request.Headers.TryGetValue("X-Test-Auth", out StringValues authHeaderValue))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        try
        {
            // Parse the auth header: "discordId:username:globalName"
            string[] parts = authHeaderValue.ToString().Split(':', 3);
            if (parts.Length != 3)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid test auth header"));
            }

            Claim[] claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, parts[0]), // Discord ID
                new Claim(ClaimTypes.Name, parts[1]), // Username
                new Claim("global_name", parts[2]) // Global name
            };

            ClaimsIdentity identity = new(claims, "TestScheme");
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, "TestScheme");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
        catch (Exception ex)
        {
            return Task.FromResult(AuthenticateResult.Fail($"Test auth failed: {ex.Message}"));
        }
    }
}

/// <summary>
///     Extension methods for HttpClient to add test authentication.
/// </summary>
public static class HttpClientAuthExtensions
{
    /// <summary>
    ///     Adds test authentication headers to the HTTP client.
    /// </summary>
    public static HttpClient WithTestAuthentication(
        this HttpClient client,
        string discordId,
        string username,
        string globalName)
    {
        client.DefaultRequestHeaders.Add("X-Test-Auth", $"{discordId}:{username}:{globalName}");
        return client;
    }
}