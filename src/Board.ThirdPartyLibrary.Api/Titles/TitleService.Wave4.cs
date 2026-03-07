using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Board.ThirdPartyLibrary.Api.Titles;

internal sealed partial class TitleService
{
    public async Task<TitleMediaAssetListResult> ListMediaAssetsAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleMediaAssetListResult(TitleResourceListStatus.Forbidden)
                : new TitleMediaAssetListResult(TitleResourceListStatus.NotFound);
        }

        var mediaAssets = await dbContext.TitleMediaAssets
            .AsNoTracking()
            .Where(candidate => candidate.TitleId == titleId)
            .OrderBy(candidate => candidate.MediaRole)
            .ToListAsync(cancellationToken);

        return new TitleMediaAssetListResult(
            TitleResourceListStatus.Success,
            mediaAssets.Select(MapMediaAsset).ToList());
    }

    public async Task<TitleMediaAssetMutationResult> UpsertMediaAssetAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        string mediaRole,
        UpsertTitleMediaAssetCommand command,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleMediaAssetMutationResult(TitleResourceMutationStatus.Forbidden)
                : new TitleMediaAssetMutationResult(TitleResourceMutationStatus.NotFound);
        }

        var title = access.Title!;
        var now = DateTime.UtcNow;
        var existingAsset = await dbContext.TitleMediaAssets
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.MediaRole == mediaRole,
                cancellationToken);

        if (existingAsset is null)
        {
            existingAsset = new TitleMediaAsset
            {
                Id = Guid.NewGuid(),
                TitleId = titleId,
                MediaRole = mediaRole,
                CreatedAtUtc = now
            };

            dbContext.TitleMediaAssets.Add(existingAsset);
        }

        existingAsset.SourceUrl = command.SourceUrl;
        existingAsset.AltText = command.AltText;
        existingAsset.MimeType = command.MimeType;
        existingAsset.Width = command.Width;
        existingAsset.Height = command.Height;
        existingAsset.UpdatedAtUtc = now;

        title.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new TitleMediaAssetMutationResult(TitleResourceMutationStatus.Success, MapMediaAsset(existingAsset));
    }

    public async Task<TitleMediaAssetMutationResult> DeleteMediaAssetAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        string mediaRole,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleMediaAssetMutationResult(TitleResourceMutationStatus.Forbidden)
                : new TitleMediaAssetMutationResult(TitleResourceMutationStatus.NotFound);
        }

        var mediaAsset = await dbContext.TitleMediaAssets
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.MediaRole == mediaRole,
                cancellationToken);

        if (mediaAsset is null)
        {
            return new TitleMediaAssetMutationResult(TitleResourceMutationStatus.NotFound);
        }

        dbContext.TitleMediaAssets.Remove(mediaAsset);
        access.Title!.UpdatedAtUtc = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TitleMediaAssetMutationResult(TitleResourceMutationStatus.Success);
    }

    public async Task<TitleReleaseListResult> ListReleasesAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleReleaseListResult(TitleResourceListStatus.Forbidden)
                : new TitleReleaseListResult(TitleResourceListStatus.NotFound);
        }

        var title = access.Title!;
        var releases = await dbContext.TitleReleases
            .AsNoTracking()
            .Include(candidate => candidate.MetadataVersion)
            .Where(candidate => candidate.TitleId == titleId)
            .OrderByDescending(candidate => candidate.PublishedAtUtc)
            .ThenByDescending(candidate => candidate.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return new TitleReleaseListResult(
            TitleResourceListStatus.Success,
            releases.Select(candidate => MapRelease(candidate, title.CurrentReleaseId == candidate.Id)).ToList());
    }

    public async Task<TitleReleaseMutationResult> GetReleaseAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleReleaseMutationResult(TitleResourceMutationStatus.Forbidden)
                : new TitleReleaseMutationResult(TitleResourceMutationStatus.NotFound);
        }

        var release = await dbContext.TitleReleases
            .AsNoTracking()
            .Include(candidate => candidate.MetadataVersion)
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.Id == releaseId,
                cancellationToken);

        if (release is null)
        {
            return new TitleReleaseMutationResult(TitleResourceMutationStatus.NotFound);
        }

        return new TitleReleaseMutationResult(
            TitleResourceMutationStatus.Success,
            MapRelease(release, access.Title!.CurrentReleaseId == release.Id));
    }

    public async Task<TitleReleaseMutationResult> CreateReleaseAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        CreateTitleReleaseCommand command,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleReleaseMutationResult(TitleResourceMutationStatus.Forbidden)
                : new TitleReleaseMutationResult(TitleResourceMutationStatus.NotFound);
        }

        var metadataVersion = await dbContext.TitleMetadataVersions
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.RevisionNumber == command.MetadataRevisionNumber,
                cancellationToken);

        if (metadataVersion is null)
        {
            return new TitleReleaseMutationResult(TitleResourceMutationStatus.NotFound);
        }

        var now = DateTime.UtcNow;
        var release = new TitleRelease
        {
            Id = Guid.NewGuid(),
            TitleId = titleId,
            MetadataVersionId = metadataVersion.Id,
            MetadataVersion = metadataVersion,
            Version = command.Version,
            Status = TitleReleaseStatuses.Draft,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.TitleReleases.Add(release);
        access.Title!.UpdatedAtUtc = now;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new TitleReleaseMutationResult(
                TitleResourceMutationStatus.Conflict,
                ErrorCode: TitleResourceErrorCodes.TitleReleaseVersionConflict);
        }

        return new TitleReleaseMutationResult(
            TitleResourceMutationStatus.Success,
            MapRelease(release, isCurrent: false));
    }

    public async Task<TitleReleaseMutationResult> UpdateReleaseAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        UpdateTitleReleaseCommand command,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleReleaseMutationResult(TitleResourceMutationStatus.Forbidden)
                : new TitleReleaseMutationResult(TitleResourceMutationStatus.NotFound);
        }

        var release = await dbContext.TitleReleases
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.Id == releaseId,
                cancellationToken);

        if (release is null)
        {
            return new TitleReleaseMutationResult(TitleResourceMutationStatus.NotFound);
        }

        if (release.Status != TitleReleaseStatuses.Draft)
        {
            return new TitleReleaseMutationResult(
                TitleResourceMutationStatus.Conflict,
                ErrorCode: TitleResourceErrorCodes.TitleReleaseStateConflict);
        }

        var metadataVersion = await dbContext.TitleMetadataVersions
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.RevisionNumber == command.MetadataRevisionNumber,
                cancellationToken);

        if (metadataVersion is null)
        {
            return new TitleReleaseMutationResult(TitleResourceMutationStatus.NotFound);
        }

        release.Version = command.Version;
        release.MetadataVersionId = metadataVersion.Id;
        release.MetadataVersion = metadataVersion;
        release.UpdatedAtUtc = DateTime.UtcNow;
        access.Title!.UpdatedAtUtc = release.UpdatedAtUtc;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new TitleReleaseMutationResult(
                TitleResourceMutationStatus.Conflict,
                ErrorCode: TitleResourceErrorCodes.TitleReleaseVersionConflict);
        }

        return new TitleReleaseMutationResult(
            TitleResourceMutationStatus.Success,
            MapRelease(release, access.Title.CurrentReleaseId == release.Id));
    }

    public async Task<TitleReleaseMutationResult> PublishReleaseAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleReleaseMutationResult(TitleResourceMutationStatus.Forbidden)
                : new TitleReleaseMutationResult(TitleResourceMutationStatus.NotFound);
        }

        var release = await dbContext.TitleReleases
            .Include(candidate => candidate.MetadataVersion)
            .Include(candidate => candidate.Artifacts)
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.Id == releaseId,
                cancellationToken);

        if (release is null)
        {
            return new TitleReleaseMutationResult(TitleResourceMutationStatus.NotFound);
        }

        if (release.Status != TitleReleaseStatuses.Draft)
        {
            return new TitleReleaseMutationResult(
                TitleResourceMutationStatus.Conflict,
                ErrorCode: TitleResourceErrorCodes.TitleReleaseStateConflict);
        }

        if (release.Artifacts.Count == 0)
        {
            return new TitleReleaseMutationResult(
                TitleResourceMutationStatus.Conflict,
                ErrorCode: TitleResourceErrorCodes.TitleReleasePublishRequiresArtifact);
        }

        var now = DateTime.UtcNow;
        release.Status = TitleReleaseStatuses.Published;
        release.PublishedAtUtc = now;
        release.UpdatedAtUtc = now;
        access.Title!.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new TitleReleaseMutationResult(
            TitleResourceMutationStatus.Success,
            MapRelease(release, access.Title.CurrentReleaseId == release.Id));
    }

    public async Task<TitleMutationResult> ActivateReleaseAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
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
        var release = await dbContext.TitleReleases
            .Include(candidate => candidate.MetadataVersion)
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.Id == releaseId,
                cancellationToken);

        if (release is null)
        {
            return new TitleMutationResult(TitleMutationStatus.NotFound);
        }

        if (release.Status != TitleReleaseStatuses.Published)
        {
            return new TitleMutationResult(
                TitleMutationStatus.Conflict,
                ErrorCode: TitleResourceErrorCodes.TitleReleaseStateConflict);
        }

        var now = DateTime.UtcNow;
        title.CurrentReleaseId = release.Id;
        title.CurrentRelease = release;
        title.UpdatedAtUtc = now;
        release.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new TitleMutationResult(TitleMutationStatus.Success, TitleSnapshotMapper.MapTitle(title, includeDescription: true));
    }

    public async Task<TitleReleaseMutationResult> WithdrawReleaseAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new TitleReleaseMutationResult(TitleResourceMutationStatus.Forbidden)
                : new TitleReleaseMutationResult(TitleResourceMutationStatus.NotFound);
        }

        var title = access.Title!;
        var release = await dbContext.TitleReleases
            .Include(candidate => candidate.MetadataVersion)
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.Id == releaseId,
                cancellationToken);

        if (release is null)
        {
            return new TitleReleaseMutationResult(TitleResourceMutationStatus.NotFound);
        }

        if (release.Status == TitleReleaseStatuses.Withdrawn)
        {
            return new TitleReleaseMutationResult(
                TitleResourceMutationStatus.Success,
                MapRelease(release, isCurrent: false));
        }

        if (release.Status != TitleReleaseStatuses.Published)
        {
            return new TitleReleaseMutationResult(
                TitleResourceMutationStatus.Conflict,
                ErrorCode: TitleResourceErrorCodes.TitleReleaseStateConflict);
        }

        var now = DateTime.UtcNow;
        release.Status = TitleReleaseStatuses.Withdrawn;
        release.UpdatedAtUtc = now;
        title.UpdatedAtUtc = now;

        if (title.CurrentReleaseId == release.Id)
        {
            title.CurrentReleaseId = null;
            title.CurrentRelease = null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return new TitleReleaseMutationResult(
            TitleResourceMutationStatus.Success,
            MapRelease(release, isCurrent: false));
    }

    public async Task<ReleaseArtifactListResult> ListReleaseArtifactsAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new ReleaseArtifactListResult(TitleResourceListStatus.Forbidden)
                : new ReleaseArtifactListResult(TitleResourceListStatus.NotFound);
        }

        var releaseExists = await dbContext.TitleReleases
            .AsNoTracking()
            .AnyAsync(candidate => candidate.TitleId == titleId && candidate.Id == releaseId, cancellationToken);

        if (!releaseExists)
        {
            return new ReleaseArtifactListResult(TitleResourceListStatus.NotFound);
        }

        var artifacts = await dbContext.ReleaseArtifacts
            .AsNoTracking()
            .Where(candidate => candidate.ReleaseId == releaseId)
            .OrderBy(candidate => candidate.PackageName)
            .ThenBy(candidate => candidate.VersionCode)
            .ToListAsync(cancellationToken);

        return new ReleaseArtifactListResult(
            TitleResourceListStatus.Success,
            artifacts.Select(MapArtifact).ToList());
    }

    public async Task<ReleaseArtifactMutationResult> CreateReleaseArtifactAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        UpsertReleaseArtifactCommand command,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new ReleaseArtifactMutationResult(TitleResourceMutationStatus.Forbidden)
                : new ReleaseArtifactMutationResult(TitleResourceMutationStatus.NotFound);
        }

        var release = await dbContext.TitleReleases
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.Id == releaseId,
                cancellationToken);

        if (release is null)
        {
            return new ReleaseArtifactMutationResult(TitleResourceMutationStatus.NotFound);
        }

        if (release.Status != TitleReleaseStatuses.Draft)
        {
            return new ReleaseArtifactMutationResult(
                TitleResourceMutationStatus.Conflict,
                ErrorCode: TitleResourceErrorCodes.TitleReleaseStateConflict);
        }

        var now = DateTime.UtcNow;
        var artifact = new ReleaseArtifact
        {
            Id = Guid.NewGuid(),
            ReleaseId = releaseId,
            ArtifactKind = command.ArtifactKind,
            PackageName = command.PackageName,
            VersionCode = command.VersionCode,
            Sha256 = command.Sha256,
            FileSizeBytes = command.FileSizeBytes,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.ReleaseArtifacts.Add(artifact);
        release.UpdatedAtUtc = now;
        access.Title!.UpdatedAtUtc = now;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new ReleaseArtifactMutationResult(
                TitleResourceMutationStatus.Conflict,
                ErrorCode: TitleResourceErrorCodes.ReleaseArtifactIdentityConflict);
        }

        return new ReleaseArtifactMutationResult(
            TitleResourceMutationStatus.Success,
            MapArtifact(artifact));
    }

    public async Task<ReleaseArtifactMutationResult> UpdateReleaseArtifactAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        Guid artifactId,
        UpsertReleaseArtifactCommand command,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new ReleaseArtifactMutationResult(TitleResourceMutationStatus.Forbidden)
                : new ReleaseArtifactMutationResult(TitleResourceMutationStatus.NotFound);
        }

        var release = await dbContext.TitleReleases
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.Id == releaseId,
                cancellationToken);

        if (release is null)
        {
            return new ReleaseArtifactMutationResult(TitleResourceMutationStatus.NotFound);
        }

        if (release.Status != TitleReleaseStatuses.Draft)
        {
            return new ReleaseArtifactMutationResult(
                TitleResourceMutationStatus.Conflict,
                ErrorCode: TitleResourceErrorCodes.TitleReleaseStateConflict);
        }

        var artifact = await dbContext.ReleaseArtifacts
            .SingleOrDefaultAsync(
                candidate => candidate.ReleaseId == releaseId && candidate.Id == artifactId,
                cancellationToken);

        if (artifact is null)
        {
            return new ReleaseArtifactMutationResult(TitleResourceMutationStatus.NotFound);
        }

        artifact.ArtifactKind = command.ArtifactKind;
        artifact.PackageName = command.PackageName;
        artifact.VersionCode = command.VersionCode;
        artifact.Sha256 = command.Sha256;
        artifact.FileSizeBytes = command.FileSizeBytes;
        artifact.UpdatedAtUtc = DateTime.UtcNow;
        release.UpdatedAtUtc = artifact.UpdatedAtUtc;
        access.Title!.UpdatedAtUtc = artifact.UpdatedAtUtc;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new ReleaseArtifactMutationResult(
                TitleResourceMutationStatus.Conflict,
                ErrorCode: TitleResourceErrorCodes.ReleaseArtifactIdentityConflict);
        }

        return new ReleaseArtifactMutationResult(
            TitleResourceMutationStatus.Success,
            MapArtifact(artifact));
    }

    public async Task<ReleaseArtifactMutationResult> DeleteReleaseArtifactAsync(
        IEnumerable<Claim> claims,
        Guid titleId,
        Guid releaseId,
        Guid artifactId,
        CancellationToken cancellationToken = default)
    {
        var access = await LoadManagedTitleAsync(claims, titleId, cancellationToken);
        if (access.Status is not TitleAccessStatus.Success)
        {
            return access.Status == TitleAccessStatus.Forbidden
                ? new ReleaseArtifactMutationResult(TitleResourceMutationStatus.Forbidden)
                : new ReleaseArtifactMutationResult(TitleResourceMutationStatus.NotFound);
        }

        var release = await dbContext.TitleReleases
            .SingleOrDefaultAsync(
                candidate => candidate.TitleId == titleId && candidate.Id == releaseId,
                cancellationToken);

        if (release is null)
        {
            return new ReleaseArtifactMutationResult(TitleResourceMutationStatus.NotFound);
        }

        if (release.Status != TitleReleaseStatuses.Draft)
        {
            return new ReleaseArtifactMutationResult(
                TitleResourceMutationStatus.Conflict,
                ErrorCode: TitleResourceErrorCodes.TitleReleaseStateConflict);
        }

        var artifact = await dbContext.ReleaseArtifacts
            .SingleOrDefaultAsync(
                candidate => candidate.ReleaseId == releaseId && candidate.Id == artifactId,
                cancellationToken);

        if (artifact is null)
        {
            return new ReleaseArtifactMutationResult(TitleResourceMutationStatus.NotFound);
        }

        dbContext.ReleaseArtifacts.Remove(artifact);
        var now = DateTime.UtcNow;
        release.UpdatedAtUtc = now;
        access.Title!.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new ReleaseArtifactMutationResult(TitleResourceMutationStatus.Success);
    }

    private static TitleMediaAssetSnapshot MapMediaAsset(TitleMediaAsset mediaAsset) =>
        new(
            mediaAsset.Id,
            mediaAsset.MediaRole,
            mediaAsset.SourceUrl,
            mediaAsset.AltText,
            mediaAsset.MimeType,
            mediaAsset.Width,
            mediaAsset.Height,
            mediaAsset.CreatedAtUtc,
            mediaAsset.UpdatedAtUtc);

    private static CurrentTitleReleaseSnapshot? MapCurrentRelease(TitleRelease? release) =>
        release is null || release.PublishedAtUtc is null
            ? null
            : new CurrentTitleReleaseSnapshot(
                release.Id,
                release.Version,
                release.MetadataVersion.RevisionNumber,
                release.PublishedAtUtc.Value);

    private static TitleReleaseSnapshot MapRelease(TitleRelease release, bool isCurrent) =>
        new(
            release.Id,
            release.Version,
            release.Status,
            release.MetadataVersion.RevisionNumber,
            isCurrent,
            release.PublishedAtUtc,
            release.CreatedAtUtc,
            release.UpdatedAtUtc);

    private static ReleaseArtifactSnapshot MapArtifact(ReleaseArtifact artifact) =>
        new(
            artifact.Id,
            artifact.ArtifactKind,
            artifact.PackageName,
            artifact.VersionCode,
            artifact.Sha256,
            artifact.FileSizeBytes,
            artifact.CreatedAtUtc,
            artifact.UpdatedAtUtc);
}

