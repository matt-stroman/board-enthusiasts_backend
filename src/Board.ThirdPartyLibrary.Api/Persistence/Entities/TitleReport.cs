namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Player-submitted moderation report for a title.
/// </summary>
internal sealed class TitleReport
{
    /// <summary>
    /// Gets or sets the report identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the reported title identifier.
    /// </summary>
    public Guid TitleId { get; set; }

    /// <summary>
    /// Gets or sets the reporting user identifier.
    /// </summary>
    public Guid ReporterUserId { get; set; }

    /// <summary>
    /// Gets or sets the current moderation workflow status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the player-supplied report reason.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the moderation resolution note when one exists.
    /// </summary>
    public string? ResolutionNote { get; set; }

    /// <summary>
    /// Gets or sets the resolving moderator user identifier when the report is closed.
    /// </summary>
    public Guid? ResolvedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the report was resolved.
    /// </summary>
    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the most recent update.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the reported title.
    /// </summary>
    public Title Title { get; set; } = null!;

    /// <summary>
    /// Gets or sets the reporting user projection.
    /// </summary>
    public AppUser ReporterUser { get; set; } = null!;

    /// <summary>
    /// Gets or sets the resolving moderator projection when available.
    /// </summary>
    public AppUser? ResolvedByUser { get; set; }

    /// <summary>
    /// Gets or sets the thread messages associated with the report.
    /// </summary>
    public ICollection<TitleReportMessage> Messages { get; set; } = [];
}
