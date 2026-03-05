using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Board.ThirdPartyLibrary.Api.Titles;

internal static partial class TitleEndpoints
{
    private static readonly Regex PackageNameRegex = PackageNamePattern();
    private static readonly Regex ReleaseVersionRegex = ReleaseVersionPattern();
    private static readonly Regex Sha256Regex = Sha256Pattern();
    private const long MaxTitleMediaUploadBytes = 25L * 1024L * 1024L;
    private static readonly HashSet<string> SupportedTitleMediaContentTypes = new(StringComparer.Ordinal)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    private static void MapWave4TitleEndpoints(RouteGroupBuilder developerTitleGroup)
    {
        developerTitleGroup.MapGet("/{titleId:guid}/media", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleService.ListMediaAssetsAsync(user.Claims, titleId, cancellationToken);
            return result.Status switch
            {
                TitleResourceListStatus.Success => Results.Ok(
                    new TitleMediaAssetListResponse(result.MediaAssets!.Select(MapTitleMediaAsset).ToArray())),
                TitleResourceListStatus.NotFound => Results.NotFound(),
                TitleResourceListStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapPost("/{titleId:guid}/media/{mediaRole}/upload", [Authorize] async (
            ClaimsPrincipal user,
            HttpContext httpContext,
            Guid titleId,
            string mediaRole,
            [FromForm] UploadTitleMediaAssetForm request,
            ITitleService titleService,
            ITitleMediaStorage titleMediaStorage,
            CancellationToken cancellationToken) =>
        {
            var normalizedMediaRole = NormalizeCode(mediaRole);
            if (normalizedMediaRole is not (TitleMediaRoles.Card or TitleMediaRoles.Hero or TitleMediaRoles.Logo))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["mediaRole"] = ["Media role must be one of: card, hero, logo."]
                    },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            if (request.Media is null)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["media"] = ["Media image is required."]
                    },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            if (request.Media.Length <= 0)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["media"] = ["Media image cannot be empty."]
                    },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            if (request.Media.Length > MaxTitleMediaUploadBytes)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["media"] = [$"Media image size must be {MaxTitleMediaUploadBytes / 1024 / 1024} MB or less."]
                    },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var contentType = string.IsNullOrWhiteSpace(request.Media.ContentType)
                ? string.Empty
                : request.Media.ContentType.Trim().ToLowerInvariant();
            if (!SupportedTitleMediaContentTypes.Contains(contentType))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["media"] = ["Media image format must be JPEG, PNG, WEBP, or GIF."]
                    },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            await using var mediaStream = request.Media.OpenReadStream();
            using var memoryStream = new MemoryStream();
            await mediaStream.CopyToAsync(memoryStream, cancellationToken);

            var routePath = await titleMediaStorage.SaveTitleMediaAsync(
                titleId,
                normalizedMediaRole,
                contentType,
                memoryStream.ToArray(),
                cancellationToken);
            var sourceUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{routePath}";

            var result = await titleService.UpsertMediaAssetAsync(
                user.Claims,
                titleId,
                normalizedMediaRole,
                new UpsertTitleMediaAssetCommand(
                    sourceUrl,
                    string.IsNullOrWhiteSpace(request.AltText) ? null : request.AltText.Trim(),
                    contentType,
                    null,
                    null),
                cancellationToken);

