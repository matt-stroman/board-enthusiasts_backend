using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Identity;
using Board.ThirdPartyLibrary.Api.Organizations;
using Board.ThirdPartyLibrary.Api.Persistence;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Titles;

/// <summary>
/// Service contract for public catalog queries and authenticated title management.
/// </summary>
internal interface ITitleService
{
    /// <summary>
    /// Lists public catalog titles that are currently discoverable.
    /// </summary>
    /// <param name="organizationSlug">Optional organization route key filter.</param>
    /// <param name="contentKind">Optional content kind filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Catalog title summaries visible to the caller.</returns>
    Task<IReadOnlyList<TitleSnapshot>> ListPublicTitlesAsync(
        string? organizationSlug,
        string? contentKind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a public catalog title by organization and title route key.
    /// </summary>
    /// <param name="organizationSlug">Organization route key.</param>
    /// <param name="titleSlug">Title route key scoped to the organization.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The public catalog title when visible; otherwise <see langword="null" />.</returns>
    Task<TitleSnapshot?> GetPublicTitleAsync(
        string organizationSlug,
        string titleSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists titles for an organization when the caller has a managing membership role.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="organizationId">Organization identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mutation-style result describing access and data.</returns>
    Task<TitleListResult> ListOrganizationTitlesAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new title and its initial metadata revision.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="organizationId">Owning organization identifier.</param>
    /// <param name="command">Stable title fields and initial metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mutation result describing the outcome.</returns>
    Task<TitleMutationResult> CreateTitleAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        CreateTitleCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a title for an authorized developer caller.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="titleId">Title identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mutation result describing the outcome.</returns>
    Task<TitleMutationResult> GetTitleAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates stable title fields such as slug, lifecycle status, and visibility.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="titleId">Title identifier.</param>
    /// <param name="command">Updated stable title fields.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mutation result describing the outcome.</returns>
    Task<TitleMutationResult> UpdateTitleAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        UpdateTitleCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the current metadata revision or creates a new revision when history must be preserved.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="titleId">Title identifier.</param>
    /// <param name="command">Metadata fields to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mutation result describing the outcome.</returns>
    Task<TitleMutationResult> UpsertCurrentMetadataAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        UpsertTitleMetadataCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all metadata revisions for a title when the caller is authorized to manage it.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="titleId">Title identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List result describing the outcome.</returns>
    Task<TitleMetadataVersionListResult> ListMetadataVersionsAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates an existing metadata revision for a title.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="titleId">Title identifier.</param>
    /// <param name="revisionNumber">Revision number to activate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mutation result describing the outcome.</returns>
    Task<TitleMutationResult> ActivateMetadataVersionAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        int revisionNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists media assets for a title when the caller is authorized to manage it.
    /// </summary>
    Task<TitleMediaAssetListResult> ListMediaAssetsAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates a fixed-slot media asset for a title.
    /// </summary>
    Task<TitleMediaAssetMutationResult> UpsertMediaAssetAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        string mediaRole,
        UpsertTitleMediaAssetCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a fixed-slot media asset from a title.
    /// </summary>
    Task<TitleMediaAssetMutationResult> DeleteMediaAssetAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        string mediaRole,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists releases for a title when the caller is authorized to manage it.
    /// </summary>
    Task<TitleReleaseListResult> ListReleasesAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific release for a title when the caller is authorized to manage it.
    /// </summary>
    Task<TitleReleaseMutationResult> GetReleaseAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new draft release for a title.
    /// </summary>
    Task<TitleReleaseMutationResult> CreateReleaseAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CreateTitleReleaseCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing draft release for a title.
    /// </summary>
    Task<TitleReleaseMutationResult> UpdateReleaseAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        UpdateTitleReleaseCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a draft release for a title.
    /// </summary>
    Task<TitleReleaseMutationResult> PublishReleaseAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a published release as the title's current release.
    /// </summary>
    Task<TitleMutationResult> ActivateReleaseAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws a published release from active use.
    /// </summary>
    Task<TitleReleaseMutationResult> WithdrawReleaseAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists artifacts for a release when the caller is authorized to manage it.
    /// </summary>
    Task<ReleaseArtifactListResult> ListReleaseArtifactsAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new artifact for a draft release.
    /// </summary>
    Task<ReleaseArtifactMutationResult> CreateReleaseArtifactAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        UpsertReleaseArtifactCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing artifact on a draft release.
    /// </summary>
    Task<ReleaseArtifactMutationResult> UpdateReleaseArtifactAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        Guid artifactId,
        UpsertReleaseArtifactCommand command,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an existing artifact from a draft release.
    /// </summary>
    Task<ReleaseArtifactMutationResult> DeleteReleaseArtifactAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        Guid artifactId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Entity Framework-backed implementation of <see cref="ITitleService" />.
/// </summary>
/// <param name="dbContext">Application database context.</param>
/// <param name="identityPersistenceService">Current-user projection helper.</param>
internal sealed partial class TitleService(
    BoardLibraryDbContext dbContext,
    IIdentityPersistenceService identityPersistenceService) : ITitleService
{
    public async Task<IReadOnlyList<TitleSnapshot>> ListPublicTitlesAsync(
        string? organizationSlug,
        string? contentKind,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Titles
            .AsNoTracking()
            .Include(candidate => candidate.Organization)
            .Include(candidate => candidate.CurrentMetadataVersion)
            .Include(candidate => candidate.MediaAssets)
            .Include(candidate => candidate.IntegrationBindings)
                .ThenInclude(candidate => candidate.IntegrationConnection)
                    .ThenInclude(candidate => candidate.SupportedPublisher)
            .Where(candidate =>
                candidate.CurrentMetadataVersionId != null &&
                candidate.CurrentMetadataVersion != null &&
                candidate.Visibility == TitleVisibilities.Listed &&
                (candidate.LifecycleStatus == TitleLifecycleStatuses.Testing ||
                 candidate.LifecycleStatus == TitleLifecycleStatuses.Published));

        if (!string.IsNullOrWhiteSpace(organizationSlug))
        {
            query = query.Where(candidate => candidate.Organization.Slug == organizationSlug);
        }

        if (!string.IsNullOrWhiteSpace(contentKind))
        {
            query = query.Where(candidate => candidate.ContentKind == contentKind);
        }

        return await query
            .OrderBy(candidate => candidate.Organization.DisplayName)
            .ThenBy(candidate => candidate.CurrentMetadataVersion!.DisplayName)
            .Select(candidate => MapTitle(candidate, includeDescription: false))
            .ToListAsync(cancellationToken);
    }

    public async Task<TitleSnapshot?> GetPublicTitleAsync(
        string organizationSlug,
        string titleSlug,
        CancellationToken cancellationToken = default)
    {
        var title = await dbContext.Titles
            .AsNoTracking()
            .Include(candidate => candidate.Organization)
            .Include(candidate => candidate.CurrentMetadataVersion)
            .Include(candidate => candidate.MediaAssets)
            .Include(candidate => candidate.IntegrationBindings)
                .ThenInclude(candidate => candidate.IntegrationConnection)
                    .ThenInclude(candidate => candidate.SupportedPublisher)
            .Include(candidate => candidate.CurrentRelease)
                .ThenInclude(candidate => candidate!.MetadataVersion)
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Organization.Slug == organizationSlug &&
                    candidate.Slug == titleSlug &&
                    candidate.CurrentMetadataVersionId != null &&
                    candidate.CurrentMetadataVersion != null &&
                    candidate.Visibility != TitleVisibilities.Private &&
                    (candidate.LifecycleStatus == TitleLifecycleStatuses.Testing ||
                     candidate.LifecycleStatus == TitleLifecycleStatuses.Published),
                cancellationToken);

        return title is null ? null : MapTitle(title, includeDescription: true);
    }

    public async Task<TitleListResult> ListOrganizationTitlesAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var organizationExists = await dbContext.Organizations
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id == organizationId, cancellationToken);

        if (!organizationExists)
        {
            return new TitleListResult(TitleListStatus.NotFound);
        }

        var actorRole = await GetActorOrganizationRoleAsync(organizationId, actor.Id, cancellationToken);
        if (!CanManageTitles(actorRole))
        {
            return new TitleListResult(TitleListStatus.Forbidden);
        }

        var titles = await dbContext.Titles
            .AsNoTracking()
            .Include(candidate => candidate.Organization)
            .Include(candidate => candidate.CurrentMetadataVersion)
            .Include(candidate => candidate.MediaAssets)
            .Include(candidate => candidate.IntegrationBindings)
                .ThenInclude(candidate => candidate.IntegrationConnection)
                    .ThenInclude(candidate => candidate.SupportedPublisher)
            .Where(candidate => candidate.OrganizationId == organizationId && candidate.CurrentMetadataVersionId != null)
            .OrderBy(candidate => candidate.CurrentMetadataVersion!.DisplayName)
            .Select(candidate => MapTitle(candidate, includeDescription: false))
            .ToListAsync(cancellationToken);

        return new TitleListResult(TitleListStatus.Success, titles);
    }

