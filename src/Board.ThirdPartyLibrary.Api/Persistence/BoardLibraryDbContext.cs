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
    /// Gets developer organizations.
    /// </summary>
    public DbSet<Organization> Organizations => Set<Organization>();

    /// <summary>
    /// Gets organization membership records for scoped developer roles.
    /// </summary>
    public DbSet<OrganizationMembership> OrganizationMemberships => Set<OrganizationMembership>();

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
    /// Gets organization-owned external publisher/store connections.
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
    /// Applies entity configurations from the API assembly.
    /// </summary>
    /// <param name="modelBuilder">Model builder used to configure the relational model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BoardLibraryDbContext).Assembly);
    }
}
