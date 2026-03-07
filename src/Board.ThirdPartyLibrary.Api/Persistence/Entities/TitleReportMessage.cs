namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Thread message exchanged during title-report review.
/// </summary>
internal sealed class TitleReportMessage
{
    /// <summary>
    /// Gets or sets the message identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the owning title-report identifier.
    /// </summary>
    public Guid TitleReportId { get; set; }

    /// <summary>
    /// Gets or sets the authoring user identifier.
    /// </summary>
    public Guid AuthorUserId { get; set; }

    /// <summary>
    /// Gets or sets the author role for rendering and workflow state transitions.
    /// </summary>
    public string AuthorRole { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the message body.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets which participant lane can see the message.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the owning report.
    /// </summary>
    public TitleReport TitleReport { get; set; } = null!;

    /// <summary>
    /// Gets or sets the authoring user projection.
    /// </summary>
    public AppUser AuthorUser { get; set; } = null!;
}