    public async Task<TitleMutationResult> CreateTitleAsync(
        IEnumerable<Claim> claims,
        Guid organizationId,
        CreateTitleCommand command,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);

        if (organization is null)
        {
            return new TitleMutationResult(TitleMutationStatus.NotFound);
        }

        var actorRole = await GetActorOrganizationRoleAsync(organizationId, actor.Id, cancellationToken);
        if (!CanManageTitles(actorRole))
        {
            return new TitleMutationResult(TitleMutationStatus.Forbidden);
        }

        var now = DateTime.UtcNow;
        var title = new Title
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Slug = command.Slug,
            ContentKind = command.ContentKind,
            LifecycleStatus = command.LifecycleStatus,
            Visibility = command.Visibility,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        var metadataVersion = new TitleMetadataVersion
        {
            Id = Guid.NewGuid(),
            TitleId = title.Id,
            RevisionNumber = 1,
            DisplayName = command.Metadata.DisplayName,
            ShortDescription = command.Metadata.ShortDescription,
            Description = command.Metadata.Description,
            GenreDisplay = command.Metadata.GenreDisplay,
            MinPlayers = command.Metadata.MinPlayers,
            MaxPlayers = command.Metadata.MaxPlayers,
            AgeRatingAuthority = command.Metadata.AgeRatingAuthority,
            AgeRatingValue = command.Metadata.AgeRatingValue,
            MinAgeYears = command.Metadata.MinAgeYears,
            IsFrozen = command.LifecycleStatus != TitleLifecycleStatuses.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Titles.Add(title);
        dbContext.TitleMetadataVersions.Add(metadataVersion);

        try
        {
            await ExecuteInOptionalTransactionAsync(
                async () =>
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                    title.CurrentMetadataVersionId = metadataVersion.Id;
                    title.UpdatedAtUtc = now;
                    await dbContext.SaveChangesAsync(cancellationToken);
                },
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new TitleMutationResult(TitleMutationStatus.Conflict);
        }

        return new TitleMutationResult(
            TitleMutationStatus.Success,
            MapTitle(title, organization, metadataVersion, includeDescription: true));
    }

