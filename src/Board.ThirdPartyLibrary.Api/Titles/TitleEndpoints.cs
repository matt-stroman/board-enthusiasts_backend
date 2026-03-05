using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;

namespace Board.ThirdPartyLibrary.Api.Titles;

/// <summary>
/// Maps title and catalog endpoints.
/// </summary>
internal static partial class TitleEndpoints
{
    private static readonly Regex SlugRegex = SlugPattern();
    private const int DefaultCatalogPageSize = 12;
    private const int MaxCatalogPageSize = 48;

    /// <summary>
    /// Maps title and catalog endpoints to the application.
    /// </summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapTitleEndpoints(this IEndpointRouteBuilder app)
    {
        var catalogGroup = app.MapGroup("/catalog");
        var developerOrganizationGroup = app.MapGroup("/developer/organizations");
        var developerTitleGroup = app.MapGroup("/developer/titles");

        catalogGroup.MapGet("/", async (
            string? organizationSlug,
            string? contentKind,
            string? genre,
            string? sort,
            int? pageNumber,
            int? pageSize,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateCatalogBrowseRequest(contentKind, sort, pageNumber, pageSize);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await titleService.ListPublicTitlesAsync(
                new PublicCatalogListQuery(
                    organizationSlug?.Trim().ToLowerInvariant(),
                    NormalizeCode(contentKind),
                    genre?.Trim(),
                    NormalizeCode(sort) ?? CatalogSortModes.Title,
                    pageNumber ?? 1,
                    pageSize ?? DefaultCatalogPageSize),
                cancellationToken);

            return Results.Ok(
                new CatalogTitleListResponse(
                    result.Titles.Select(MapTitleSummary).ToArray(),
                    MapCatalogPage(result.Paging)));
        });

        catalogGroup.MapGet("/{organizationSlug}/{titleSlug}", async (
            string organizationSlug,
            string titleSlug,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var title = await titleService.GetPublicTitleAsync(
                organizationSlug.Trim().ToLowerInvariant(),
                titleSlug.Trim().ToLowerInvariant(),
                cancellationToken);

            return title is null
                ? Results.NotFound()
                : Results.Ok(new CatalogTitleResponse(MapTitleDetail(title)));
        });

        developerOrganizationGroup.MapGet("/{organizationId:guid}/titles", [Authorize] async (
            ClaimsPrincipal user,
            Guid organizationId,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleService.ListOrganizationTitlesAsync(user.Claims, organizationId, cancellationToken);
            return result.Status switch
            {
                TitleListStatus.Success => Results.Ok(new DeveloperTitleListResponse(result.Titles!.Select(MapTitleSummary).ToArray())),
                TitleListStatus.NotFound => Results.NotFound(),
                TitleListStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerOrganizationGroup.MapPost("/{organizationId:guid}/titles", [Authorize] async (
            ClaimsPrincipal user,
            Guid organizationId,
            CreateTitleRequest request,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateCreateTitleRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await titleService.CreateTitleAsync(
                user.Claims,
                organizationId,
                new CreateTitleCommand(
                    NormalizeSlug(request.Slug),
                    NormalizeCode(request.ContentKind)!,
                    TitleLifecycleStatuses.Draft,
                    TitleVisibilities.Private,
                    MapMetadataCommand(request.Metadata)),
                cancellationToken);

            return result.Status switch
            {
                TitleMutationStatus.Success => Results.Created(
                    $"/catalog/{result.Title!.OrganizationSlug}/{result.Title.Slug}",
                    new DeveloperTitleResponse(MapDeveloperTitleDetail(result.Title))),
                TitleMutationStatus.NotFound => Results.NotFound(),
                TitleMutationStatus.Forbidden => Results.Forbid(),
                TitleMutationStatus.Conflict => CreateProblemResult(
                    StatusCodes.Status409Conflict,
                    "Title already exists",
                    "The supplied title slug is already in use within the organization.",
                    "title_slug_conflict"),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapGet("/{titleId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleService.GetTitleAsync(user.Claims, titleId, cancellationToken);
            return result.Status switch
            {
                TitleMutationStatus.Success => Results.Ok(new DeveloperTitleResponse(MapDeveloperTitleDetail(result.Title!))),
                TitleMutationStatus.NotFound => Results.NotFound(),
                TitleMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapPut("/{titleId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            UpdateTitleRequest request,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateUpdateTitleRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await titleService.UpdateTitleAsync(
                user.Claims,
                titleId,
                new UpdateTitleCommand(
                    NormalizeSlug(request.Slug),
                    NormalizeCode(request.ContentKind)!,
                    NormalizeCode(request.LifecycleStatus)!,
                    NormalizeCode(request.Visibility)!),
                cancellationToken);

            return result.Status switch
            {
                TitleMutationStatus.Success => Results.Ok(new DeveloperTitleResponse(MapDeveloperTitleDetail(result.Title!))),
                TitleMutationStatus.NotFound => Results.NotFound(),
                TitleMutationStatus.Forbidden => Results.Forbid(),
                TitleMutationStatus.Conflict => CreateProblemResult(
                    StatusCodes.Status409Conflict,
                    "Title already exists",
                    "The supplied title slug is already in use within the organization.",
                    "title_slug_conflict"),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapPut("/{titleId:guid}/metadata/current", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            UpsertTitleMetadataRequest request,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateMetadataRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await titleService.UpsertCurrentMetadataAsync(
                user.Claims,
                titleId,
                MapMetadataCommand(request),
                cancellationToken);

            return result.Status switch
            {
                TitleMutationStatus.Success => Results.Ok(new DeveloperTitleResponse(MapDeveloperTitleDetail(result.Title!))),
                TitleMutationStatus.NotFound => Results.NotFound(),
                TitleMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapGet("/{titleId:guid}/metadata-versions", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleService.ListMetadataVersionsAsync(user.Claims, titleId, cancellationToken);
            return result.Status switch
            {
                TitleMetadataVersionListStatus.Success => Results.Ok(
                    new TitleMetadataVersionListResponse(result.MetadataVersions!.Select(MapMetadataVersion).ToArray())),
                TitleMetadataVersionListStatus.NotFound => Results.NotFound(),
                TitleMetadataVersionListStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapPost("/{titleId:guid}/metadata-versions/{revisionNumber:int}/activate", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            int revisionNumber,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleService.ActivateMetadataVersionAsync(user.Claims, titleId, revisionNumber, cancellationToken);
            return result.Status switch
            {
                TitleMutationStatus.Success => Results.Ok(new DeveloperTitleResponse(MapDeveloperTitleDetail(result.Title!))),
                TitleMutationStatus.NotFound => Results.NotFound(),
                TitleMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        MapWave4TitleEndpoints(developerTitleGroup);

        return app;
    }

    private static Dictionary<string, string[]> ValidateCreateTitleRequest(CreateTitleRequest request)
    {
        var errors = ValidateTitleCoreRequest(request.Slug, request.ContentKind);
        foreach (var pair in ValidateMetadataRequest(request.Metadata))
        {
            errors[pair.Key] = pair.Value;
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateUpdateTitleRequest(UpdateTitleRequest request) =>
        ValidateTitleRequest(request);

    private static Dictionary<string, string[]> ValidateCatalogBrowseRequest(
        string? contentKind,
        string? sort,
        int? pageNumber,
        int? pageSize)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var normalizedContentKind = NormalizeCode(contentKind);
        if (normalizedContentKind is not null &&
            normalizedContentKind is not (TitleContentKinds.Game or TitleContentKinds.App))
        {
            errors["contentKind"] = ["Content kind must be one of: game, app."];
        }

        var normalizedSort = NormalizeCode(sort);
        if (normalizedSort is not null &&
            normalizedSort is not (CatalogSortModes.Title or CatalogSortModes.Genre))
        {
            errors["sort"] = ["Sort must be one of: title, genre."];
        }

        if (pageNumber is <= 0)
        {
            errors["pageNumber"] = ["Page number must be at least 1."];
        }

        if (pageSize is <= 0 or > MaxCatalogPageSize)
        {
            errors["pageSize"] = [$"Page size must be between 1 and {MaxCatalogPageSize}."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateTitleRequest(ITitleRequest request)
    {
        var errors = ValidateTitleCoreRequest(request.Slug, request.ContentKind);

        var normalizedLifecycleStatus = NormalizeCode(request.LifecycleStatus);
        if (normalizedLifecycleStatus is not
            (TitleLifecycleStatuses.Draft or TitleLifecycleStatuses.Testing or TitleLifecycleStatuses.Published or TitleLifecycleStatuses.Archived))
        {
            errors["lifecycleStatus"] = ["Lifecycle status must be one of: draft, testing, published, archived."];
        }

        var normalizedVisibility = NormalizeCode(request.Visibility);
        if (normalizedVisibility is not
            (TitleVisibilities.Private or TitleVisibilities.Unlisted or TitleVisibilities.Listed))
        {
            errors["visibility"] = ["Visibility must be one of: private, unlisted, listed."];
        }
        else if (normalizedLifecycleStatus is (TitleLifecycleStatuses.Draft or TitleLifecycleStatuses.Archived) &&
            normalizedVisibility != TitleVisibilities.Private)
        {
            errors["visibility"] = ["Draft and archived titles must use private visibility."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateTitleCoreRequest(string slug, string contentKind)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(slug))
        {
            errors["slug"] = ["Slug is required."];
        }
        else if (!SlugRegex.IsMatch(NormalizeSlug(slug)))
        {
            errors["slug"] = ["Slug must contain only lowercase letters, numbers, and single hyphen separators."];
        }

        var normalizedContentKind = NormalizeCode(contentKind);
        if (normalizedContentKind is not (TitleContentKinds.Game or TitleContentKinds.App))
        {
            errors["contentKind"] = ["Content kind must be one of: game, app."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateMetadataRequest(UpsertTitleMetadataRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors["metadata.displayName"] = ["Display name is required."];
        }

        if (string.IsNullOrWhiteSpace(request.ShortDescription))
        {
            errors["metadata.shortDescription"] = ["Short description is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            errors["metadata.description"] = ["Description is required."];
        }

        if (string.IsNullOrWhiteSpace(request.GenreDisplay))
        {
            errors["metadata.genreDisplay"] = ["Genre display is required."];
        }

        if (request.MinPlayers < 1)
        {
            errors["metadata.minPlayers"] = ["Minimum players must be at least 1."];
        }

        if (request.MaxPlayers < request.MinPlayers)
        {
            errors["metadata.maxPlayers"] = ["Maximum players must be greater than or equal to minimum players."];
        }

        if (string.IsNullOrWhiteSpace(request.AgeRatingAuthority))
        {
            errors["metadata.ageRatingAuthority"] = ["Age rating authority is required."];
        }

        if (string.IsNullOrWhiteSpace(request.AgeRatingValue))
        {
            errors["metadata.ageRatingValue"] = ["Age rating value is required."];
        }

        if (request.MinAgeYears < 0)
        {
            errors["metadata.minAgeYears"] = ["Minimum age years must be zero or greater."];
        }

        return errors;
    }

    private static CatalogTitleSummaryDto MapTitleSummary(TitleSnapshot title) =>
        new(
            title.Id,
            title.OrganizationId,
            title.OrganizationSlug,
            title.Slug,
            title.ContentKind,
            title.LifecycleStatus,
            title.Visibility,
            title.CurrentMetadataRevision,
            title.DisplayName,
            title.ShortDescription,
            title.GenreDisplay,
            title.MinPlayers,
            title.MaxPlayers,
            BuildPlayerCountDisplay(title.MinPlayers, title.MaxPlayers),
            title.AgeRatingAuthority,
            title.AgeRatingValue,
            title.MinAgeYears,
            BuildAgeDisplay(title.AgeRatingAuthority, title.AgeRatingValue),
            title.CardImageUrl,
            title.AcquisitionUrl);

    private static CatalogTitleDto MapTitleDetail(TitleSnapshot title) =>
        new(
            title.Id,
            title.OrganizationId,
            title.OrganizationSlug,
            title.Slug,
            title.ContentKind,
            title.LifecycleStatus,
            title.Visibility,
            title.CurrentMetadataRevision,
            title.DisplayName,
            title.ShortDescription,
            title.Description,
            title.GenreDisplay,
            title.MinPlayers,
            title.MaxPlayers,
            BuildPlayerCountDisplay(title.MinPlayers, title.MaxPlayers),
            title.AgeRatingAuthority,
            title.AgeRatingValue,
            title.MinAgeYears,
            BuildAgeDisplay(title.AgeRatingAuthority, title.AgeRatingValue),
            title.CardImageUrl,
            title.AcquisitionUrl,
            title.MediaAssets.Select(MapTitleMediaAsset).ToArray(),
            MapCurrentRelease(title.CurrentRelease),
            MapPublicTitleAcquisition(title.Acquisition),
            title.CreatedAtUtc,
            title.UpdatedAtUtc);

    private static CatalogPageDto MapCatalogPage(CatalogPageSnapshot page) =>
        new(
            page.PageNumber,
            page.PageSize,
            page.TotalCount,
            page.TotalPages,
            page.HasPreviousPage,
            page.HasNextPage);

    private static DeveloperTitleDto MapDeveloperTitleDetail(TitleSnapshot title) =>
        new(
            title.Id,
            title.OrganizationId,
            title.OrganizationSlug,
            title.Slug,
            title.ContentKind,
            title.LifecycleStatus,
            title.Visibility,
            title.CurrentMetadataRevision,
            title.DisplayName,
            title.ShortDescription,
            title.Description,
            title.GenreDisplay,
            title.MinPlayers,
            title.MaxPlayers,
            BuildPlayerCountDisplay(title.MinPlayers, title.MaxPlayers),
            title.AgeRatingAuthority,
            title.AgeRatingValue,
            title.MinAgeYears,
            BuildAgeDisplay(title.AgeRatingAuthority, title.AgeRatingValue),
            title.CardImageUrl,
            title.AcquisitionUrl,
            title.MediaAssets.Select(MapTitleMediaAsset).ToArray(),
            MapCurrentRelease(title.CurrentRelease),
            MapPublicTitleAcquisition(title.Acquisition),
            title.CurrentReleaseId,
            title.CreatedAtUtc,
            title.UpdatedAtUtc);

    private static TitleMetadataVersionDto MapMetadataVersion(TitleMetadataVersionSnapshot version) =>
        new(
            version.RevisionNumber,
            version.IsCurrent,
            version.IsFrozen,
            version.DisplayName,
            version.ShortDescription,
            version.Description,
            version.GenreDisplay,
            version.MinPlayers,
            version.MaxPlayers,
            BuildPlayerCountDisplay(version.MinPlayers, version.MaxPlayers),
            version.AgeRatingAuthority,
            version.AgeRatingValue,
            version.MinAgeYears,
            BuildAgeDisplay(version.AgeRatingAuthority, version.AgeRatingValue),
            version.CreatedAtUtc,
            version.UpdatedAtUtc);

    private static UpsertTitleMetadataCommand MapMetadataCommand(UpsertTitleMetadataRequest request) =>
        new(
            request.DisplayName.Trim(),
            request.ShortDescription.Trim(),
            request.Description.Trim(),
            request.GenreDisplay.Trim(),
            request.MinPlayers,
            request.MaxPlayers,
            request.AgeRatingAuthority.Trim(),
            request.AgeRatingValue.Trim(),
            request.MinAgeYears);

    private static PublicTitleAcquisitionDto? MapPublicTitleAcquisition(PublicTitleAcquisitionSnapshot? acquisition) =>
        acquisition is null
            ? null
            : new PublicTitleAcquisitionDto(
                acquisition.Url,
                acquisition.Label,
                acquisition.ProviderDisplayName,
                acquisition.ProviderHomepageUrl);

    private static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();

    private static string? NormalizeCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string BuildPlayerCountDisplay(int minPlayers, int maxPlayers) =>
        minPlayers == maxPlayers
            ? $"{minPlayers} {(minPlayers == 1 ? "player" : "players")}"
            : $"{minPlayers}-{maxPlayers} players";

    private static string BuildAgeDisplay(string ageRatingAuthority, string ageRatingValue) =>
        $"{ageRatingAuthority.Trim()} {ageRatingValue.Trim()}";

    private static IResult CreateProblemResult(int statusCode, string title, string detail, string code) =>
        Results.Json(
            new TitleProblemEnvelope(
                Type: $"https://boardtpl.dev/problems/{code.Replace('_', '-')}",
                Title: title,
                Status: statusCode,
                Detail: detail,
                Code: code),
            statusCode: statusCode);

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();
}

/// <summary>
/// Shared stable title fields used by create and update request payloads.
/// </summary>
internal interface ITitleRequest
{
    /// <summary>
    /// Gets the organization-scoped title route key.
    /// </summary>
    string Slug { get; }

    /// <summary>
    /// Gets the stable title content kind.
    /// </summary>
    string ContentKind { get; }

    /// <summary>
    /// Gets the lifecycle state for the title.
    /// </summary>
    string LifecycleStatus { get; }

    /// <summary>
    /// Gets the public discoverability mode for the title.
    /// </summary>
    string Visibility { get; }
}

/// <summary>
/// Request payload for creating a title.
/// </summary>
/// <param name="Slug">Title route key unique within the organization.</param>
/// <param name="ContentKind">Content kind such as game or app.</param>
/// <param name="LifecycleStatus">Reserved for forward compatibility; create currently forces draft lifecycle.</param>
/// <param name="Visibility">Reserved for forward compatibility; create currently forces private visibility.</param>
/// <param name="Metadata">Initial required metadata snapshot.</param>
internal sealed record CreateTitleRequest(
    string Slug,
    string ContentKind,
    string LifecycleStatus,
    string Visibility,
    UpsertTitleMetadataRequest Metadata) : ITitleRequest;

/// <summary>
/// Request payload for updating stable title fields.
/// </summary>
/// <param name="Slug">Title route key unique within the organization.</param>
/// <param name="ContentKind">Content kind such as game or app.</param>
/// <param name="LifecycleStatus">Lifecycle status for the title.</param>
/// <param name="Visibility">Visibility mode for the title.</param>
internal sealed record UpdateTitleRequest(
    string Slug,
    string ContentKind,
    string LifecycleStatus,
    string Visibility) : ITitleRequest;

/// <summary>
/// Request payload for creating or updating the current title metadata snapshot.
/// </summary>
/// <param name="DisplayName">Public display name.</param>
/// <param name="ShortDescription">Short public description.</param>
/// <param name="Description">Full public description.</param>
/// <param name="GenreDisplay">Display-oriented genre text.</param>
/// <param name="MinPlayers">Minimum player count.</param>
/// <param name="MaxPlayers">Maximum player count.</param>
/// <param name="AgeRatingAuthority">Age-rating authority such as ESRB or PEGI.</param>
/// <param name="AgeRatingValue">Authority-specific age rating value.</param>
/// <param name="MinAgeYears">Minimum recommended player age.</param>
internal sealed record UpsertTitleMetadataRequest(
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
/// Public catalog title summary DTO.
/// </summary>
/// <param name="Id">Title identifier.</param>
/// <param name="OrganizationId">Owning organization identifier.</param>
/// <param name="OrganizationSlug">Owning organization route key.</param>
/// <param name="Slug">Title route key unique within the organization.</param>
/// <param name="ContentKind">Content kind such as game or app.</param>
/// <param name="LifecycleStatus">Lifecycle status for the title.</param>
/// <param name="Visibility">Visibility mode for the title.</param>
/// <param name="CurrentMetadataRevision">Currently active metadata revision number.</param>
/// <param name="DisplayName">Public display name.</param>
/// <param name="ShortDescription">Short public description.</param>
/// <param name="GenreDisplay">Display-oriented genre text.</param>
/// <param name="MinPlayers">Minimum player count.</param>
/// <param name="MaxPlayers">Maximum player count.</param>
/// <param name="PlayerCountDisplay">Derived public player-count display.</param>
/// <param name="AgeRatingAuthority">Age-rating authority such as ESRB or PEGI.</param>
/// <param name="AgeRatingValue">Authority-specific age rating value.</param>
/// <param name="MinAgeYears">Minimum recommended player age.</param>
/// <param name="AgeDisplay">Derived public age display.</param>
/// <param name="CardImageUrl">Card/list image URL when configured.</param>
/// <param name="AcquisitionUrl">Primary acquisition URL when an active binding exists.</param>
internal sealed record CatalogTitleSummaryDto(
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
    string GenreDisplay,
    int MinPlayers,
    int MaxPlayers,
    string PlayerCountDisplay,
    string AgeRatingAuthority,
    string AgeRatingValue,
    int MinAgeYears,
    string AgeDisplay,
    string? CardImageUrl,
    string? AcquisitionUrl);

/// <summary>
/// Detailed catalog title DTO.
/// </summary>
/// <param name="Id">Title identifier.</param>
/// <param name="OrganizationId">Owning organization identifier.</param>
/// <param name="OrganizationSlug">Owning organization route key.</param>
/// <param name="Slug">Title route key unique within the organization.</param>
/// <param name="ContentKind">Content kind such as game or app.</param>
/// <param name="LifecycleStatus">Lifecycle status for the title.</param>
/// <param name="Visibility">Visibility mode for the title.</param>
/// <param name="CurrentMetadataRevision">Currently active metadata revision number.</param>
/// <param name="DisplayName">Public display name.</param>
/// <param name="ShortDescription">Short public description.</param>
/// <param name="Description">Full public description.</param>
/// <param name="GenreDisplay">Display-oriented genre text.</param>
/// <param name="MinPlayers">Minimum player count.</param>
/// <param name="MaxPlayers">Maximum player count.</param>
/// <param name="PlayerCountDisplay">Derived public player-count display.</param>
/// <param name="AgeRatingAuthority">Age-rating authority such as ESRB or PEGI.</param>
/// <param name="AgeRatingValue">Authority-specific age rating value.</param>
/// <param name="MinAgeYears">Minimum recommended player age.</param>
/// <param name="AgeDisplay">Derived public age display.</param>
/// <param name="CardImageUrl">Card/list image URL when configured.</param>
/// <param name="AcquisitionUrl">Primary acquisition URL when an active binding exists.</param>
/// <param name="MediaAssets">Configured title media assets.</param>
/// <param name="CurrentRelease">Currently active public release when present.</param>
/// <param name="Acquisition">Detailed public acquisition summary when present.</param>
/// <param name="CreatedAt">UTC timestamp when the title was created.</param>
/// <param name="UpdatedAt">UTC timestamp when the title was last updated.</param>
internal sealed record CatalogTitleDto(
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
    string PlayerCountDisplay,
    string AgeRatingAuthority,
    string AgeRatingValue,
    int MinAgeYears,
    string AgeDisplay,
    string? CardImageUrl,
    string? AcquisitionUrl,
    IReadOnlyList<TitleMediaAssetDto> MediaAssets,
    CurrentTitleReleaseDto? CurrentRelease,
    PublicTitleAcquisitionDto? Acquisition,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Detailed developer title DTO.
/// </summary>
/// <param name="Id">Title identifier.</param>
/// <param name="OrganizationId">Owning organization identifier.</param>
/// <param name="OrganizationSlug">Owning organization route key.</param>
/// <param name="Slug">Title route key unique within the organization.</param>
/// <param name="ContentKind">Content kind such as game or app.</param>
/// <param name="LifecycleStatus">Lifecycle status for the title.</param>
/// <param name="Visibility">Visibility mode for the title.</param>
/// <param name="CurrentMetadataRevision">Currently active metadata revision number.</param>
/// <param name="DisplayName">Public display name.</param>
/// <param name="ShortDescription">Short public description.</param>
/// <param name="Description">Full public description.</param>
/// <param name="GenreDisplay">Display-oriented genre text.</param>
/// <param name="MinPlayers">Minimum player count.</param>
/// <param name="MaxPlayers">Maximum player count.</param>
/// <param name="PlayerCountDisplay">Derived public player-count display.</param>
/// <param name="AgeRatingAuthority">Age-rating authority such as ESRB or PEGI.</param>
/// <param name="AgeRatingValue">Authority-specific age rating value.</param>
/// <param name="MinAgeYears">Minimum recommended player age.</param>
/// <param name="AgeDisplay">Derived public age display.</param>
/// <param name="CardImageUrl">Card/list image URL when configured.</param>
/// <param name="AcquisitionUrl">Primary acquisition URL when an active binding exists.</param>
/// <param name="MediaAssets">Configured title media assets.</param>
/// <param name="CurrentRelease">Currently active public release when present.</param>
/// <param name="Acquisition">Detailed public acquisition summary when present.</param>
/// <param name="CurrentReleaseId">Currently active release identifier when present.</param>
/// <param name="CreatedAt">UTC timestamp when the title was created.</param>
/// <param name="UpdatedAt">UTC timestamp when the title was last updated.</param>
internal sealed record DeveloperTitleDto(
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
    string PlayerCountDisplay,
    string AgeRatingAuthority,
    string AgeRatingValue,
    int MinAgeYears,
    string AgeDisplay,
    string? CardImageUrl,
    string? AcquisitionUrl,
    IReadOnlyList<TitleMediaAssetDto> MediaAssets,
    CurrentTitleReleaseDto? CurrentRelease,
    PublicTitleAcquisitionDto? Acquisition,
    Guid? CurrentReleaseId,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Public title acquisition DTO.
/// </summary>
/// <param name="Url">External acquisition URL.</param>
/// <param name="Label">Optional player-facing acquisition label.</param>
/// <param name="ProviderDisplayName">Provider name shown to players.</param>
/// <param name="ProviderHomepageUrl">Canonical provider homepage URL when known.</param>
internal sealed record PublicTitleAcquisitionDto(
    string Url,
    string? Label,
    string ProviderDisplayName,
    string? ProviderHomepageUrl);

/// <summary>
/// Response wrapper for public catalog title lists.
/// </summary>
/// <param name="Titles">Catalog titles visible to the caller.</param>
/// <param name="Paging">Paging metadata for the matched result set.</param>
internal sealed record CatalogTitleListResponse(IReadOnlyList<CatalogTitleSummaryDto> Titles, CatalogPageDto Paging);

/// <summary>
/// Paging metadata for public catalog browse responses.
/// </summary>
/// <param name="PageNumber">1-based page number.</param>
/// <param name="PageSize">Requested page size.</param>
/// <param name="TotalCount">Total matched titles across all pages.</param>
/// <param name="TotalPages">Total available pages for the current page size.</param>
/// <param name="HasPreviousPage">Whether a previous page exists.</param>
/// <param name="HasNextPage">Whether a next page exists.</param>
internal sealed record CatalogPageDto(
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage);

/// <summary>
/// Response wrapper for a public catalog title.
/// </summary>
/// <param name="Title">Catalog title details.</param>
internal sealed record CatalogTitleResponse(CatalogTitleDto Title);

/// <summary>
/// Response wrapper for developer-visible title lists.
/// </summary>
/// <param name="Titles">Titles visible to the caller.</param>
internal sealed record DeveloperTitleListResponse(IReadOnlyList<CatalogTitleSummaryDto> Titles);

/// <summary>
/// Response wrapper for a developer-visible title.
/// </summary>
/// <param name="Title">Title details.</param>
internal sealed record DeveloperTitleResponse(DeveloperTitleDto Title);

/// <summary>
/// Metadata revision DTO.
/// </summary>
/// <param name="RevisionNumber">Per-title revision number.</param>
/// <param name="IsCurrent">Whether the revision is currently active for the title.</param>
/// <param name="IsFrozen">Whether the revision is immutable.</param>
/// <param name="DisplayName">Public display name.</param>
/// <param name="ShortDescription">Short public description.</param>
/// <param name="Description">Full public description.</param>
/// <param name="GenreDisplay">Display-oriented genre text.</param>
/// <param name="MinPlayers">Minimum player count.</param>
/// <param name="MaxPlayers">Maximum player count.</param>
/// <param name="PlayerCountDisplay">Derived public player-count display.</param>
/// <param name="AgeRatingAuthority">Age-rating authority such as ESRB or PEGI.</param>
/// <param name="AgeRatingValue">Authority-specific age rating value.</param>
/// <param name="MinAgeYears">Minimum recommended player age.</param>
/// <param name="AgeDisplay">Derived public age display.</param>
/// <param name="CreatedAt">UTC timestamp when the metadata revision was created.</param>
/// <param name="UpdatedAt">UTC timestamp when the metadata revision was last updated.</param>
internal sealed record TitleMetadataVersionDto(
    int RevisionNumber,
    bool IsCurrent,
    bool IsFrozen,
    string DisplayName,
    string ShortDescription,
    string Description,
    string GenreDisplay,
    int MinPlayers,
    int MaxPlayers,
    string PlayerCountDisplay,
    string AgeRatingAuthority,
    string AgeRatingValue,
    int MinAgeYears,
    string AgeDisplay,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Response wrapper for metadata revision lists.
/// </summary>
/// <param name="MetadataVersions">Metadata revisions visible to the caller.</param>
internal sealed record TitleMetadataVersionListResponse(IReadOnlyList<TitleMetadataVersionDto> MetadataVersions);

/// <summary>
/// Problem-details-style payload used for title-specific conflict responses.
/// </summary>
/// <param name="Type">Problem type URI.</param>
/// <param name="Title">Short problem title.</param>
/// <param name="Status">HTTP status code.</param>
/// <param name="Detail">Problem description.</param>
/// <param name="Code">Application-specific error code.</param>
internal sealed record TitleProblemEnvelope(string? Type, string Title, int Status, string? Detail, string? Code);
