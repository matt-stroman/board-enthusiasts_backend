namespace Board.ThirdPartyLibrary.Api.Identity;

/// <summary>
/// Platform roles supported by the Board Enthusiasts.
/// </summary>
internal static class PlatformRoleCatalog
{
    /// <summary>
    /// Gets the supported platform roles.
    /// </summary>
    public static IReadOnlyList<PlatformRoleDefinition> Roles { get; } =
    [
        new("player", "Player", "platform"),
        new("developer", "Developer", "platform"),
        new("verified_developer", "VerifiedDeveloper", "platform"),
        new("super_admin", "SuperAdmin", "platform"),
        new("admin", "Admin", "platform"),
        new("moderator", "Moderator", "platform")
    ];
}

/// <summary>
/// Platform role metadata returned by the API.
/// </summary>
/// <param name="Code">Stable role code.</param>
/// <param name="DisplayName">Human-friendly role name.</param>
/// <param name="Scope">Role scope.</param>
internal sealed record PlatformRoleDefinition(string Code, string DisplayName, string Scope);