    public async Task<TitleMutationResult> GetTitleAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken = default)
    {
        var title = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        return title.Status switch
        {
            TitleAccessStatus.Success => new TitleMutationResult(
                TitleMutationStatus.Success,
                MapTitle(title.Title!, includeDescription: true)),
            TitleAccessStatus.NotFound => new TitleMutationResult(TitleMutationStatus.NotFound),
            TitleAccessStatus.Forbidden => new TitleMutationResult(TitleMutationStatus.Forbidden),
            _ => new TitleMutationResult(TitleMutationStatus.NotFound)
        };
    }

    public async Task<TitleMutationResult> UpdateTitleAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        UpdateTitleCommand command,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleMutationResult(TitleMutationStatus.Forbidden)
                : new TitleMutationResult(TitleMutationStatus.NotFound);
        }

        var title = access.Title!;
        var now = DateTime.UtcNow;
        var isLeavingDraft =
            title.LifecycleStatus == TitleLifecycleStatuses.Draft &&
            command.LifecycleStatus != TitleLifecycleStatuses.Draft;

        title.Slug = command.Slug;
        title.ContentKind = command.ContentKind;
        title.LifecycleStatus = command.LifecycleStatus;
        title.Visibility = command.Visibility;
        title.UpdatedAtUtc = now;

        if (isLeavingDraft && title.CurrentMetadataVersion is not null && !title.CurrentMetadataVersion.IsFrozen)
        {
            title.CurrentMetadataVersion.IsFrozen = true;
            title.CurrentMetadataVersion.UpdatedAtUtc = now;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return new TitleMutationResult(TitleMutationStatus.Success, MapTitle(title, includeDescription: true));
        }
        catch (DbUpdateException)
        {
            return new TitleMutationResult(TitleMutationStatus.Conflict);
        }
    }

    public async Task<TitleMutationResult> UpsertCurrentMetadataAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        UpsertTitleMetadataCommand command,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleMutationResult(TitleMutationStatus.Forbidden)
                : new TitleMutationResult(TitleMutationStatus.NotFound);
        }

        var title = access.Title!;
        var now = DateTime.UtcNow;

        if (title.CurrentMetadataVersion is not null &&
            title.LifecycleStatus == TitleLifecycleStatuses.Draft &&
            !title.CurrentMetadataVersion.IsFrozen)
        {
            ApplyMetadata(title.CurrentMetadataVersion, command, now);
            title.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new TitleMutationResult(TitleMutationStatus.Success, MapTitle(title, includeDescription: true));
        }

        var nextRevision = await dbContext.TitleMetadataVersions
            .Where(candidate => candidate.TitleId == title.Id)
            .Select(candidate => (int?)candidate.RevisionNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var newMetadataVersion = new TitleMetadataVersion
        {
            Id = Guid.NewGuid(),
            TitleId = title.Id,
            RevisionNumber = nextRevision + 1,
            IsFrozen = title.LifecycleStatus != TitleLifecycleStatuses.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        ApplyMetadata(newMetadataVersion, command, now);

        dbContext.TitleMetadataVersions.Add(newMetadataVersion);

        await ExecuteInOptionalTransactionAsync(
            async () =>
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                title.CurrentMetadataVersionId = newMetadataVersion.Id;
                title.UpdatedAtUtc = now;
                await dbContext.SaveChangesAsync(cancellationToken);
            },
            cancellationToken);

        return new TitleMutationResult(TitleMutationStatus.Success, MapTitle(title, includeDescription: true));
    }

    public async Task<TitleMetadataVersionListResult> ListMetadataVersionsAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleMetadataVersionListResult(TitleMetadataVersionListStatus.Forbidden)
                : new TitleMetadataVersionListResult(TitleMetadataVersionListStatus.NotFound);
        }

        var versions = await dbContext.TitleMetadataVersions
            .AsNoTracking()
            .Where(candidate => candidate.TitleId == titleId)
            .OrderBy(candidate => candidate.RevisionNumber)
            .Select(candidate => new TitleMetadataVersionSnapshot(
                candidate.RevisionNumber,
                candidate.Id == access.Title!.CurrentMetadataVersionId,
                candidate.IsFrozen,
                candidate.DisplayName,
                candidate.ShortDescription,
                candidate.Description,
                candidate.GenreDisplay,
                candidate.MinPlayers,
                candidate.MaxPlayers,
                candidate.AgeRatingAuthority,
                candidate.AgeRatingValue,
                candidate.MinAgeYears,
                candidate.CreatedAtUtc,
                candidate.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return new TitleMetadataVersionListResult(TitleMetadataVersionListStatus.Success, versions);
    }

    public async Task<TitleMutationResult> ActivateMetadataVersionAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        int revisionNumber,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleMutationResult(TitleMutationStatus.Forbidden)
                : new TitleMutationResult(TitleMutationStatus.NotFound);
        }

        var title = access.Title!;
        var metadataVersion = await dbContext.TitleMetadataVersions
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.RevisionNumber == revisionNumber,
                cancellationToken);

        if (metadataVersion is null)
        {
            return new TitleMutationResult(TitleMutationStatus.NotFound);
        }

        var now = DateTime.UtcNow;
        title.CurrentMetadataVersionId = metadataVersion.Id;
        title.UpdatedAtUtc = now;

        if (title.LifecycleStatus != TitleLifecycleStatuses.Draft && !metadataVersion.IsFrozen)
        {
            metadataVersion.IsFrozen = true;
            metadataVersion.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new TitleMutationResult(TitleMutationStatus.Success, MapTitle(title, includeDescription: true));
    }

    private async Task<TitleAccessResult> LoadManagedTitleAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var title = await dbContext.Titles
            .Include(candidate => candidate.Organization)
            .Include(candidate => candidate.CurrentMetadataVersion)
            .Include(candidate => candidate.MediaAssets)
            .Include(candidate => candidate.IntegrationBindings)
                .ThenInclude(candidate => candidate.IntegrationConnection)
                    .ThenInclude(candidate => candidate.SupportedPublisher)
            .Include(candidate => candidate.CurrentRelease)
                .ThenInclude(candidate => candidate!.MetadataVersion)
            .SingleOrDefaultAsync(candidate => candidate.Id == titleId, cancellationToken);

        if (title is null)
        {
            return new TitleAccessResult(TitleAccessStatus.NotFound);
        }

        var actorRole = await GetActorOrganizationRoleAsync(title.OrganizationId, actor.Id, cancellationToken);
        if (!CanManageTitles(actorRole))
        {
            return new TitleAccessResult(TitleAccessStatus.Forbidden);
        }

        return new TitleAccessResult(TitleAccessStatus.Success, title);
    }

    private async Task<AppUser> EnsureActorAsync(IEnumerable<Claim> claims, CancellationToken cancellationToken)
    {
        await identityPersistenceService.EnsureCurrentUserProjectionAsync(claims, cancellationToken);
        var subject = GetRequiredSubject(claims);

        return await dbContext.Users.SingleAsync(candidate => candidate.KeycloakSubject == subject, cancellationToken);
    }

    private async Task<string?> GetActorOrganizationRoleAsync(Guid organizationId, Guid actorUserId, CancellationToken cancellationToken) =>
        await dbContext.OrganizationMemberships
            .Where(candidate => candidate.OrganizationId == organizationId && candidate.UserId == actorUserId)
            .Select(candidate => candidate.Role)
            .SingleOrDefaultAsync(cancellationToken);

    private static bool CanManageTitles(string? role) =>
        string.Equals(role, OrganizationRoles.Owner, StringComparison.Ordinal) ||
        string.Equals(role, OrganizationRoles.Admin, StringComparison.Ordinal) ||
        string.Equals(role, OrganizationRoles.Editor, StringComparison.Ordinal);

    private static void ApplyMetadata(TitleMetadataVersion metadataVersion, UpsertTitleMetadataCommand command, DateTime now)
    {
        metadataVersion.DisplayName = command.DisplayName;
        metadataVersion.ShortDescription = command.ShortDescription;
        metadataVersion.Description = command.Description;
        metadataVersion.GenreDisplay = command.GenreDisplay;
        metadataVersion.MinPlayers = command.MinPlayers;
        metadataVersion.MaxPlayers = command.MaxPlayers;
        metadataVersion.AgeRatingAuthority = command.AgeRatingAuthority;
        metadataVersion.AgeRatingValue = command.AgeRatingValue;
        metadataVersion.MinAgeYears = command.MinAgeYears;
        metadataVersion.UpdatedAtUtc = now;
    }

    private static TitleSnapshot MapTitle(Title title, bool includeDescription) =>
        MapTitle(title, title.Organization, title.CurrentMetadataVersion!, includeDescription);

    private static TitleSnapshot MapTitle(
        Title title,
        Organization organization,
        TitleMetadataVersion metadataVersion,
        bool includeDescription) =>
        CreateTitleSnapshot(title, organization, metadataVersion, includeDescription);

    private static TitleSnapshot CreateTitleSnapshot(
        Title title,
        Organization organization,
        TitleMetadataVersion metadataVersion,
        bool includeDescription)
    {
        var acquisitionBinding = title.IntegrationBindings
            .Where(candidate => candidate.IsEnabled && candidate.IsPrimary && candidate.IntegrationConnection.IsEnabled)
            .OrderBy(candidate => candidate.CreatedAtUtc)
            .FirstOrDefault();

        return new TitleSnapshot(
            title.Id,
            title.OrganizationId,
            organization.Slug,
            title.Slug,
            title.ContentKind,
            title.LifecycleStatus,
            title.Visibility,
            metadataVersion.RevisionNumber,
            metadataVersion.DisplayName,
            metadataVersion.ShortDescription,
            includeDescription ? metadataVersion.Description : null,
            metadataVersion.GenreDisplay,
            metadataVersion.MinPlayers,
            metadataVersion.MaxPlayers,
            metadataVersion.AgeRatingAuthority,
            metadataVersion.AgeRatingValue,
            metadataVersion.MinAgeYears,
            title.CurrentReleaseId,
            title.MediaAssets
                .SingleOrDefault(candidate => candidate.MediaRole == TitleMediaRoles.Card)
                ?.SourceUrl,
            acquisitionBinding?.AcquisitionUrl,
            title.MediaAssets
                .OrderBy(candidate => candidate.MediaRole)
                .Select(MapMediaAsset)
                .ToArray(),
            MapCurrentRelease(title.CurrentRelease),
            MapPublicTitleAcquisition(acquisitionBinding),
            includeDescription ? title.CreatedAtUtc : null,
            includeDescription ? title.UpdatedAtUtc : null);
    }

    private static PublicTitleAcquisitionSnapshot? MapPublicTitleAcquisition(TitleIntegrationBinding? binding)
    {
        if (binding is null)
        {
            return null;
        }

        var providerDisplayName = binding.IntegrationConnection.SupportedPublisher?.DisplayName
            ?? binding.IntegrationConnection.CustomPublisherDisplayName;

        if (string.IsNullOrWhiteSpace(providerDisplayName))
        {
            return null;
        }

        return new PublicTitleAcquisitionSnapshot(
            binding.AcquisitionUrl,
            binding.AcquisitionLabel,
            providerDisplayName,
            binding.IntegrationConnection.SupportedPublisher?.HomepageUrl
                ?? binding.IntegrationConnection.CustomPublisherHomepageUrl);
    }

    private static string GetRequiredSubject(IEnumerable<Claim> claims)
    {
        var subject = claims.FirstOrDefault(claim => string.Equals(claim.Type, "sub", StringComparison.OrdinalIgnoreCase))?.Value;

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new InvalidOperationException("Authenticated user is missing the required subject claim.");
        }

        return subject;
    }

    private async Task ExecuteInOptionalTransactionAsync(Func<Task> action, CancellationToken cancellationToken)
    {
        if (string.Equals(dbContext.Database.ProviderName, "Microsoft.EntityFrameworkCore.InMemory", StringComparison.Ordinal))
        {
            await action();
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await action();
        await transaction.CommitAsync(cancellationToken);
    }
}

