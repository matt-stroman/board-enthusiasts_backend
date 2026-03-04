namespace Board.ThirdPartyLibrary.Api.Persistence.Entities;

/// <summary>
/// Application-owned user projection keyed to a Keycloak subject.
/// </summary>
internal sealed class AppUser
{
    /// <summary>
    /// Gets or sets the application user identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the immutable Keycloak subject identifier.
    /// </summary>
    public string KeycloakSubject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the cached display name when available.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the cached email address when available.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets whether the cached email address was verified by Keycloak.
    /// </summary>
    public bool EmailVerified { get; set; }

    /// <summary>
    /// Gets or sets the cached upstream identity provider name when available.
    /// </summary>
    public string? IdentityProvider { get; set; }

    /// <summary>
    /// Gets or sets the UTC creation timestamp.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC update timestamp.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the optional linked Board profile.
    /// </summary>
    public UserBoardProfile? BoardProfile { get; set; }

    /// <summary>
    /// Gets or sets the optional developer enrollment request for this user.
    /// </summary>
    public DeveloperEnrollmentRequest? DeveloperEnrollmentRequest { get; set; }

    /// <summary>
    /// Gets or sets the organization memberships for this user projection.
    /// </summary>
    public ICollection<OrganizationMembership> OrganizationMemberships { get; set; } = [];

    /// <summary>
    /// Gets or sets developer enrollment requests this user has reviewed.
    /// </summary>
    public ICollection<DeveloperEnrollmentRequest> ReviewedDeveloperEnrollmentRequests { get; set; } = [];
}
