using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Persistence;

/// <summary>
/// Entity Framework database context for the application-owned Board library data model.
/// </summary>
/// <param name="options">Configured EF Core options.</param>
internal sealed class BoardLibraryDbContext(DbContextOptions<BoardLibraryDbContext> options)
    : DbContext(options)
{
    /// <summary>
    /// Gets the application-owned user projections keyed to Keycloak subjects.
    /// </summary>
    public DbSet<AppUser> Users => Set<AppUser>();

    /// <summary>
    /// Gets optional linked Board profile projections for local users.
    /// </summary>
    public DbSet<UserBoardProfile> UserBoardProfiles => Set<UserBoardProfile>();

    /// <summary>
    /// Gets developer studios.
    /// </summary>
    public DbSet<Studio> Studios => Set<Studio>();

    /// <summary>
    /// Gets studio membership records for scoped developer roles.
    /// </summary>
    public DbSet<StudioMembership> StudioMemberships => Set<StudioMembership>();

    /// <summary>
    /// Gets public links associated with studio profiles.
    /// </summary>
    public DbSet<StudioLink> StudioLinks => Set<StudioLink>();

    /// <summary>
    /// Gets stable catalog title records.
    /// </summary>
    public DbSet<Title> Titles => Set<Title>();

    /// <summary>
    /// Gets versioned player-facing title metadata snapshots.
    /// </summary>
    public DbSet<TitleMetadataVersion> TitleMetadataVersions => Set<TitleMetadataVersion>();

    /// <summary>
    /// Gets the platform-managed supported publisher registry.
    /// </summary>
    public DbSet<SupportedPublisher> SupportedPublishers => Set<SupportedPublisher>();

    /// <summary>
    /// Gets studio-owned external publisher/store connections.
    /// </summary>
    public DbSet<IntegrationConnection> IntegrationConnections => Set<IntegrationConnection>();

    /// <summary>
    /// Gets title-scoped external acquisition bindings.
    /// </summary>
    public DbSet<TitleIntegrationBinding> TitleIntegrationBindings => Set<TitleIntegrationBinding>();

    /// <summary>
    /// Gets fixed-slot media assets associated with titles.
    /// </summary>
    public DbSet<TitleMediaAsset> TitleMediaAssets => Set<TitleMediaAsset>();

    /// <summary>
    /// Gets semver releases associated with titles.
    /// </summary>
    public DbSet<TitleRelease> TitleReleases => Set<TitleRelease>();

    /// <summary>
    /// Gets installable artifact metadata associated with releases.
    /// </summary>
    public DbSet<ReleaseArtifact> ReleaseArtifacts => Set<ReleaseArtifact>();

    /// <summary>
    /// Gets owned-title library entries for players.
    /// </summary>
    public DbSet<PlayerOwnedTitle> PlayerOwnedTitles => Set<PlayerOwnedTitle>();

    /// <summary>
    /// Gets private wishlist entries for players.
    /// </summary>
    public DbSet<PlayerWishlistEntry> PlayerWishlistEntries => Set<PlayerWishlistEntry>();

    /// <summary>
    /// Gets title-moderation reports submitted by players.
    /// </summary>
    public DbSet<TitleReport> TitleReports => Set<TitleReport>();

    /// <summary>
    /// Gets discussion messages associated with title reports.
    /// </summary>
    public DbSet<TitleReportMessage> TitleReportMessages => Set<TitleReportMessage>();

    /// <summary>
    /// Gets in-app notifications targeted to local users.
    /// </summary>
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    /// <summary>
    /// Gets the local projection of platform roles for users.
    /// </summary>
    public DbSet<UserPlatformRole> UserPlatformRoles => Set<UserPlatformRole>();

    /// <summary>
    /// Applies entity configurations from the API assembly.
    /// </summary>
    /// <param name="modelBuilder">Model builder used to configure the relational model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BoardLibraryDbContext).Assembly);
    }
}