/// <summary>
/// Command used to create a title with its initial metadata revision.
/// </summary>
/// <param name="Slug">Organization-scoped title route key.</param>
/// <param name="ContentKind">Stable title content kind.</param>
/// <param name="LifecycleStatus">Lifecycle status for the title.</param>
/// <param name="Visibility">Public discoverability mode.</param>
/// <param name="Metadata">Initial metadata payload.</param>
internal sealed record CreateTitleCommand(
    string Slug,
    string ContentKind,
    string LifecycleStatus,
    string Visibility,
    UpsertTitleMetadataCommand Metadata);

/// <summary>
/// Command used to update stable title fields.
/// </summary>
/// <param name="Slug">Organization-scoped title route key.</param>
/// <param name="ContentKind">Stable title content kind.</param>
/// <param name="LifecycleStatus">Lifecycle status for the title.</param>
/// <param name="Visibility">Public discoverability mode.</param>
internal sealed record UpdateTitleCommand(
    string Slug,
    string ContentKind,
    string LifecycleStatus,
    string Visibility);

/// <summary>
/// Command payload for updating or creating a title metadata revision.
/// </summary>
/// <param name="DisplayName">Public display name.</param>
/// <param name="ShortDescription">Short public description.</param>
/// <param name="Description">Full public description.</param>
/// <param name="GenreDisplay">Display-oriented genre text.</param>
/// <param name="MinPlayers">Minimum supported player count.</param>
/// <param name="MaxPlayers">Maximum supported player count.</param>
/// <param name="AgeRatingAuthority">Age rating authority such as ESRB or PEGI.</param>
/// <param name="AgeRatingValue">Authority-specific age rating value.</param>
/// <param name="MinAgeYears">Minimum recommended player age.</param>
internal sealed record UpsertTitleMetadataCommand(
    string DisplayName,
    string ShortDescription,
    string Description,
    string GenreDisplay,
    int MinPlayers,
    int MaxPlayers,
    string AgeRatingAuthority,
    string AgeRatingValue,
    int MinAgeYears);