/// <summary>
/// Command payload for creating or updating a title media asset.
/// </summary>
internal sealed record UpsertTitleMediaAssetCommand(
    string SourceUrl,
    string? AltText,
    string? MimeType,
    int? Width,
    int? Height);

/// <summary>
/// Command payload for creating a title release.
/// </summary>
internal sealed record CreateTitleReleaseCommand(
    string Version,
    int MetadataRevisionNumber);

/// <summary>
/// Command payload for updating a title release.
/// </summary>
internal sealed record UpdateTitleReleaseCommand(
    string Version,
    int MetadataRevisionNumber);

/// <summary>
/// Command payload for creating or updating a release artifact.
/// </summary>
internal sealed record UpsertReleaseArtifactCommand(
    string ArtifactKind,
    string PackageName,
    long VersionCode,
    string? Sha256,
    long? FileSizeBytes);

/// <summary>
/// Projection of a title media asset.
/// </summary>
internal sealed record TitleMediaAssetSnapshot(
    Guid Id,
    string MediaRole,
    string SourceUrl,
    string? AltText,
    string? MimeType,
    int? Width,
    int? Height,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// Projection of the currently active public release for a title.
/// </summary>
internal sealed record CurrentTitleReleaseSnapshot(
    Guid Id,
    string Version,
    int MetadataRevisionNumber,
    DateTime PublishedAtUtc);

/// <summary>
/// Projection of a title release.
/// </summary>
internal sealed record TitleReleaseSnapshot(
    Guid Id,
    string Version,
    string Status,
    int MetadataRevisionNumber,
    bool IsCurrent,
    DateTime? PublishedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// Projection of a release artifact.
/// </summary>
internal sealed record ReleaseArtifactSnapshot(
    Guid Id,
    string ArtifactKind,
    string PackageName,
    long VersionCode,
    string? Sha256,
    long? FileSizeBytes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

/// <summary>
/// Result wrapper for title media asset lists.
/// </summary>
internal sealed record TitleMediaAssetListResult(
    TitleResourceListStatus Status,
    IReadOnlyList<TitleMediaAssetSnapshot>? MediaAssets = null);

/// <summary>
/// Result wrapper for title media asset mutations.
/// </summary>
internal sealed record TitleMediaAssetMutationResult(
    TitleResourceMutationStatus Status,
    TitleMediaAssetSnapshot? MediaAsset = null);

/// <summary>
/// Result wrapper for title release lists.
/// </summary>
internal sealed record TitleReleaseListResult(
    TitleResourceListStatus Status,
    IReadOnlyList<TitleReleaseSnapshot>? Releases = null);

/// <summary>
/// Result wrapper for title release mutations.
/// </summary>
internal sealed record TitleReleaseMutationResult(
    TitleResourceMutationStatus Status,
    TitleReleaseSnapshot? Release = null,
    string? ErrorCode = null);

/// <summary>
/// Result wrapper for release artifact lists.
/// </summary>
internal sealed record ReleaseArtifactListResult(
    TitleResourceListStatus Status,
    IReadOnlyList<ReleaseArtifactSnapshot>? Artifacts = null);

/// <summary>
/// Result wrapper for release artifact mutations.
/// </summary>
internal sealed record ReleaseArtifactMutationResult(
    TitleResourceMutationStatus Status,
    ReleaseArtifactSnapshot? Artifact = null,
    string? ErrorCode = null);

/// <summary>
/// Shared outcome codes for Wave 4 title resources.
/// </summary>
internal enum TitleResourceMutationStatus
{
    Success,
    NotFound,
    Forbidden,
    Conflict
}

/// <summary>
/// Shared list outcome codes for Wave 4 title resources.
/// </summary>
internal enum TitleResourceListStatus
{
    Success,
    NotFound,
    Forbidden
}

/// <summary>
/// Known title media roles.
/// </summary>
internal static class TitleMediaRoles
{
    public const string Card = "card";
    public const string Hero = "hero";
    public const string Logo = "logo";
}

/// <summary>
/// Known title release statuses.
/// </summary>
internal static class TitleReleaseStatuses
{
    public const string Draft = "draft";
    public const string Published = "published";
    public const string Withdrawn = "withdrawn";
}

/// <summary>
/// Known release artifact kinds.
/// </summary>
internal static class ReleaseArtifactKinds
{
    public const string Apk = "apk";
}

/// <summary>
/// Known error codes for Wave 4 title resources.
/// </summary>
internal static class TitleResourceErrorCodes
{
    public const string TitleReleaseVersionConflict = "title_release_version_conflict";
    public const string TitleReleaseStateConflict = "title_release_state_conflict";
    public const string TitleReleasePublishRequiresArtifact = "title_release_publish_requires_artifact";
    public const string ReleaseArtifactIdentityConflict = "release_artifact_identity_conflict";
}
