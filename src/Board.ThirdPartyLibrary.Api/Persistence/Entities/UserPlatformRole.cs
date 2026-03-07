namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Local projection of a platform role observed on the authenticated user's claims.
/// </summary>
internal sealed class UserPlatformRole
{
    /// <summary>
    /// Gets or sets the user identifier.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the platform role code.
    /// </summary>
    public string Role { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC update timestamp.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the owning user projection.
    /// </summary>
    public AppUser User { get; set; } = null!;
}