/// <summary>
/// Flattened title projection used by endpoint DTO mapping.
/// </summary>
/// <param name="Id">Title identifier.</param>
/// <param name="OrganizationId">Owning organization identifier.</param>
/// <param name="OrganizationSlug">Owning organization route key.</param>
/// <param name="Slug">Organization-scoped title route key.</param>
/// <param name="ContentKind">Stable title content kind.</param>
/// <param name="LifecycleStatus">Lifecycle status for the title.</param>
/// <param name="Visibility">Public discoverability mode.</param>
/// <param name="CurrentMetadataRevision">Currently active metadata revision number.</param>
/// <param name="DisplayName">Public display name.</param>
/// <param name="ShortDescription">Short public description.</param>
/// <param name="Description">Full public description when requested.</param>
/// <param name="GenreDisplay">Display-oriented genre text.</param>
/// <param name="MinPlayers">Minimum supported player count.</param>
/// <param name="MaxPlayers">Maximum supported player count.</param>
/// <param name="AgeRatingAuthority">Age rating authority such as ESRB or PEGI.</param>
/// <param name="AgeRatingValue">Authority-specific age rating value.</param>
/// <param name="MinAgeYears">Minimum recommended player age.</param>
/// <param name="CurrentReleaseId">Currently active release identifier when present.</param>
/// <param name="CardImageUrl">Card/list image URL when configured.</param>
/// <param name="MediaAssets">Configured title media assets.</param>
/// <param name="CurrentRelease">Currently active public release when present.</param>
/// <param name="CreatedAtUtc">UTC creation timestamp when requested.</param>
/// <param name="UpdatedAtUtc">UTC update timestamp when requested.</param>
internal sealed record TitleSnapshot(
    Guid Id,
    Guid OrganizationId,
    string OrganizationSlug,
    string Slug,
    string ContentKind,
    string LifecycleStatus,
    string Visibility,
    int CurrentMetadataRevision,
    string DisplayName,
    string ShortDescription,
    string? Description,
    string GenreDisplay,
    int MinPlayers,
    int MaxPlayers,
    string AgeRatingAuthority,
    string AgeRatingValue,
    int MinAgeYears,
    Guid? CurrentReleaseId,
    string? CardImageUrl,
    string? AcquisitionUrl,
    IReadOnlyList<TitleMediaAssetSnapshot> MediaAssets,
    CurrentTitleReleaseSnapshot? CurrentRelease,
    PublicTitleAcquisitionSnapshot? Acquisition,
    DateTime? CreatedAtUtc,
    DateTime? UpdatedAtUtc);

