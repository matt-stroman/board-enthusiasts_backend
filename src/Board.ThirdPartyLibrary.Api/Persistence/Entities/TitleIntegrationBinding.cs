namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Title-scoped external acquisition binding pointing at a reusable integration connection.
/// </summary>
internal sealed class TitleIntegrationBinding
{
    /// <summary>
    /// Gets or sets the binding identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the title identifier.
    /// </summary>
    public Guid TitleId { get; set; }

    /// <summary>
    /// Gets or sets the integration connection identifier.
    /// </summary>
    public Guid IntegrationConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the player-facing external acquisition URL.
    /// </summary>
    public string AcquisitionUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional player-facing acquisition label.
    /// </summary>
    public string? AcquisitionLabel { get; set; }

    /// <summary>
    /// Gets or sets free-form provider-specific configuration values.
    /// </summary>
    public string? ConfigurationJson { get; set; }

    /// <summary>
    /// Gets or sets whether this is the primary active acquisition binding for the title.
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// Gets or sets whether the binding is active.
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
    /// Gets or sets the bound title.
    /// </summary>
    public Title Title { get; set; } = null!;

    /// <summary>
    /// Gets or sets the bound integration connection.
    /// </summary>
    public IntegrationConnection IntegrationConnection { get; set; } = null!;
}
