namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Application-owned developer enrollment request for a user projection.
/// </summary>
internal sealed class DeveloperEnrollmentRequest
{
    /// <summary>
    /// Gets or sets the developer enrollment request identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the applicant user identifier.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the current workflow status.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the UTC timestamp when the request was submitted.
    /// </summary>
    public DateTime RequestedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the request was reviewed.
    /// </summary>
    public DateTime? ReviewedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the reviewer user identifier when the request has been reviewed.
    /// </summary>
    public Guid? ReviewedByUserId { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC update timestamp.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the applicant user projection.
    /// </summary>
    public AppUser User { get; set; } = null!;

    /// <summary>
    /// Gets or sets the reviewer user projection when one exists.
    /// </summary>
    public AppUser? ReviewedByUser { get; set; }
}
