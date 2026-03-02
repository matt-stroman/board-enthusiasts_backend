namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Platform-managed canonical publisher or storefront registry entry.
/// </summary>
internal sealed class SupportedPublisher
{
    /// <summary>
    /// Gets or sets the publisher identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the stable machine-friendly publisher key.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the public display name.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the canonical publisher homepage URL.
    /// </summary>
    public string HomepageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether this registry entry is available for selection.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC update timestamp.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets organization-owned integration connections using this publisher.
    /// </summary>
    public ICollection<IntegrationConnection> IntegrationConnections { get; set; } = [];
}
