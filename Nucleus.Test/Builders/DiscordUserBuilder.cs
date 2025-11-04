using Nucleus.Discord;

namespace Nucleus.Test.Builders;

/// <summary>
/// Fluent builder for creating test DiscordUser instances.
/// </summary>
public class DiscordUserBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _username = "testuser";
    private string? _globalName = "Test User";
    private string? _avatar = null;

    public DiscordUserBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public DiscordUserBuilder WithUsername(string username)
    {
        _username = username;
        return this;
    }

    public DiscordUserBuilder WithGlobalName(string? globalName)
    {
        _globalName = globalName;
        return this;
    }

    public DiscordUserBuilder WithAvatar(string? avatar)
    {
        _avatar = avatar;
        return this;
    }

    /// <summary>
    /// Creates a user with an avatar hash.
    /// </summary>
    public DiscordUserBuilder WithDefaultAvatar()
    {
        _avatar = "a1b2c3d4e5f6g7h8i9j0";
        return this;
    }

    public DiscordUser Build()
    {
        return new DiscordUser(
            Id: _id,
            Username: _username,
            GlobalName: _globalName,
            Avatar: _avatar
        );
    }

    /// <summary>
    /// Creates a default test Discord user with common values.
    /// </summary>
    public static DiscordUser CreateDefault() => new DiscordUserBuilder().Build();

    /// <summary>
    /// Creates multiple Discord users with sequential usernames.
    /// </summary>
    public static IEnumerable<DiscordUser> CreateMany(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return new DiscordUserBuilder()
                .WithUsername($"testuser{i + 1}")
                .WithGlobalName($"Test User {i + 1}")
                .Build();
        }
    }
}