            return result.Status switch
            {
                TitleResourceMutationStatus.Success => Results.Ok(new TitleMediaAssetResponse(MapTitleMediaAsset(result.MediaAsset!))),
                TitleResourceMutationStatus.NotFound => Results.NotFound(),
                TitleResourceMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        }).DisableAntiforgery();

        developerTitleGroup.MapPut("/{titleId:guid}/media/{mediaRole}", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            string mediaRole,
            UpsertTitleMediaAssetRequest request,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateMediaAssetRequest(mediaRole, request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await titleService.UpsertMediaAssetAsync(
                user.Claims,
                titleId,
                NormalizeCode(mediaRole)!,
                MapMediaAssetCommand(request),
                cancellationToken);

            return result.Status switch
            {
                TitleResourceMutationStatus.Success => Results.Ok(new TitleMediaAssetResponse(MapTitleMediaAsset(result.MediaAsset!))),
                TitleResourceMutationStatus.NotFound => Results.NotFound(),
                TitleResourceMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapDelete("/{titleId:guid}/media/{mediaRole}", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            string mediaRole,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var normalizedMediaRole = NormalizeCode(mediaRole);
            if (normalizedMediaRole is not (TitleMediaRoles.Card or TitleMediaRoles.Hero or TitleMediaRoles.Logo))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>
                    {
                        ["mediaRole"] = ["Media role must be one of: card, hero, logo."]
                    },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await titleService.DeleteMediaAssetAsync(user.Claims, titleId, normalizedMediaRole, cancellationToken);
            return result.Status switch
            {
                TitleResourceMutationStatus.Success => Results.NoContent(),
                TitleResourceMutationStatus.NotFound => Results.NotFound(),
                TitleResourceMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapGet("/{titleId:guid}/releases", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleService.ListReleasesAsync(user.Claims, titleId, cancellationToken);
            return result.Status switch
            {
                TitleResourceListStatus.Success => Results.Ok(new TitleReleaseListResponse(result.Releases!.Select(MapTitleRelease).ToArray())),
                TitleResourceListStatus.NotFound => Results.NotFound(),
                TitleResourceListStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapPost("/{titleId:guid}/releases", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            CreateTitleReleaseRequest request,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateTitleReleaseRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await titleService.CreateReleaseAsync(
                user.Claims,
                titleId,
                new CreateTitleReleaseCommand(request.Version.Trim(), request.MetadataRevisionNumber),
                cancellationToken);

            return result.Status switch
            {
                TitleResourceMutationStatus.Success => Results.Created(
                    $"/developer/titles/{titleId}/releases/{result.Release!.Id}",
                    new TitleReleaseResponse(MapTitleRelease(result.Release))),
                TitleResourceMutationStatus.NotFound => Results.NotFound(),
                TitleResourceMutationStatus.Forbidden => Results.Forbid(),
                TitleResourceMutationStatus.Conflict => CreateReleaseConflictResult(
                    result.ErrorCode,
                    "Only draft releases can be modified."),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapGet("/{titleId:guid}/releases/{releaseId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            Guid releaseId,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleService.GetReleaseAsync(user.Claims, titleId, releaseId, cancellationToken);
            return result.Status switch
            {
                TitleResourceMutationStatus.Success => Results.Ok(new TitleReleaseResponse(MapTitleRelease(result.Release!))),
                TitleResourceMutationStatus.NotFound => Results.NotFound(),
                TitleResourceMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapPut("/{titleId:guid}/releases/{releaseId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            Guid releaseId,
            UpdateTitleReleaseRequest request,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateTitleReleaseRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await titleService.UpdateReleaseAsync(
                user.Claims,
                titleId,
                releaseId,
                new UpdateTitleReleaseCommand(request.Version.Trim(), request.MetadataRevisionNumber),
                cancellationToken);

            return result.Status switch
            {
                TitleResourceMutationStatus.Success => Results.Ok(new TitleReleaseResponse(MapTitleRelease(result.Release!))),
                TitleResourceMutationStatus.NotFound => Results.NotFound(),
                TitleResourceMutationStatus.Forbidden => Results.Forbid(),
                TitleResourceMutationStatus.Conflict => CreateReleaseConflictResult(
                    result.ErrorCode,
                    "Only draft releases can be modified."),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapPost("/{titleId:guid}/releases/{releaseId:guid}/publish", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            Guid releaseId,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleService.PublishReleaseAsync(user.Claims, titleId, releaseId, cancellationToken);
            return result.Status switch
            {
                TitleResourceMutationStatus.Success => Results.Ok(new TitleReleaseResponse(MapTitleRelease(result.Release!))),
                TitleResourceMutationStatus.NotFound => Results.NotFound(),
                TitleResourceMutationStatus.Forbidden => Results.Forbid(),
                TitleResourceMutationStatus.Conflict => CreateReleaseConflictResult(
                    result.ErrorCode,
                    "Only draft releases can be published."),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapPost("/{titleId:guid}/releases/{releaseId:guid}/activate", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            Guid releaseId,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleService.ActivateReleaseAsync(user.Claims, titleId, releaseId, cancellationToken);
            return result.Status switch
            {
                TitleMutationStatus.Success => Results.Ok(new DeveloperTitleResponse(MapDeveloperTitleDetail(result.Title!))),
                TitleMutationStatus.NotFound => Results.NotFound(),
                TitleMutationStatus.Forbidden => Results.Forbid(),
                TitleMutationStatus.Conflict => CreateProblemResult(
                    StatusCodes.Status409Conflict,
                    "Release state conflict",
                    "Only published releases can be activated.",
                    result.ErrorCode ?? TitleResourceErrorCodes.TitleReleaseStateConflict),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapPost("/{titleId:guid}/releases/{releaseId:guid}/withdraw", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            Guid releaseId,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleService.WithdrawReleaseAsync(user.Claims, titleId, releaseId, cancellationToken);
            return result.Status switch
            {
                TitleResourceMutationStatus.Success => Results.Ok(new TitleReleaseResponse(MapTitleRelease(result.Release!))),
                TitleResourceMutationStatus.NotFound => Results.NotFound(),
                TitleResourceMutationStatus.Forbidden => Results.Forbid(),
                TitleResourceMutationStatus.Conflict => CreateReleaseConflictResult(
                    result.ErrorCode,
                    "Only published releases can be withdrawn."),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapGet("/{titleId:guid}/releases/{releaseId:guid}/artifacts", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            Guid releaseId,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleService.ListReleaseArtifactsAsync(user.Claims, titleId, releaseId, cancellationToken);
            return result.Status switch
            {
                TitleResourceListStatus.Success => Results.Ok(
                    new ReleaseArtifactListResponse(result.Artifacts!.Select(MapReleaseArtifact).ToArray())),
                TitleResourceListStatus.NotFound => Results.NotFound(),
                TitleResourceListStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapPost("/{titleId:guid}/releases/{releaseId:guid}/artifacts", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            Guid releaseId,
            UpsertReleaseArtifactRequest request,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateReleaseArtifactRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await titleService.CreateReleaseArtifactAsync(
                user.Claims,
                titleId,
                releaseId,
                MapReleaseArtifactCommand(request),
                cancellationToken);

            return result.Status switch
            {
                TitleResourceMutationStatus.Success => Results.Created(
                    $"/developer/titles/{titleId}/releases/{releaseId}/artifacts/{result.Artifact!.Id}",
                    new ReleaseArtifactResponse(MapReleaseArtifact(result.Artifact))),
                TitleResourceMutationStatus.NotFound => Results.NotFound(),
                TitleResourceMutationStatus.Forbidden => Results.Forbid(),
                TitleResourceMutationStatus.Conflict => CreateArtifactConflictResult(result.ErrorCode),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapPut("/{titleId:guid}/releases/{releaseId:guid}/artifacts/{artifactId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            Guid releaseId,
            Guid artifactId,
            UpsertReleaseArtifactRequest request,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateReleaseArtifactRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await titleService.UpdateReleaseArtifactAsync(
                user.Claims,
                titleId,
                releaseId,
                artifactId,
                MapReleaseArtifactCommand(request),
                cancellationToken);

            return result.Status switch
            {
                TitleResourceMutationStatus.Success => Results.Ok(new ReleaseArtifactResponse(MapReleaseArtifact(result.Artifact!))),
                TitleResourceMutationStatus.NotFound => Results.NotFound(),
                TitleResourceMutationStatus.Forbidden => Results.Forbid(),
                TitleResourceMutationStatus.Conflict => CreateArtifactConflictResult(result.ErrorCode),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapDelete("/{titleId:guid}/releases/{releaseId:guid}/artifacts/{artifactId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            Guid releaseId,
            Guid artifactId,
            ITitleService titleService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleService.DeleteReleaseArtifactAsync(user.Claims, titleId, releaseId, artifactId, cancellationToken);
            return result.Status switch
            {
                TitleResourceMutationStatus.Success => Results.NoContent(),
                TitleResourceMutationStatus.NotFound => Results.NotFound(),
                TitleResourceMutationStatus.Forbidden => Results.Forbid(),
                TitleResourceMutationStatus.Conflict => CreateArtifactConflictResult(result.ErrorCode),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });
    }

    private static Dictionary<string, string[]> ValidateMediaAssetRequest(string mediaRole, UpsertTitleMediaAssetRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var normalizedMediaRole = NormalizeCode(mediaRole);
        if (normalizedMediaRole is not (TitleMediaRoles.Card or TitleMediaRoles.Hero or TitleMediaRoles.Logo))
        {
            errors["mediaRole"] = ["Media role must be one of: card, hero, logo."];
        }

        if (!Uri.TryCreate(request.SourceUrl?.Trim(), UriKind.Absolute, out var sourceUri) ||
            (sourceUri.Scheme != Uri.UriSchemeHttp && sourceUri.Scheme != Uri.UriSchemeHttps))
        {
            errors["sourceUrl"] = ["Source URL must be an absolute http or https URL."];
        }

        if (request.Width is null ^ request.Height is null)
        {
            errors["dimensions"] = ["Width and height must both be supplied when either value is provided."];
        }

        if (request.Width is <= 0)
        {
            errors["width"] = ["Width must be greater than zero when provided."];
        }

        if (request.Height is <= 0)
        {
            errors["height"] = ["Height must be greater than zero when provided."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateTitleReleaseRequest(ITitleReleaseRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(request.Version))
        {
            errors["version"] = ["Release version is required."];
        }
        else if (!ReleaseVersionRegex.IsMatch(request.Version.Trim()))
        {
            errors["version"] = ["Release version must be a valid semver string."];
        }

        if (request.MetadataRevisionNumber < 1)
        {
            errors["metadataRevisionNumber"] = ["Metadata revision number must be greater than zero."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateReleaseArtifactRequest(UpsertReleaseArtifactRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (NormalizeCode(request.ArtifactKind) != ReleaseArtifactKinds.Apk)
        {
            errors["artifactKind"] = ["Artifact kind must be apk."];
        }

        if (string.IsNullOrWhiteSpace(request.PackageName))
        {
            errors["packageName"] = ["Package name is required."];
        }
        else if (!PackageNameRegex.IsMatch(request.PackageName.Trim()))
        {
            errors["packageName"] = ["Package name must be a valid Android package identifier."];
        }

        if (request.VersionCode <= 0)
        {
            errors["versionCode"] = ["Version code must be greater than zero."];
        }

        if (!string.IsNullOrWhiteSpace(request.Sha256) && !Sha256Regex.IsMatch(request.Sha256.Trim()))
        {
            errors["sha256"] = ["SHA-256 must be a 64-character lowercase hexadecimal string when provided."];
        }

        if (request.FileSizeBytes is <= 0)
        {
            errors["fileSizeBytes"] = ["File size must be greater than zero when provided."];
        }

        return errors;
    }

    private static UpsertTitleMediaAssetCommand MapMediaAssetCommand(UpsertTitleMediaAssetRequest request) =>
        new(
            request.SourceUrl.Trim(),
            string.IsNullOrWhiteSpace(request.AltText) ? null : request.AltText.Trim(),
            string.IsNullOrWhiteSpace(request.MimeType) ? null : request.MimeType.Trim(),
            request.Width,
            request.Height);

    private static UpsertReleaseArtifactCommand MapReleaseArtifactCommand(UpsertReleaseArtifactRequest request) =>
        new(
            NormalizeCode(request.ArtifactKind)!,
            request.PackageName.Trim(),
            request.VersionCode,
            string.IsNullOrWhiteSpace(request.Sha256) ? null : request.Sha256.Trim(),
            request.FileSizeBytes);

    private static TitleMediaAssetDto MapTitleMediaAsset(TitleMediaAssetSnapshot asset) =>
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

    private static CurrentTitleReleaseDto? MapCurrentRelease(CurrentTitleReleaseSnapshot? release) =>
        release is null
            ? null
            : new CurrentTitleReleaseDto(
                release.Id,
                release.Version,
                release.MetadataRevisionNumber,
                release.PublishedAtUtc);

    private static TitleReleaseDto MapTitleRelease(TitleReleaseSnapshot release) =>
        new(
            release.Id,
            release.Version,
            release.Status,
            release.MetadataRevisionNumber,
            release.IsCurrent,
            release.PublishedAtUtc,
            release.CreatedAtUtc,
            release.UpdatedAtUtc);

    private static ReleaseArtifactDto MapReleaseArtifact(ReleaseArtifactSnapshot artifact) =>
        new(
            artifact.Id,
            artifact.ArtifactKind,
            artifact.PackageName,
            artifact.VersionCode,
            artifact.Sha256,
            artifact.FileSizeBytes,
            artifact.CreatedAtUtc,
            artifact.UpdatedAtUtc);

    private static IResult CreateReleaseConflictResult(string? errorCode, string defaultStateDetail) =>
        errorCode switch
        {
            TitleResourceErrorCodes.TitleReleaseVersionConflict => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "Release already exists",
                "The supplied release version is already in use for this title.",
                errorCode),
            TitleResourceErrorCodes.TitleReleasePublishRequiresArtifact => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "Release cannot be published",
                "A release must include at least one artifact before it can be published.",
                errorCode),
            _ => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "Release state conflict",
                defaultStateDetail,
                errorCode ?? TitleResourceErrorCodes.TitleReleaseStateConflict)
        };

    private static IResult CreateArtifactConflictResult(string? errorCode) =>
        errorCode switch
        {
            TitleResourceErrorCodes.ReleaseArtifactIdentityConflict => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "Artifact already exists",
                "The supplied artifact package name and version code are already in use for this release.",
                errorCode),
            _ => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "Release state conflict",
                "Artifacts can only be modified while the release is draft.",
                errorCode ?? TitleResourceErrorCodes.TitleReleaseStateConflict)
        };

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*(?:\\.[A-Za-z][A-Za-z0-9_]*)+$")]
    private static partial Regex PackageNamePattern();

    [GeneratedRegex("^(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?(?:\\+[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$")]
    private static partial Regex ReleaseVersionPattern();

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256Pattern();
}

/// <summary>
/// Shared request contract for title release create/update payloads.
/// </summary>
internal interface ITitleReleaseRequest
{
    string Version { get; }

    int MetadataRevisionNumber { get; }
}

/// <summary>
/// Request payload for creating or updating a title media asset.
/// </summary>
/// <param name="SourceUrl">Absolute media source URL.</param>
/// <param name="AltText">Optional accessibility text.</param>
/// <param name="MimeType">Optional MIME type.</param>
/// <param name="Width">Optional media width in pixels.</param>
/// <param name="Height">Optional media height in pixels.</param>
internal sealed record UpsertTitleMediaAssetRequest(
    string SourceUrl,
    string? AltText,
    string? MimeType,
    int? Width,
    int? Height);

/// <summary>
/// Form payload for uploading a title media asset file.
/// </summary>
internal sealed class UploadTitleMediaAssetForm
{
    public IFormFile? Media { get; set; }

    public string? AltText { get; set; }
}

/// <summary>
/// Request payload for creating a title release.
/// </summary>
/// <param name="Version">Release semver string.</param>
/// <param name="MetadataRevisionNumber">Metadata revision number bound to the release.</param>
internal sealed record CreateTitleReleaseRequest(
    string Version,
    int MetadataRevisionNumber) : ITitleReleaseRequest;

/// <summary>
/// Request payload for updating a title release.
/// </summary>
/// <param name="Version">Release semver string.</param>
/// <param name="MetadataRevisionNumber">Metadata revision number bound to the release.</param>
internal sealed record UpdateTitleReleaseRequest(
    string Version,
    int MetadataRevisionNumber) : ITitleReleaseRequest;

/// <summary>
/// Request payload for creating or updating a release artifact.
/// </summary>
/// <param name="ArtifactKind">Artifact kind identifier.</param>
/// <param name="PackageName">Android package identifier.</param>
/// <param name="VersionCode">Android version code.</param>
/// <param name="Sha256">Optional SHA-256 checksum.</param>
/// <param name="FileSizeBytes">Optional artifact size in bytes.</param>
internal sealed record UpsertReleaseArtifactRequest(
    string ArtifactKind,
    string PackageName,
    long VersionCode,
    string? Sha256,
    long? FileSizeBytes);

/// <summary>
/// Title media asset DTO.
/// </summary>
/// <param name="Id">Media asset identifier.</param>
/// <param name="MediaRole">Media slot role.</param>
/// <param name="SourceUrl">Absolute media source URL.</param>
/// <param name="AltText">Optional accessibility text.</param>
/// <param name="MimeType">Optional MIME type.</param>
/// <param name="Width">Optional media width in pixels.</param>
/// <param name="Height">Optional media height in pixels.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="UpdatedAt">UTC update timestamp.</param>
internal sealed record TitleMediaAssetDto(
    Guid Id,
    string MediaRole,
    string SourceUrl,
    string? AltText,
    string? MimeType,
    int? Width,
    int? Height,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Currently active release summary DTO.
/// </summary>
/// <param name="Id">Release identifier.</param>
/// <param name="Version">Release semver string.</param>
/// <param name="MetadataRevisionNumber">Metadata revision number bound to the release.</param>
/// <param name="PublishedAt">UTC publish timestamp.</param>
internal sealed record CurrentTitleReleaseDto(
    Guid Id,
    string Version,
    int MetadataRevisionNumber,
    DateTime PublishedAt);

/// <summary>
/// Title release DTO.
/// </summary>
/// <param name="Id">Release identifier.</param>
/// <param name="Version">Release semver string.</param>
/// <param name="Status">Release lifecycle status.</param>
/// <param name="MetadataRevisionNumber">Metadata revision number bound to the release.</param>
/// <param name="IsCurrent">Whether the release is currently activated for the title.</param>
/// <param name="PublishedAt">UTC publish timestamp when present.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="UpdatedAt">UTC update timestamp.</param>
internal sealed record TitleReleaseDto(
    Guid Id,
    string Version,
    string Status,
    int MetadataRevisionNumber,
    bool IsCurrent,
    DateTime? PublishedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Release artifact DTO.
/// </summary>
/// <param name="Id">Artifact identifier.</param>
/// <param name="ArtifactKind">Artifact kind identifier.</param>
/// <param name="PackageName">Android package identifier.</param>
/// <param name="VersionCode">Android version code.</param>
/// <param name="Sha256">Optional SHA-256 checksum.</param>
/// <param name="FileSizeBytes">Optional artifact size in bytes.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="UpdatedAt">UTC update timestamp.</param>
internal sealed record ReleaseArtifactDto(
    Guid Id,
    string ArtifactKind,
    string PackageName,
    long VersionCode,
    string? Sha256,
    long? FileSizeBytes,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Response wrapper for title media asset listings.
/// </summary>
/// <param name="MediaAssets">Configured media assets.</param>
internal sealed record TitleMediaAssetListResponse(IReadOnlyList<TitleMediaAssetDto> MediaAssets);

/// <summary>
/// Response wrapper for a title media asset.
/// </summary>
/// <param name="MediaAsset">Title media asset details.</param>
internal sealed record TitleMediaAssetResponse(TitleMediaAssetDto MediaAsset);

/// <summary>
/// Response wrapper for title release listings.
/// </summary>
/// <param name="Releases">Configured releases.</param>
internal sealed record TitleReleaseListResponse(IReadOnlyList<TitleReleaseDto> Releases);

/// <summary>
/// Response wrapper for a title release.
/// </summary>
/// <param name="Release">Title release details.</param>
internal sealed record TitleReleaseResponse(TitleReleaseDto Release);

/// <summary>
/// Response wrapper for release artifact listings.
/// </summary>
/// <param name="Artifacts">Configured release artifacts.</param>
internal sealed record ReleaseArtifactListResponse(IReadOnlyList<ReleaseArtifactDto> Artifacts);

/// <summary>
/// Response wrapper for a release artifact.
/// </summary>
/// <param name="Artifact">Release artifact details.</param>
internal sealed record ReleaseArtifactResponse(ReleaseArtifactDto Artifact);
