namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Join entity linking an application user to a studio role.
/// </summary>
internal sealed class StudioMembership
{
    /// <summary>
    /// Gets or sets the owning studio identifier.
    /// </summary>
    public Guid StudioId { get; set; }

    /// <summary>
    /// Gets or sets the member user identifier.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the studio-scoped membership role.
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
    /// Gets or sets the owning studio.
    /// </summary>
    public Studio Studio { get; set; } = null!;

    /// <summary>
    /// Gets or sets the member user projection.
    /// </summary>
    public AppUser User { get; set; } = null!;
}