/// <summary>
/// Public title acquisition projection derived from the active primary binding.
/// </summary>
internal sealed record PublicTitleAcquisitionSnapshot(
    string Url,
    string? Label,
    string ProviderDisplayName,
    string? ProviderHomepageUrl);

/// <summary>
/// Projection of a title metadata revision for developer-facing responses.
/// </summary>
/// <param name="RevisionNumber">Per-title revision number.</param>
/// <param name="IsCurrent">Whether the revision is currently active.</param>
/// <param name="IsFrozen">Whether the revision is immutable.</param>
/// <param name="DisplayName">Public display name.</param>
/// <param name="ShortDescription">Short public description.</param>
/// <param name="Description">Full public description.</param>
/// <param name="GenreDisplay">Display-oriented genre text.</param>
/// <param name="MinPlayers">Minimum supported player count.</param>
/// <param name="MaxPlayers">Maximum supported player count.</param>
/// <param name="AgeRatingAuthority">Age rating authority such as ESRB or PEGI.</param>
/// <param name="AgeRatingValue">Authority-specific age rating value.</param>
/// <param name="MinAgeYears">Minimum recommended player age.</param>
/// <param name="CreatedAtUtc">UTC creation timestamp.</param>
/// <param name="UpdatedAtUtc">UTC update timestamp.</param>
internal sealed record TitleMetadataVersionSnapshot(
    int RevisionNumber,
    bool IsCurrent,
    bool IsFrozen,
    string DisplayName,
    string ShortDescription,
    string Description,
    string GenreDisplay,
    int MinPlayers,
    int MaxPlayers,
    string AgeRatingAuthority,
    string AgeRatingValue,
    int MinAgeYears,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// Result wrapper for title mutations and single-title lookups.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="Title">Returned title snapshot when available.</param>
internal sealed record TitleMutationResult(
    TitleMutationStatus Status,
    TitleSnapshot? Title = null,
    string? ErrorCode = null);

/// <summary>
/// Result wrapper for organization title listings.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="Titles">Returned title snapshots when available.</param>
internal sealed record TitleListResult(
    TitleListStatus Status,
    IReadOnlyList<TitleSnapshot>? Titles = null);

/// <summary>
/// Result wrapper for metadata revision listings.
/// </summary>
/// <param name="Status">Operation status.</param>
/// <param name="MetadataVersions">Returned metadata revisions when available.</param>
internal sealed record TitleMetadataVersionListResult(
    TitleMetadataVersionListStatus Status,
    IReadOnlyList<TitleMetadataVersionSnapshot>? MetadataVersions = null);

/// <summary>
/// Internal helper representing authorized title access resolution.
/// </summary>
/// <param name="Status">Access result status.</param>
/// <param name="Title">Resolved title when access succeeded.</param>
internal sealed record TitleAccessResult(
    TitleAccessStatus Status,
    Title? Title = null);

/// <summary>
/// Outcome codes for title create/update/get operations.
/// </summary>
internal enum TitleMutationStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict
}

