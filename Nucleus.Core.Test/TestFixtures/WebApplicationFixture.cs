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
using Npgsql;
using Testcontainers.PostgreSql;

namespace Nucleus.Test.TestFixtures;

/// <summary>
///     Provides a test web application factory for integration testing the main Nucleus API endpoints.
///     Sets up test authentication and uses a test database.
/// </summary>
public class WebApplicationFixture : WebApplicationFactory<Nucleus.Program>, IAsyncLifetime
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

        // Set environment to Testing
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
        CreateTestWhitelist(
            "123456789012345678", // AuthHelper.DefaultTestDiscordId
            "987654321098765432" // AuthHelper.SecondaryTestDiscordId
        );
    }

    public new async Task DisposeAsync()
    {
        await _container.DisposeAsync();
        await base.DisposeAsync();
    }

    /// <summary>
    ///     Creates an authenticated HTTP client with a test Discord user.
    /// </summary>
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
        builder.UseEnvironment("Testing");

        // Override configuration
        builder.UseSetting("DatabaseConnectionString", _connectionString);
        builder.UseSetting("DiscordClientId", "test_discord_client_id_12345");
        builder.UseSetting("DiscordClientSecret", "test_discord_client_secret_67890");
        builder.UseSetting("FrontendOrigin", "http://localhost:5173");
        builder.UseSetting("BackendAddress", "http://localhost:5000");

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

            // Override authentication to use TestScheme
            services.Configure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = "TestScheme";
                options.DefaultAuthenticateScheme = "TestScheme";
                options.DefaultChallengeScheme = "TestScheme";
            });

            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("TestScheme", _ => { });
        });
    }

    /// <summary>
    ///     Creates a test whitelist.json file for authentication tests.
    /// </summary>
    public static void CreateTestWhitelist(params string[] discordIds)
    {
        var users = discordIds.Select(id => new { DiscordId = id, Role = "Admin" }).ToArray();
        var whitelistConfig = new { Users = users };
        string json = JsonSerializer.Serialize(whitelistConfig, new JsonSerializerOptions { WriteIndented = true });

        string whitelistPath = Path.Combine(AppContext.BaseDirectory, "whitelist.json");

        // Use file locking to prevent race conditions
        string lockPath = whitelistPath + ".lock";
        const int maxRetries = 10;
        const int retryDelayMs = 100;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using (FileStream lockStream = new(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    File.WriteAllText(whitelistPath, json);
                }
                return;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                Thread.Sleep(retryDelayMs);
            }
        }

        try
        {
            File.WriteAllText(whitelistPath, json);
        }
        catch (IOException)
        {
            if (File.Exists(whitelistPath) && new FileInfo(whitelistPath).Length > 0)
            {
                return;
            }
            throw;
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
        if (!Request.Headers.TryGetValue("X-Test-Auth", out StringValues authHeaderValue))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        try
        {
            string[] parts = authHeaderValue.ToString().Split(':', 3);
            if (parts.Length != 3)
            {
                return Task.FromResult(AuthenticateResult.Fail("Invalid test auth header"));
            }

            Claim[] claims =
            [
                new Claim(ClaimTypes.NameIdentifier, parts[0]),
                new Claim(ClaimTypes.Name, parts[1]),
                new Claim("global_name", parts[2])
            ];

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
