using System.Text.Json;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Nucleus.Test.TestFixtures;

/// <summary>
/// Provides WireMock server instances for mocking external APIs.
/// Supports Discord OAuth and Apex Legends API mocking.
/// </summary>
public class ExternalApiFixture : IDisposable
{
    private WireMockServer? _server;

    /// <summary>
    /// Gets the WireMock server instance. Call StartServer first.
    /// </summary>
    public WireMockServer Server =>
        _server ?? throw new InvalidOperationException("Server not started. Call StartServer first.");

    /// <summary>
    /// Gets the base URL of the mock server.
    /// </summary>
    public string BaseUrl => Server.Urls[0];

    /// <summary>
    /// Starts the WireMock server on a random available port.
    /// </summary>
    public void StartServer()
    {
        _server = WireMockServer.Start();
    }

    /// <summary>
    /// Stops and resets the WireMock server.
    /// </summary>
    public void StopServer()
    {
        _server?.Stop();
        _server?.Dispose();
        _server = null;
    }

    /// <summary>
    /// Resets all configured mocks on the server.
    /// </summary>
    public void Reset()
    {
        Server.Reset();
    }

    /// <summary>
    /// Sets up a mock for Discord OAuth token exchange.
    /// </summary>
    /// <param name="code">The authorization code to accept</param>
    /// <param name="accessToken">The access token to return</param>
    /// <param name="refreshToken">The refresh token to return</param>
    public void MockDiscordTokenExchange(
        string code = "test_auth_code",
        string accessToken = "test_access_token",
        string refreshToken = "test_refresh_token")
    {
        var response = new
        {
            access_token = accessToken,
            refresh_token = refreshToken,
            token_type = "Bearer",
            expires_in = 604800,
            scope = "identify"
        };

        Server
            .Given(Request.Create()
                .WithPath("/api/oauth2/token")
                .UsingPost()
                .WithBody($"*code={code}*"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(response)));
    }

    /// <summary>
    /// Sets up a mock for Discord user profile fetch.
    /// </summary>
    /// <param name="discordId">Discord user ID</param>
    /// <param name="username">Discord username</param>
    /// <param name="globalName">Discord global name</param>
    /// <param name="avatar">Avatar hash (optional)</param>
    public void MockDiscordUserProfile(
        string discordId = "123456789012345678",
        string username = "testuser",
        string globalName = "Test User",
        string? avatar = null)
    {
        var response = new
        {
            id = discordId,
            username = username,
            global_name = globalName,
            avatar = avatar,
            discriminator = "0",
            public_flags = 0
        };

        Server
            .Given(Request.Create()
                .WithPath("/api/users/@me")
                .UsingGet()
                .WithHeader("Authorization", "Bearer *"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(response)));
    }

    /// <summary>
    /// Sets up a mock for Discord OAuth failure.
    /// </summary>
    /// <param name="errorCode">Error code to return</param>
    /// <param name="errorMessage">Error message to return</param>
    public void MockDiscordOAuthFailure(
        string errorCode = "invalid_grant",
        string errorMessage = "Invalid authorization code")
    {
        var response = new
        {
            error = errorCode,
            error_description = errorMessage
        };

        Server
            .Given(Request.Create()
                .WithPath("/api/oauth2/token")
                .UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(400)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(response)));
    }

    /// <summary>
    /// Sets up a mock for Apex Legends map rotation API.
    /// </summary>
    /// <param name="currentMap">Current map name</param>
    /// <param name="nextMap">Next map name</param>
    /// <param name="currentEnd">Current map end timestamp (Unix seconds)</param>
    public void MockApexMapRotation(
        string currentMap = "Kings Canyon",
        string nextMap = "Worlds Edge",
        long? currentEnd = null)
    {
        var endTime = currentEnd ?? DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();

        var response = new
        {
            battle_royale = new
            {
                current = new
                {
                    map = currentMap,
                    remainingSecs = (int)(endTime - DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    remainingMins = (int)((endTime - DateTimeOffset.UtcNow.ToUnixTimeSeconds()) / 60),
                    remainingTimer = "00:45:23",
                    end = endTime
                },
                next = new
                {
                    map = nextMap,
                    start = endTime,
                    end = endTime + 3600
                }
            },
            arenas = new
            {
                current = new { map = "Party Crasher" },
                next = new { map = "Phase Runner" }
            },
            ranked = new
            {
                current = new { map = currentMap },
                next = new { map = nextMap }
            }
        };

        Server
            .Given(Request.Create()
                .WithPath("/maprotation")
                .UsingGet()
                .WithParam("auth", "*"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(response)));
    }

    /// <summary>
    /// Sets up a mock for Apex Legends API failure.
    /// </summary>
    /// <param name="statusCode">HTTP status code to return</param>
    public void MockApexApiFailure(int statusCode = 500)
    {
        Server
            .Given(Request.Create()
                .WithPath("/maprotation")
                .UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(statusCode)
                .WithBody("Internal Server Error"));
    }

    /// <summary>
    /// Sets up a mock for Bunny CDN video creation.
    /// </summary>
    /// <param name="videoId">Video ID to return</param>
    /// <param name="libraryId">Library ID</param>
    public void MockBunnyVideoCreation(
        string videoId = "test-video-guid",
        int libraryId = 12345)
    {
        var response = new
        {
            guid = videoId,
            videoLibraryId = libraryId,
            title = "Test Video",
            dateUploaded = DateTimeOffset.UtcNow.ToString("o"),
            views = 0,
            isPublic = false,
            length = 0,
            status = 2, // Processing
            thumbnailFileName = "",
            encodeProgress = 0
        };

        Server
            .Given(Request.Create()
                .WithPath($"/library/{libraryId}/videos")
                .UsingPost()
                .WithHeader("AccessKey", "*"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(response)));
    }

    public void Dispose()
    {
        StopServer();
    }
}
