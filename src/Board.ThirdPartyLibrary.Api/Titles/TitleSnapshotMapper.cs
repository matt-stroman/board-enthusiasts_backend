using Board.ThirdPartyLibrary.Api.Persistence.Entities;
using Board.ThirdPartyLibrary.Api.Players;

namespace Board.ThirdPartyLibrary.Api.Titles;

/// <summary>
/// Shared mapping helpers for title read models exposed across multiple endpoint areas.
/// </summary>
internal static class TitleSnapshotMapper
{
    /// <summary>
    /// Maps a title entity to a flattened snapshot.
    /// </summary>
    /// <param name="title">Title entity.</param>
    /// <param name="includeDescription">Whether to include full description and timestamps.</param>
    /// <returns>Flattened title snapshot.</returns>
    public static TitleSnapshot MapTitle(Title title, bool includeDescription) =>
        MapTitle(title, title.Studio, title.CurrentMetadataVersion!, includeDescription);

    /// <summary>
    /// Maps a title entity using explicit studio and metadata instances.
    /// </summary>
    /// <param name="title">Title entity.</param>
    /// <param name="studio">Owning studio.</param>
    /// <param name="metadataVersion">Current metadata version.</param>
    /// <param name="includeDescription">Whether to include full description and timestamps.</param>
    /// <returns>Flattened title snapshot.</returns>
    public static TitleSnapshot MapTitle(
        Title title,
        Studio studio,
        TitleMetadataVersion metadataVersion,
        bool includeDescription)
    {
        var acquisitionBinding = title.IntegrationBindings
            .Where(candidate => candidate.IsEnabled && candidate.IsPrimary && candidate.IntegrationConnection.IsEnabled)
            .OrderBy(candidate => candidate.CreatedAtUtc)
            .FirstOrDefault();

        return new TitleSnapshot(
            title.Id,
            title.StudioId,
            studio.Slug,
            title.Slug,
            title.ContentKind,
            title.LifecycleStatus,
            title.Visibility,
            title.Reports.Any(candidate => candidate.Status != TitleReportStatuses.Invalidated),
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

    private static TitleMediaAssetSnapshot MapMediaAsset(TitleMediaAsset asset) =>
        new(
            asset.Id,
            asset.MediaRole,
            asset.SourceUrl,
            asset.AltText,
            asset.MimeType,
            asset.Width,
            asset.Height,
            asset.CreatedAtUtc,
            asset.UpdatedAtUtc);

    private static CurrentTitleReleaseSnapshot? MapCurrentRelease(TitleRelease? release) =>
        release is null
            ? null
            : new CurrentTitleReleaseSnapshot(
                release.Id,
                release.Version,
                release.MetadataVersion.RevisionNumber,
                release.PublishedAtUtc ?? release.UpdatedAtUtc);
}
