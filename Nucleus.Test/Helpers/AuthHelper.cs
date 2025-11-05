using System.Security.Claims;
using System.Text.Json;
using Nucleus.Discord;

namespace Nucleus.Test.Helpers;

/// <summary>
/// Helper class for creating test authentication scenarios.
/// </summary>
public static class AuthHelper
{
    /// <summary>
    /// Creates a ClaimsPrincipal for a test Discord user.
    /// </summary>
    /// <param name="discordId">Discord user ID</param>
    /// <param name="username">Discord username</param>
    /// <param name="globalName">Discord global name</param>
    public static ClaimsPrincipal CreateTestUser(
        string discordId = "123456789012345678",
        string username = "testuser",
        string globalName = "Test User")
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, discordId),
            new Claim(ClaimTypes.Name, username),
            new Claim("global_name", globalName)
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Creates an unauthenticated ClaimsPrincipal.
    /// </summary>
    public static ClaimsPrincipal CreateUnauthenticatedUser()
    {
        return new ClaimsPrincipal(new ClaimsIdentity());
    }

    /// <summary>
    /// Creates a test whitelist.json file with specified Discord IDs.
    /// </summary>
    /// <param name="discordIds">Discord user IDs to whitelist</param>
    /// <param name="filePath">Path to whitelist file (default: whitelist.json in AppContext.BaseDirectory)</param>
    public static void CreateTestWhitelist(string[] discordIds, string? filePath = null)
    {
        filePath ??= Path.Combine(AppContext.BaseDirectory, "whitelist.json");
        var whitelistConfig = new { WhitelistedDiscordUserIds = discordIds };
        var json = JsonSerializer.Serialize(whitelistConfig, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Deletes the test whitelist file.
    /// </summary>
    /// <param name="filePath">Path to whitelist file</param>
    public static void DeleteTestWhitelist(string? filePath = null)
    {
        filePath ??= Path.Combine(AppContext.BaseDirectory, "whitelist.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// Creates a test Discord user for database seeding.
    /// </summary>
    public static DiscordUser CreateTestDiscordUser(
        Guid? id = null,
        string username = "testuser",
        string? globalName = "Test User",
        string? avatar = null)
    {
        return new DiscordUser(
            Id: id ?? Guid.NewGuid(),
            Username: username,
            GlobalName: globalName,
            Avatar: avatar
        );
    }

    /// <summary>
    /// Default test user Discord ID used across tests.
    /// </summary>
    public const string DefaultTestDiscordId = "123456789012345678";

    /// <summary>
    /// Secondary test user Discord ID for multi-user tests.
    /// </summary>
    public const string SecondaryTestDiscordId = "987654321098765432";

    /// <summary>
    /// Unauthorized test user Discord ID (not in whitelist).
    /// </summary>
    public const string UnauthorizedTestDiscordId = "999999999999999999";

    /// <summary>
    /// Creates a default test whitelist with the default test users.
    /// </summary>
    public static void CreateDefaultTestWhitelist(string? filePath = null)
    {
        CreateTestWhitelist(new[] { DefaultTestDiscordId, SecondaryTestDiscordId }, filePath);
    }
}