/// <summary>
/// Outcome codes for organization title listings.
/// </summary>
internal enum TitleListStatus
{
    Success,
    NotFound,
    Forbidden
}

/// <summary>
/// Outcome codes for metadata revision listings.
/// </summary>
internal enum TitleMetadataVersionListStatus
{
    Success,
    NotFound,
    Forbidden
}

/// <summary>
/// Internal outcome codes for title access resolution.
/// </summary>
internal enum TitleAccessStatus
{
    Success,
    NotFound,
    Forbidden
}

/// <summary>
/// Known title lifecycle status codes.
/// </summary>
internal static class TitleLifecycleStatuses
{
    /// <summary>
    /// Draft title visible only to the developer team.
    /// </summary>
    public const string Draft = "draft";

    /// <summary>
    /// Testing title that may be public depending on visibility.
    /// </summary>
    public const string Testing = "testing";

    /// <summary>
    /// Officially published title.
    /// </summary>
    public const string Published = "published";

    /// <summary>
    /// Archived title retained for historical/management purposes.
    /// </summary>
    public const string Archived = "archived";
}

/// <summary>
/// Known title visibility codes.
/// </summary>
internal static class TitleVisibilities
{
    /// <summary>
    /// Not publicly reachable.
    /// </summary>
    public const string Private = "private";

    /// <summary>
    /// Publicly reachable by direct link but excluded from listings.
    /// </summary>
    public const string Unlisted = "unlisted";

    /// <summary>
    /// Publicly reachable and included in listings.
    /// </summary>
    public const string Listed = "listed";
}

/// <summary>
/// Known title content kind codes.
/// </summary>
internal static class TitleContentKinds
{
    /// <summary>
    /// Game title.
    /// </summary>
    public const string Game = "game";

    /// <summary>
    /// App title.
    /// </summary>
    public const string App = "app";
}
