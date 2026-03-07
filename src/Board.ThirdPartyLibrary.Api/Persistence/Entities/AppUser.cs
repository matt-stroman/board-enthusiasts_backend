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
    /// Gets or sets the application-managed username shown in profile surfaces.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the application-managed first name.
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the application-managed last name.
    /// </summary>
    public string? LastName { get; set; }

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
    /// Gets or sets the avatar URL configured for the user when the user prefers a hosted avatar image.
    /// </summary>
    public string? AvatarUrl { get; set; }

    /// <summary>
    /// Gets or sets the content type for an uploaded avatar image.
    /// </summary>
    public string? AvatarImageContentType { get; set; }

    /// <summary>
    /// Gets or sets the uploaded avatar image content.
    /// </summary>
    public byte[]? AvatarImageData { get; set; }

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
    /// Gets or sets the studio memberships for this user projection.
    /// </summary>
    public ICollection<StudioMembership> StudioMemberships { get; set; } = [];

    /// <summary>
    /// Gets or sets the locally projected platform roles for this user.
    /// </summary>
    public ICollection<UserPlatformRole> PlatformRoles { get; set; } = [];

    /// <summary>
    /// Gets or sets owned-title library entries for this user.
    /// </summary>
    public ICollection<PlayerOwnedTitle> OwnedTitles { get; set; } = [];

    /// <summary>
    /// Gets or sets wishlist entries for this user.
    /// </summary>
    public ICollection<PlayerWishlistEntry> WishlistEntries { get; set; } = [];

    /// <summary>
    /// Gets or sets title reports submitted by this user.
    /// </summary>
    public ICollection<TitleReport> SubmittedTitleReports { get; set; } = [];

    /// <summary>
    /// Gets or sets report-thread messages authored by this user.
    /// </summary>
    public ICollection<TitleReportMessage> TitleReportMessages { get; set; } = [];

    /// <summary>
    /// Gets or sets title reports resolved by this user.
    /// </summary>
    public ICollection<TitleReport> ResolvedTitleReports { get; set; } = [];

    /// <summary>
    /// Gets or sets in-app notifications targeted to this user.
    /// </summary>
    public ICollection<UserNotification> Notifications { get; set; } = [];
}
