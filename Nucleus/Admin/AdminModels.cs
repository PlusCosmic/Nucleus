using Nucleus.Shared.Auth;

namespace Nucleus.Admin;

/// <summary>
/// Represents a user with their role and permissions for admin views.
/// </summary>
public record UserWithPermissions(
    Guid Id,
    string DiscordId,
    string Username,
    string? GlobalName,
    string? Avatar,
    UserRole Role,
    List<string> AdditionalPermissions,
    List<string> EffectivePermissions
);

/// <summary>
/// Request to grant a permission to a user.
/// </summary>
public record GrantPermissionRequest(string Permission);
