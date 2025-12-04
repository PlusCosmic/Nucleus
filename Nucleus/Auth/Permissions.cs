namespace Nucleus.Auth;

/// <summary>
/// Defines all available permissions in the system and their role defaults.
/// </summary>
public static class Permissions
{
    // Wildcard permission - grants all permissions
    public const string All = "*";

    // Clips permissions
    public const string ClipsRead = "clips.read";
    public const string ClipsCreate = "clips.create";
    public const string ClipsEdit = "clips.edit";
    public const string ClipsDelete = "clips.delete";

    // Playlist permissions
    public const string PlaylistsRead = "playlists.read";
    public const string PlaylistsManage = "playlists.manage";

    // Links permissions
    public const string LinksRead = "links.read";
    public const string LinksManage = "links.manage";

    // Minecraft permissions
    public const string MinecraftStatus = "minecraft.status";
    public const string MinecraftConsole = "minecraft.console";
    public const string MinecraftFiles = "minecraft.files";

    // Admin permissions
    public const string AdminUsers = "admin.users";

    /// <summary>
    /// Default permissions for each role.
    /// </summary>
    public static readonly IReadOnlyDictionary<UserRole, HashSet<string>> RoleDefaults =
        new Dictionary<UserRole, HashSet<string>>
        {
            [UserRole.Viewer] = new()
            {
                ClipsRead,
                PlaylistsRead,
                LinksRead,
                MinecraftStatus
            },
            [UserRole.Editor] = new()
            {
                // All Viewer permissions
                ClipsRead,
                PlaylistsRead,
                LinksRead,
                MinecraftStatus,
                // Plus Editor-specific
                ClipsCreate,
                ClipsEdit,
                PlaylistsManage,
                LinksManage
            },
            [UserRole.Admin] = new()
            {
                All // Admins have all permissions
            }
        };

    /// <summary>
    /// Gets the default permissions for a role.
    /// </summary>
    public static HashSet<string> GetRolePermissions(UserRole role)
    {
        return RoleDefaults.TryGetValue(role, out var permissions)
            ? new HashSet<string>(permissions)
            : new HashSet<string>();
    }
}
