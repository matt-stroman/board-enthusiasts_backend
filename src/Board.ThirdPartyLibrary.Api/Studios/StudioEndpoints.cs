using System.Security.Claims;
using System.Text.RegularExpressions;
using Board.ThirdPartyLibrary.Api.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Board.ThirdPartyLibrary.Api.Studios;

/// <summary>
/// Maps studio and membership endpoints.
/// </summary>
internal static partial class StudioEndpoints
{
    private static readonly Regex SlugRegex = SlugPattern();
    private static readonly HashSet<string> SupportedStudioMediaContentTypes = new(StringComparer.Ordinal)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
        "image/svg+xml"
    };
    private const long MaxStudioMediaUploadBytes = 10L * 1024L * 1024L;

    /// <summary>
    /// Maps studio endpoints to the application.
    /// </summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapStudioEndpoints(this IEndpointRouteBuilder app)
    {
        var publicGroup = app.MapGroup("/studios");
        var managementGroup = app.MapGroup("/developer/studios");

        publicGroup.MapGet("/", async (
            IStudioService studioService,
            CancellationToken cancellationToken) =>
        {
            var studios = await studioService.ListStudiosAsync(cancellationToken);
            return Results.Ok(new StudioListResponse(studios.Select(MapStudio).ToArray()));
        });

        publicGroup.MapGet("/{slug}", async (
            string slug,
            IStudioService studioService,
            CancellationToken cancellationToken) =>
        {
            var studio = await studioService.GetStudioBySlugAsync(slug.Trim(), cancellationToken);
            return studio is null
                ? Results.NotFound()
                : Results.Ok(new StudioResponse(MapStudio(studio)));
        });

        managementGroup.MapGet("/", [Authorize] async (
            ClaimsPrincipal user,
            IStudioService studioService,
            CancellationToken cancellationToken) =>
        {
            var studios = await studioService.ListManagedStudiosAsync(user.Claims, cancellationToken);
            return Results.Ok(new DeveloperStudioListResponse(studios.Select(MapDeveloperStudio).ToArray()));
        });

        publicGroup.MapPost("/", [Authorize] async (
            ClaimsPrincipal user,
            CreateStudioRequest request,
            IDeveloperEnrollmentService developerEnrollmentService,
            IStudioService studioService,
            CancellationToken cancellationToken) =>
        {
            if (!await developerEnrollmentService.HasDeveloperAccessAsync(user.Claims, cancellationToken))
            {
                return Results.Forbid();
            }

            var validationErrors = ValidateStudioRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await studioService.CreateStudioAsync(
                user.Claims,
                new CreateStudioCommand(
                    NormalizeSlug(request.Slug),
                    request.DisplayName.Trim(),
                    request.Description?.Trim(),
                    request.LogoUrl?.Trim(),
                    request.BannerUrl?.Trim()),
                cancellationToken);

            return result.Status switch
            {
                StudioMutationStatus.Success => Results.Created(
                    $"/studios/{result.Studio!.Slug}",
                    new StudioResponse(MapStudio(result.Studio))),
                StudioMutationStatus.Conflict => CreateProblemResult(
                    StatusCodes.Status409Conflict,
                    "Studio already exists",
                    "The supplied studio slug is already in use.",
                    "studio_slug_conflict"),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        managementGroup.MapPut("/{studioId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid studioId,
            UpdateStudioRequest request,
            IStudioService studioService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateStudioRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await studioService.UpdateStudioAsync(
                user.Claims,
                studioId,
                new UpdateStudioCommand(
                    NormalizeSlug(request.Slug),
                    request.DisplayName.Trim(),
                    request.Description?.Trim(),
                    request.LogoUrl?.Trim(),
                    request.BannerUrl?.Trim()),
                cancellationToken);

            return result.Status switch
            {
                StudioMutationStatus.Success => Results.Ok(new StudioResponse(MapStudio(result.Studio!))),
                StudioMutationStatus.NotFound => Results.NotFound(),
                StudioMutationStatus.Forbidden => Results.Forbid(),
                StudioMutationStatus.Conflict => CreateProblemResult(
                    StatusCodes.Status409Conflict,
                    "Studio already exists",
                    "The supplied studio slug is already in use.",
                    "studio_slug_conflict"),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        managementGroup.MapGet("/{studioId:guid}/links", [Authorize] async (
            ClaimsPrincipal user,
            Guid studioId,
            IStudioService studioService,
            CancellationToken cancellationToken) =>
        {
            var result = await studioService.ListLinksAsync(user.Claims, studioId, cancellationToken);
            return result.Status switch
            {
                StudioLinkListStatus.Success => Results.Ok(new StudioLinkListResponse(result.Links!.Select(MapLink).ToArray())),
                StudioLinkListStatus.NotFound => Results.NotFound(),
                StudioLinkListStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        managementGroup.MapPost("/{studioId:guid}/links", [Authorize] async (
            ClaimsPrincipal user,
            Guid studioId,
            UpsertStudioLinkRequest request,
            IStudioService studioService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateStudioLinkRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await studioService.CreateLinkAsync(
                user.Claims,
                studioId,
                new UpsertStudioLinkCommand(request.Label.Trim(), request.Url.Trim()),
                cancellationToken);

            return result.Status switch
            {
                StudioLinkMutationStatus.Success => Results.Created(
                    $"/developer/studios/{studioId}/links/{result.Link!.Id}",
                    new StudioLinkResponse(MapLink(result.Link))),
                StudioLinkMutationStatus.NotFound => Results.NotFound(),
                StudioLinkMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        managementGroup.MapPut("/{studioId:guid}/links/{linkId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid studioId,
            Guid linkId,
            UpsertStudioLinkRequest request,
            IStudioService studioService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateStudioLinkRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await studioService.UpdateLinkAsync(
                user.Claims,
                studioId,
                linkId,
                new UpsertStudioLinkCommand(request.Label.Trim(), request.Url.Trim()),
                cancellationToken);

            return result.Status switch
            {
                StudioLinkMutationStatus.Success => Results.Ok(new StudioLinkResponse(MapLink(result.Link!))),
                StudioLinkMutationStatus.NotFound => Results.NotFound(),
                StudioLinkMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        managementGroup.MapDelete("/{studioId:guid}/links/{linkId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid studioId,
            Guid linkId,
            IStudioService studioService,
            CancellationToken cancellationToken) =>
        {
            var result = await studioService.DeleteLinkAsync(user.Claims, studioId, linkId, cancellationToken);
            return result switch
            {
                StudioLinkDeleteStatus.Success => Results.NoContent(),
                StudioLinkDeleteStatus.NotFound => Results.NotFound(),
                StudioLinkDeleteStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        managementGroup.MapPost("/{studioId:guid}/logo-upload", [Authorize] async (
            ClaimsPrincipal user,
            HttpContext httpContext,
            Guid studioId,
            [FromForm] UploadStudioMediaForm request,
            IStudioService studioService,
            IStudioMediaStorage studioMediaStorage,
            CancellationToken cancellationToken) =>
        {
            return await UploadStudioMediaAsync(
                user,
                httpContext,
                studioId,
                StudioMediaRoles.Logo,
                request,
                studioService,
                studioMediaStorage,
                cancellationToken);
        }).DisableAntiforgery();

        managementGroup.MapPost("/{studioId:guid}/banner-upload", [Authorize] async (
            ClaimsPrincipal user,
            HttpContext httpContext,
            Guid studioId,
            [FromForm] UploadStudioMediaForm request,
            IStudioService studioService,
            IStudioMediaStorage studioMediaStorage,
            CancellationToken cancellationToken) =>
        {
            return await UploadStudioMediaAsync(
                user,
                httpContext,
                studioId,
                StudioMediaRoles.Banner,
                request,
                studioService,
                studioMediaStorage,
                cancellationToken);
        }).DisableAntiforgery();

        managementGroup.MapDelete("/{studioId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid studioId,
            IStudioService studioService,
            CancellationToken cancellationToken) =>
        {
            var result = await studioService.DeleteStudioAsync(user.Claims, studioId, cancellationToken);
            return result switch
            {
                StudioDeleteStatus.Success => Results.NoContent(),
                StudioDeleteStatus.NotFound => Results.NotFound(),
                StudioDeleteStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        managementGroup.MapGet("/{studioId:guid}/memberships", [Authorize] async (
            ClaimsPrincipal user,
            Guid studioId,
            IStudioService studioService,
            CancellationToken cancellationToken) =>
        {
            var result = await studioService.ListMembershipsAsync(user.Claims, studioId, cancellationToken);
            return result.Status switch
            {
                StudioMembershipListStatus.Success => Results.Ok(new StudioMembershipListResponse(result.Memberships!.Select(MapMembership).ToArray())),
                StudioMembershipListStatus.NotFound => Results.NotFound(),
                StudioMembershipListStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        managementGroup.MapPut("/{studioId:guid}/memberships/{memberKeycloakSubject}", [Authorize] async (
            ClaimsPrincipal user,
            Guid studioId,
            string memberKeycloakSubject,
            UpsertStudioMembershipRequest request,
            IStudioService studioService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateMembershipRequest(memberKeycloakSubject, request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await studioService.UpsertMembershipAsync(
                user.Claims,
                studioId,
                new UpsertStudioMembershipCommand(memberKeycloakSubject.Trim(), request.Role.Trim().ToLowerInvariant()),
                cancellationToken);

            return result.Status switch
            {
                StudioMembershipMutationStatus.Success => Results.Ok(new StudioMembershipResponse(MapMembership(result.Membership!))),
                StudioMembershipMutationStatus.NotFound => Results.NotFound(),
                StudioMembershipMutationStatus.Forbidden => Results.Forbid(),
                StudioMembershipMutationStatus.TargetUserNotFound => CreateProblemResult(
                    StatusCodes.Status404NotFound,
                    "Target user not found",
                    "No local user projection exists for the supplied Keycloak subject.",
                    "studio_member_target_not_found"),
                StudioMembershipMutationStatus.Conflict => CreateProblemResult(
                    StatusCodes.Status409Conflict,
                    "Studio owner conflict",
                    "The last studio owner cannot be removed or downgraded.",
                    "studio_last_owner_conflict"),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        managementGroup.MapDelete("/{studioId:guid}/memberships/{memberKeycloakSubject}", [Authorize] async (
            ClaimsPrincipal user,
            Guid studioId,
            string memberKeycloakSubject,
            IStudioService studioService,
            CancellationToken cancellationToken) =>
        {
            var result = await studioService.DeleteMembershipAsync(
                user.Claims,
                studioId,
                memberKeycloakSubject.Trim(),
                cancellationToken);

            return result switch
            {
                StudioMembershipDeleteStatus.Success => Results.NoContent(),
                StudioMembershipDeleteStatus.NotFound => Results.NotFound(),
                StudioMembershipDeleteStatus.Forbidden => Results.Forbid(),
                StudioMembershipDeleteStatus.Conflict => CreateProblemResult(
                    StatusCodes.Status409Conflict,
                    "Studio owner conflict",
                    "The last studio owner cannot be removed or downgraded.",
                    "studio_last_owner_conflict"),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        return app;
    }

    private static StudioDto MapStudio(StudioSummarySnapshot studio) =>
        new(
            studio.Id,
            studio.Slug,
            studio.DisplayName,
            studio.Description,
            studio.LogoUrl,
            studio.BannerUrl,
            studio.Links.Select(MapLink).ToArray(),
            null,
            null);

    private static StudioDto MapStudio(StudioSnapshot studio) =>
        new(
            studio.Id,
            studio.Slug,
            studio.DisplayName,
            studio.Description,
            studio.LogoUrl,
            studio.BannerUrl,
            studio.Links.Select(MapLink).ToArray(),
            studio.CreatedAtUtc,
            studio.UpdatedAtUtc);

    private static DeveloperStudioDto MapDeveloperStudio(DeveloperStudioSummarySnapshot studio) =>
        new(
            studio.Id,
            studio.Slug,
            studio.DisplayName,
            studio.Description,
            studio.LogoUrl,
            studio.BannerUrl,
            studio.Links.Select(MapLink).ToArray(),
            studio.Role);

    private static StudioLinkDto MapLink(StudioLinkSnapshot link) =>
        new(
            link.Id,
            link.Label,
            link.Url,
            link.CreatedAtUtc,
            link.UpdatedAtUtc);

    private static StudioMembershipDto MapMembership(StudioMembershipSnapshot membership) =>
        new(
            membership.StudioId,
            membership.KeycloakSubject,
            membership.DisplayName,
            membership.Email,
            membership.Role,
            membership.JoinedAtUtc,
            membership.UpdatedAtUtc);

    private static Dictionary<string, string[]> ValidateStudioRequest(StudioRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(request.Slug))
        {
            errors["slug"] = ["Slug is required."];
        }
        else if (!SlugRegex.IsMatch(NormalizeSlug(request.Slug)))
        {
            errors["slug"] = ["Slug must contain only lowercase letters, numbers, and single hyphen separators."];
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors["displayName"] = ["Display name is required."];
        }

        if (!string.IsNullOrWhiteSpace(request.LogoUrl) &&
            !Uri.TryCreate(request.LogoUrl, UriKind.Absolute, out _))
        {
            errors["logoUrl"] = ["Logo URL must be an absolute URI."];
        }

        if (!string.IsNullOrWhiteSpace(request.BannerUrl) &&
            !Uri.TryCreate(request.BannerUrl, UriKind.Absolute, out _))
        {
            errors["bannerUrl"] = ["Banner URL must be an absolute URI."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateStudioLinkRequest(UpsertStudioLinkRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            errors["label"] = ["Link label is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Url))
        {
            errors["url"] = ["Link URL is required."];
        }
        else if (!Uri.TryCreate(request.Url.Trim(), UriKind.Absolute, out _))
        {
            errors["url"] = ["Link URL must be an absolute URI."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateMembershipRequest(
        string memberKeycloakSubject,
        UpsertStudioMembershipRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(memberKeycloakSubject))
        {
            errors["memberKeycloakSubject"] = ["Member Keycloak subject is required."];
        }

        var normalizedRole = request.Role?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedRole) ||
            normalizedRole is not (StudioRoles.Owner or StudioRoles.Admin or StudioRoles.Editor))
        {
            errors["role"] = ["Role must be one of: owner, admin, editor."];
        }

        return errors;
    }

    private static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();

    private static async Task<IResult> UploadStudioMediaAsync(
        ClaimsPrincipal user,
        HttpContext httpContext,
        Guid studioId,
        string mediaRole,
        UploadStudioMediaForm request,
        IStudioService studioService,
        IStudioMediaStorage studioMediaStorage,
        CancellationToken cancellationToken)
    {
        if (request.Media is null)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["media"] = ["Media image is required."]
                },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        if (request.Media.Length <= 0)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["media"] = ["Media image cannot be empty."]
                },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        if (request.Media.Length > MaxStudioMediaUploadBytes)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["media"] = [$"Media image size must be {MaxStudioMediaUploadBytes / 1024 / 1024} MB or less."]
                },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        var contentType = string.IsNullOrWhiteSpace(request.Media.ContentType)
            ? string.Empty
            : request.Media.ContentType.Trim().ToLowerInvariant();
        if (!SupportedStudioMediaContentTypes.Contains(contentType))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["media"] = ["Media image format must be JPEG, PNG, WEBP, GIF, or SVG."]
                },
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }

        await using var mediaStream = request.Media.OpenReadStream();
        using var memoryStream = new MemoryStream();
        await mediaStream.CopyToAsync(memoryStream, cancellationToken);

        var routePath = await studioMediaStorage.SaveStudioMediaAsync(
            studioId,
            mediaRole,
            contentType,
            memoryStream.ToArray(),
            cancellationToken);
        var sourceUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{routePath}";

        var result = await studioService.SetStudioMediaUrlAsync(
            user.Claims,
            studioId,
            mediaRole,
            sourceUrl,
            cancellationToken);

        return result.Status switch
        {
            StudioMutationStatus.Success => Results.Ok(new StudioResponse(MapStudio(result.Studio!))),
            StudioMutationStatus.NotFound => Results.NotFound(),
            StudioMutationStatus.Forbidden => Results.Forbid(),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };
    }

    private static IResult CreateProblemResult(int statusCode, string title, string detail, string code) =>
        Results.Json(
            new StudioProblemEnvelope(
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
/// Shared studio create/update request contract.
/// </summary>
internal interface StudioRequest
{
    string Slug { get; }

    string DisplayName { get; }

    string? Description { get; }

    string? LogoUrl { get; }

    string? BannerUrl { get; }
}

/// <summary>
/// Request payload for creating a studio.
/// </summary>
/// <param name="Slug">Human-readable unique route key.</param>
/// <param name="DisplayName">Public display name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
/// <param name="BannerUrl">Optional public banner URL.</param>
internal sealed record CreateStudioRequest(string Slug, string DisplayName, string? Description, string? LogoUrl, string? BannerUrl) : StudioRequest;

/// <summary>
/// Request payload for updating a studio.
/// </summary>
/// <param name="Slug">Human-readable unique route key.</param>
/// <param name="DisplayName">Public display name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
/// <param name="BannerUrl">Optional public banner URL.</param>
internal sealed record UpdateStudioRequest(string Slug, string DisplayName, string? Description, string? LogoUrl, string? BannerUrl) : StudioRequest;

/// <summary>
/// Request payload for creating or updating a studio link.
/// </summary>
/// <param name="Label">Player-facing label for the link.</param>
/// <param name="Url">Absolute destination URL.</param>
internal sealed record UpsertStudioLinkRequest(string Label, string Url);

/// <summary>
/// Form payload for uploading studio logo/banner media.
/// </summary>
internal sealed class UploadStudioMediaForm
{
    /// <summary>
    /// Gets or sets the uploaded studio media file.
    /// </summary>
    public IFormFile? Media { get; set; }
}

/// <summary>
/// Request payload for adding or changing a studio membership.
/// </summary>
/// <param name="Role">Studio-scoped role.</param>
internal sealed record UpsertStudioMembershipRequest(string Role);

/// <summary>
/// Public studio DTO.
/// </summary>
/// <param name="Id">Studio identifier.</param>
/// <param name="Slug">Human-readable unique route key.</param>
/// <param name="DisplayName">Public display name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
/// <param name="BannerUrl">Optional public banner URL.</param>
/// <param name="Links">Configured public studio links.</param>
/// <param name="CreatedAt">UTC timestamp when the studio was created.</param>
/// <param name="UpdatedAt">UTC timestamp when the studio was last updated.</param>
internal sealed record StudioDto(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    string? LogoUrl,
    string? BannerUrl,
    IReadOnlyList<StudioLinkDto> Links,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Response wrapper for studio lists.
/// </summary>
/// <param name="Studios">Studios visible to the caller.</param>
internal sealed record StudioListResponse(IReadOnlyList<StudioDto> Studios);

/// <summary>
/// Developer-visible studio summary DTO.
/// </summary>
/// <param name="Id">Studio identifier.</param>
/// <param name="Slug">Human-readable unique route key.</param>
/// <param name="DisplayName">Public display name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
/// <param name="BannerUrl">Optional public banner URL.</param>
/// <param name="Links">Configured public studio links.</param>
/// <param name="Role">Caller membership role within the studio.</param>
internal sealed record DeveloperStudioDto(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    string? LogoUrl,
    string? BannerUrl,
    IReadOnlyList<StudioLinkDto> Links,
    string Role);

/// <summary>
/// Response wrapper for developer-visible studio lists.
/// </summary>
/// <param name="Studios">Studios the caller can manage.</param>
internal sealed record DeveloperStudioListResponse(IReadOnlyList<DeveloperStudioDto> Studios);

/// <summary>
/// Response wrapper for a studio.
/// </summary>
/// <param name="Studio">Studio details.</param>
internal sealed record StudioResponse(StudioDto Studio);

/// <summary>
/// Studio link DTO.
/// </summary>
/// <param name="Id">Studio link identifier.</param>
/// <param name="Label">Player-facing label.</param>
/// <param name="Url">Absolute destination URL.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="UpdatedAt">UTC update timestamp.</param>
internal sealed record StudioLinkDto(
    Guid Id,
    string Label,
    string Url,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Response wrapper for studio links.
/// </summary>
/// <param name="Links">Studio links visible to the caller.</param>
internal sealed record StudioLinkListResponse(IReadOnlyList<StudioLinkDto> Links);

/// <summary>
/// Response wrapper for a studio link.
/// </summary>
/// <param name="Link">Studio link details.</param>
internal sealed record StudioLinkResponse(StudioLinkDto Link);

/// <summary>
/// Studio membership DTO.
/// </summary>
/// <param name="StudioId">Studio identifier.</param>
/// <param name="KeycloakSubject">Member Keycloak subject.</param>
/// <param name="DisplayName">Cached display name for the member.</param>
/// <param name="Email">Cached email for the member.</param>
/// <param name="Role">Studio-scoped role.</param>
/// <param name="JoinedAt">UTC timestamp when the membership was created.</param>
/// <param name="UpdatedAt">UTC timestamp when the membership was last updated.</param>
internal sealed record StudioMembershipDto(
    Guid StudioId,
    string KeycloakSubject,
    string? DisplayName,
    string? Email,
    string Role,
    DateTime JoinedAt,
    DateTime UpdatedAt);

/// <summary>
/// Response wrapper for studio memberships.
/// </summary>
/// <param name="Memberships">Memberships visible to the caller.</param>
internal sealed record StudioMembershipListResponse(IReadOnlyList<StudioMembershipDto> Memberships);

/// <summary>
/// Response wrapper for a studio membership.
/// </summary>
/// <param name="Membership">Studio membership details.</param>
internal sealed record StudioMembershipResponse(StudioMembershipDto Membership);

/// <summary>
/// Problem-details envelope used by studio endpoints.
/// </summary>
/// <param name="Type">Problem type URI.</param>
/// <param name="Title">Problem title.</param>
/// <param name="Status">HTTP status code.</param>
/// <param name="Detail">Problem detail message.</param>
/// <param name="Code">Machine-readable problem code.</param>
internal sealed record StudioProblemEnvelope(string? Type, string Title, int Status, string? Detail, string? Code);
