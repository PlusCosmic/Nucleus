namespace Nucleus.Auth;

/// <summary>
/// Represents a single entry in the whitelist configuration.
/// </summary>
public class WhitelistEntry
{
    public string DiscordId { get; set; } = string.Empty;
    public string Role { get; set; } = nameof(UserRole.Viewer);

    public UserRole GetRole()
    {
        return Enum.TryParse<UserRole>(Role, ignoreCase: true, out var role)
            ? role
            : UserRole.Viewer;
    }
}

/// <summary>
/// Configuration model for whitelist.json.
/// Supports both the new format (Users array with roles) and legacy format (flat array).
/// </summary>
public class WhitelistConfig
{
    public List<WhitelistEntry> Users { get; set; } = [];
}
