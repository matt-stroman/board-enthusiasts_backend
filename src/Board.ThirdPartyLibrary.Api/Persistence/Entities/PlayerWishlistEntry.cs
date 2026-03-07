namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Private wishlist entry for a player.
/// </summary>
internal sealed class PlayerWishlistEntry
{
    /// <summary>
    /// Gets or sets the owning application user identifier.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the wished title identifier.
    /// </summary>
    public Guid TitleId { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the title was added.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the owning user projection.
    /// </summary>
    public AppUser User { get; set; } = null!;

    /// <summary>
    /// Gets or sets the wished title.
    /// </summary>
    public Title Title { get; set; } = null!;
}
