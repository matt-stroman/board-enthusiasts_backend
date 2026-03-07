using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Auth;
using Board.ThirdPartyLibrary.Api.Identity;
using Board.ThirdPartyLibrary.Api.Studios;
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
    /// <param name="query">Catalog browse query and paging options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Paged catalog title summaries visible to the caller.</returns>
    Task<PublicCatalogListResult> ListPublicTitlesAsync(
        PublicCatalogListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a public catalog title by studio and title route key.
    /// </summary>
    /// <param name="studioSlug">Studio route key.</param>
    /// <param name="titleSlug">Title route key scoped to the studio.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The public catalog title when visible; otherwise <see langword="null" />.</returns>
    Task<TitleSnapshot?> GetPublicTitleAsync(
        string studioSlug,
        string titleSlug,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists titles for a studio when the caller has a managing membership role.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="studioId">Studio identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mutation-style result describing access and data.</returns>
    Task<TitleListResult> ListStudioTitlesAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new title and its initial metadata revision.
    /// </summary>
    /// <param name="claims">Authenticated caller claims.</param>
    /// <param name="studioId">Owning studio identifier.</param>
    /// <param name="command">Stable title fields and initial metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Mutation result describing the outcome.</returns>
    Task<TitleMutationResult> CreateTitleAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
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
    public async Task<PublicCatalogListResult> ListPublicTitlesAsync(
        PublicCatalogListQuery query,
        CancellationToken cancellationToken = default)
    {
        var titleQuery = dbContext.Titles
            .AsNoTracking()
            .Include(candidate => candidate.Studio)
            .Include(candidate => candidate.CurrentMetadataVersion)
            .Include(candidate => candidate.MediaAssets)
            .Include(candidate => candidate.Reports)
            .Include(candidate => candidate.IntegrationBindings)
                .ThenInclude(candidate => candidate.IntegrationConnection)
                    .ThenInclude(candidate => candidate.SupportedPublisher)
            .Where(candidate =>
                candidate.CurrentMetadataVersionId != null &&
                candidate.CurrentMetadataVersion != null &&
                candidate.Visibility == TitleVisibilities.Listed &&
                (candidate.LifecycleStatus == TitleLifecycleStatuses.Testing ||
                 candidate.LifecycleStatus == TitleLifecycleStatuses.Published));

        if (!string.IsNullOrWhiteSpace(query.StudioSlug))
        {
            titleQuery = titleQuery.Where(candidate => candidate.Studio.Slug == query.StudioSlug);
        }

        if (!string.IsNullOrWhiteSpace(query.ContentKind))
        {
            titleQuery = titleQuery.Where(candidate => candidate.ContentKind == query.ContentKind);
        }

        if (!string.IsNullOrWhiteSpace(query.Genre))
        {
            var normalizedGenre = query.Genre.Trim().ToLowerInvariant();
            titleQuery = titleQuery.Where(candidate => candidate.CurrentMetadataVersion!.GenreDisplay.ToLower() == normalizedGenre);
        }

        titleQuery = query.Sort switch
        {
            CatalogSortModes.Genre => titleQuery
                .OrderBy(candidate => candidate.CurrentMetadataVersion!.GenreDisplay)
                .ThenBy(candidate => candidate.CurrentMetadataVersion!.DisplayName)
                .ThenBy(candidate => candidate.Studio.DisplayName),
            _ => titleQuery
                .OrderBy(candidate => candidate.CurrentMetadataVersion!.DisplayName)
                .ThenBy(candidate => candidate.Studio.DisplayName)
                .ThenBy(candidate => candidate.CurrentMetadataVersion!.GenreDisplay)
        };

        var totalCount = await titleQuery.CountAsync(cancellationToken);
        var titles = await titleQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(candidate => TitleSnapshotMapper.MapTitle(candidate, includeDescription: false))
            .ToListAsync(cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)query.PageSize);
        return new PublicCatalogListResult(
            titles,
            new CatalogPageSnapshot(
                query.PageNumber,
                query.PageSize,
                totalCount,
                totalPages,
                query.PageNumber > 1 && totalPages > 0,
                query.PageNumber < totalPages));
    }

    public async Task<TitleSnapshot?> GetPublicTitleAsync(
        string studioSlug,
        string titleSlug,
        CancellationToken cancellationToken = default)
    {
        var title = await dbContext.Titles
            .AsNoTracking()
            .Include(candidate => candidate.Studio)
            .Include(candidate => candidate.CurrentMetadataVersion)
            .Include(candidate => candidate.MediaAssets)
            .Include(candidate => candidate.Reports)
            .Include(candidate => candidate.IntegrationBindings)
                .ThenInclude(candidate => candidate.IntegrationConnection)
                    .ThenInclude(candidate => candidate.SupportedPublisher)
            .Include(candidate => candidate.CurrentRelease)
                .ThenInclude(candidate => candidate!.MetadataVersion)
            .SingleOrDefaultAsync(
                candidate =>
                    candidate.Studio.Slug == studioSlug &&
                    candidate.Slug == titleSlug &&
                    candidate.CurrentMetadataVersionId != null &&
                    candidate.CurrentMetadataVersion != null &&
                    candidate.Visibility != TitleVisibilities.Private &&
                    (candidate.LifecycleStatus == TitleLifecycleStatuses.Testing ||
                     candidate.LifecycleStatus == TitleLifecycleStatuses.Published),
                cancellationToken);

        return title is null ? null : TitleSnapshotMapper.MapTitle(title, includeDescription: true);
    }

    public async Task<TitleListResult> ListStudioTitlesAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var studioExists = await dbContext.Studios
            .AsNoTracking()
            .AnyAsync(candidate => candidate.Id == studioId, cancellationToken);

        if (!studioExists)
        {
            return new TitleListResult(TitleListStatus.NotFound);
        }

        var actorRole = await GetActorStudioRoleAsync(studioId, actor.Id, cancellationToken);
        if (!CanManageTitles(actorRole))
        {
            return new TitleListResult(TitleListStatus.Forbidden);
        }

        var titles = await dbContext.Titles
            .AsNoTracking()
            .Include(candidate => candidate.Studio)
            .Include(candidate => candidate.CurrentMetadataVersion)
            .Include(candidate => candidate.MediaAssets)
            .Include(candidate => candidate.IntegrationBindings)
                .ThenInclude(candidate => candidate.IntegrationConnection)
                    .ThenInclude(candidate => candidate.SupportedPublisher)
            .Where(candidate => candidate.StudioId == studioId && candidate.CurrentMetadataVersionId != null)
            .OrderBy(candidate => candidate.CurrentMetadataVersion!.DisplayName)
            .Select(candidate => TitleSnapshotMapper.MapTitle(candidate, includeDescription: false))
            .ToListAsync(cancellationToken);

        return new TitleListResult(TitleListStatus.Success, titles);
    }

    public async Task<TitleMutationResult> CreateTitleAsync(
        IEnumerable<Claim> claims,
        Guid studioId,
        CreateTitleCommand command,
        CancellationToken cancellationToken = default)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var studio = await dbContext.Studios
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == studioId, cancellationToken);

        if (studio is null)
        {
            return new TitleMutationResult(TitleMutationStatus.NotFound);
        }

        var actorRole = await GetActorStudioRoleAsync(studioId, actor.Id, cancellationToken);
        if (!CanManageTitles(actorRole))
        {
            return new TitleMutationResult(TitleMutationStatus.Forbidden);
        }

        var now = DateTime.UtcNow;
        var title = new Title
        {
            Id = Guid.NewGuid(),
            StudioId = studioId,
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
            TitleSnapshotMapper.MapTitle(title, studio, metadataVersion, includeDescription: true));
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
                TitleSnapshotMapper.MapTitle(title.Title!, includeDescription: true)),
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
            return new TitleMutationResult(TitleMutationStatus.Success, TitleSnapshotMapper.MapTitle(title, includeDescription: true));
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
            return new TitleMutationResult(TitleMutationStatus.Success, TitleSnapshotMapper.MapTitle(title, includeDescription: true));
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

        return new TitleMutationResult(TitleMutationStatus.Success, TitleSnapshotMapper.MapTitle(title, includeDescription: true));
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
        return new TitleMutationResult(TitleMutationStatus.Success, TitleSnapshotMapper.MapTitle(title, includeDescription: true));
    }

    private async Task<TitleAccessResult> LoadManagedTitleAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken)
    {
        var actor = await EnsureActorAsync(claims, cancellationToken);
        var title = await dbContext.Titles
            .Include(candidate => candidate.Studio)
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

        var actorRole = await GetActorStudioRoleAsync(title.StudioId, actor.Id, cancellationToken);
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

    private async Task<string?> GetActorStudioRoleAsync(Guid studioId, Guid actorUserId, CancellationToken cancellationToken) =>
        await dbContext.StudioMemberships
            .Where(candidate => candidate.StudioId == studioId && candidate.UserId == actorUserId)
            .Select(candidate => candidate.Role)
            .SingleOrDefaultAsync(cancellationToken);

    private static bool CanManageTitles(string? role) =>
        string.Equals(role, StudioRoles.Owner, StringComparison.Ordinal) ||
        string.Equals(role, StudioRoles.Admin, StringComparison.Ordinal) ||
        string.Equals(role, StudioRoles.Editor, StringComparison.Ordinal);

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

    private static string GetRequiredSubject(IEnumerable<Claim> claims)
    {
        var subject = ClaimValueResolver.GetSubject(claims);

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
/// <param name="Slug">Studio-scoped title route key.</param>
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
/// <param name="Slug">Studio-scoped title route key.</param>
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
/// Query options for public catalog browse requests.
/// </summary>
/// <param name="StudioSlug">Optional studio route key filter.</param>
/// <param name="ContentKind">Optional content kind filter.</param>
/// <param name="Genre">Optional exact genre display filter.</param>
/// <param name="Sort">Stable sort mode.</param>
/// <param name="PageNumber">1-based page number.</param>
/// <param name="PageSize">Requested page size.</param>
internal sealed record PublicCatalogListQuery(
    string? StudioSlug,
    string? ContentKind,
    string? Genre,
    string Sort,
    int PageNumber,
    int PageSize);

/// <summary>
/// Flattened title projection used by endpoint DTO mapping.
/// </summary>
/// <param name="Id">Title identifier.</param>
/// <param name="StudioId">Owning studio identifier.</param>
/// <param name="StudioSlug">Owning studio route key.</param>
/// <param name="Slug">Studio-scoped title route key.</param>
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
/// <param name="IsReported">Whether the title currently has an active moderation report.</param>
/// <param name="CurrentReleaseId">Currently active release identifier when present.</param>
/// <param name="CardImageUrl">Card/list image URL when configured.</param>
/// <param name="AcquisitionUrl">Primary acquisition URL when an active binding exists.</param>
/// <param name="MediaAssets">Configured title media assets.</param>
/// <param name="CurrentRelease">Currently active public release when present.</param>
/// <param name="Acquisition">Detailed public acquisition summary when present.</param>
/// <param name="CreatedAtUtc">UTC creation timestamp when requested.</param>
/// <param name="UpdatedAtUtc">UTC update timestamp when requested.</param>
internal sealed record TitleSnapshot(
    Guid Id,
    Guid StudioId,
    string StudioSlug,
    string Slug,
    string ContentKind,
    string LifecycleStatus,
    string Visibility,
    bool IsReported,
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
/// Paging metadata returned alongside a public catalog browse result.
/// </summary>
/// <param name="PageNumber">1-based page number.</param>
/// <param name="PageSize">Requested page size.</param>
/// <param name="TotalCount">Total matched titles across all pages.</param>
/// <param name="TotalPages">Total available pages for the current page size.</param>
/// <param name="HasPreviousPage">Whether a previous page exists.</param>
/// <param name="HasNextPage">Whether a next page exists.</param>
internal sealed record CatalogPageSnapshot(
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);

/// <summary>
/// Paged result for public catalog browsing.
/// </summary>
/// <param name="Titles">Titles on the requested page.</param>
/// <param name="Paging">Paging metadata for the overall result set.</param>
internal sealed record PublicCatalogListResult(
    IReadOnlyList<TitleSnapshot> Titles,
    CatalogPageSnapshot Paging);

/// <summary>
/// Public title acquisition projection derived from the active primary binding.
/// </summary>
/// <param name="Url">External acquisition URL.</param>
/// <param name="Label">Optional player-facing acquisition label.</param>
/// <param name="ProviderDisplayName">Provider name shown to players.</param>
/// <param name="ProviderHomepageUrl">Canonical provider homepage URL when known.</param>
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
/// <param name="ErrorCode">Optional machine-readable conflict code.</param>
internal sealed record TitleMutationResult(
    TitleMutationStatus Status,
    TitleSnapshot? Title = null,
    string? ErrorCode = null);

/// <summary>
/// Result wrapper for studio title listings.
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
/// Outcome codes for studio title listings.
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
/// Supported sort modes for public catalog browsing.
/// </summary>
internal static class CatalogSortModes
{
    /// <summary>
    /// Sort by public display title.
    /// </summary>
    public const string Title = "title";

    /// <summary>
    /// Sort by genre, then title.
    /// </summary>
    public const string Genre = "genre";
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
