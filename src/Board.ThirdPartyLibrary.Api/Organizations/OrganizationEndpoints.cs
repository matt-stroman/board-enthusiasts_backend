using System.Security.Claims;
using System.Text.RegularExpressions;
using Board.ThirdPartyLibrary.Api.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Board.ThirdPartyLibrary.Api.Organizations;

/// <summary>
/// Maps organization and membership endpoints.
/// </summary>
internal static partial class OrganizationEndpoints
{
    private static readonly Regex SlugRegex = SlugPattern();

    /// <summary>
    /// Maps organization endpoints to the application.
    /// </summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapOrganizationEndpoints(this IEndpointRouteBuilder app)
    {
        var publicGroup = app.MapGroup("/organizations");
        var managementGroup = app.MapGroup("/developer/organizations");

        publicGroup.MapGet("/", async (
            IOrganizationService organizationService,
            CancellationToken cancellationToken) =>
        {
            var organizations = await organizationService.ListOrganizationsAsync(cancellationToken);
            return Results.Ok(new OrganizationListResponse(organizations.Select(MapOrganization).ToArray()));
        });

        publicGroup.MapGet("/{slug}", async (
            string slug,
            IOrganizationService organizationService,
            CancellationToken cancellationToken) =>
        {
            var organization = await organizationService.GetOrganizationBySlugAsync(slug.Trim(), cancellationToken);
            return organization is null
                ? Results.NotFound()
                : Results.Ok(new OrganizationResponse(MapOrganization(organization)));
        });

        managementGroup.MapGet("/", [Authorize] async (
            ClaimsPrincipal user,
            IOrganizationService organizationService,
            CancellationToken cancellationToken) =>
        {
            var organizations = await organizationService.ListManagedOrganizationsAsync(user.Claims, cancellationToken);
            return Results.Ok(new DeveloperOrganizationListResponse(organizations.Select(MapDeveloperOrganization).ToArray()));
        });

        publicGroup.MapPost("/", [Authorize] async (
            ClaimsPrincipal user,
            CreateOrganizationRequest request,
            IDeveloperEnrollmentService developerEnrollmentService,
            IOrganizationService organizationService,
            CancellationToken cancellationToken) =>
        {
            if (!await developerEnrollmentService.HasDeveloperAccessAsync(user.Claims, cancellationToken))
            {
                return Results.Forbid();
            }

            var validationErrors = ValidateOrganizationRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await organizationService.CreateOrganizationAsync(
                user.Claims,
                new CreateOrganizationCommand(
                    NormalizeSlug(request.Slug),
                    request.DisplayName.Trim(),
                    request.Description?.Trim(),
                    request.LogoUrl?.Trim()),
                cancellationToken);

            return result.Status switch
            {
                OrganizationMutationStatus.Success => Results.Created(
                    $"/organizations/{result.Organization!.Slug}",
                    new OrganizationResponse(MapOrganization(result.Organization))),
                OrganizationMutationStatus.Conflict => CreateProblemResult(
                    StatusCodes.Status409Conflict,
                    "Organization already exists",
                    "The supplied organization slug is already in use.",
                    "organization_slug_conflict"),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        managementGroup.MapPut("/{organizationId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid organizationId,
            UpdateOrganizationRequest request,
            IOrganizationService organizationService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateOrganizationRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await organizationService.UpdateOrganizationAsync(
                user.Claims,
                organizationId,
                new UpdateOrganizationCommand(
                    NormalizeSlug(request.Slug),
                    request.DisplayName.Trim(),
                    request.Description?.Trim(),
                    request.LogoUrl?.Trim()),
                cancellationToken);

            return result.Status switch
            {
                OrganizationMutationStatus.Success => Results.Ok(new OrganizationResponse(MapOrganization(result.Organization!))),
                OrganizationMutationStatus.NotFound => Results.NotFound(),
                OrganizationMutationStatus.Forbidden => Results.Forbid(),
                OrganizationMutationStatus.Conflict => CreateProblemResult(
                    StatusCodes.Status409Conflict,
                    "Organization already exists",
                    "The supplied organization slug is already in use.",
                    "organization_slug_conflict"),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        managementGroup.MapDelete("/{organizationId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid organizationId,
            IOrganizationService organizationService,
            CancellationToken cancellationToken) =>
        {
            var result = await organizationService.DeleteOrganizationAsync(user.Claims, organizationId, cancellationToken);
            return result switch
            {
                OrganizationDeleteStatus.Success => Results.NoContent(),
                OrganizationDeleteStatus.NotFound => Results.NotFound(),
                OrganizationDeleteStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        managementGroup.MapGet("/{organizationId:guid}/memberships", [Authorize] async (
            ClaimsPrincipal user,
            Guid organizationId,
            IOrganizationService organizationService,
            CancellationToken cancellationToken) =>
        {
            var result = await organizationService.ListMembershipsAsync(user.Claims, organizationId, cancellationToken);
            return result.Status switch
            {
                OrganizationMembershipListStatus.Success => Results.Ok(new OrganizationMembershipListResponse(result.Memberships!.Select(MapMembership).ToArray())),
                OrganizationMembershipListStatus.NotFound => Results.NotFound(),
                OrganizationMembershipListStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        managementGroup.MapPut("/{organizationId:guid}/memberships/{memberKeycloakSubject}", [Authorize] async (
            ClaimsPrincipal user,
            Guid organizationId,
            string memberKeycloakSubject,
            UpsertOrganizationMembershipRequest request,
            IOrganizationService organizationService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateMembershipRequest(memberKeycloakSubject, request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await organizationService.UpsertMembershipAsync(
                user.Claims,
                organizationId,
                new UpsertOrganizationMembershipCommand(memberKeycloakSubject.Trim(), request.Role.Trim().ToLowerInvariant()),
                cancellationToken);

            return result.Status switch
            {
                OrganizationMembershipMutationStatus.Success => Results.Ok(new OrganizationMembershipResponse(MapMembership(result.Membership!))),
                OrganizationMembershipMutationStatus.NotFound => Results.NotFound(),
                OrganizationMembershipMutationStatus.Forbidden => Results.Forbid(),
                OrganizationMembershipMutationStatus.TargetUserNotFound => CreateProblemResult(
                    StatusCodes.Status404NotFound,
                    "Target user not found",
                    "No local user projection exists for the supplied Keycloak subject.",
                    "organization_member_target_not_found"),
                OrganizationMembershipMutationStatus.Conflict => CreateProblemResult(
                    StatusCodes.Status409Conflict,
                    "Organization owner conflict",
                    "The last organization owner cannot be removed or downgraded.",
                    "organization_last_owner_conflict"),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        managementGroup.MapDelete("/{organizationId:guid}/memberships/{memberKeycloakSubject}", [Authorize] async (
            ClaimsPrincipal user,
            Guid organizationId,
            string memberKeycloakSubject,
            IOrganizationService organizationService,
            CancellationToken cancellationToken) =>
        {
            var result = await organizationService.DeleteMembershipAsync(
                user.Claims,
                organizationId,
                memberKeycloakSubject.Trim(),
                cancellationToken);

            return result switch
            {
                OrganizationMembershipDeleteStatus.Success => Results.NoContent(),
                OrganizationMembershipDeleteStatus.NotFound => Results.NotFound(),
                OrganizationMembershipDeleteStatus.Forbidden => Results.Forbid(),
                OrganizationMembershipDeleteStatus.Conflict => CreateProblemResult(
                    StatusCodes.Status409Conflict,
                    "Organization owner conflict",
                    "The last organization owner cannot be removed or downgraded.",
                    "organization_last_owner_conflict"),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        return app;
    }

    private static OrganizationDto MapOrganization(OrganizationSummarySnapshot organization) =>
        new(
            organization.Id,
            organization.Slug,
            organization.DisplayName,
            organization.Description,
            organization.LogoUrl,
            null,
            null);

    private static OrganizationDto MapOrganization(OrganizationSnapshot organization) =>
        new(
            organization.Id,
            organization.Slug,
            organization.DisplayName,
            organization.Description,
            organization.LogoUrl,
            organization.CreatedAtUtc,
            organization.UpdatedAtUtc);

    private static DeveloperOrganizationDto MapDeveloperOrganization(DeveloperOrganizationSummarySnapshot organization) =>
        new(
            organization.Id,
            organization.Slug,
            organization.DisplayName,
            organization.Description,
            organization.LogoUrl,
            organization.Role);

    private static OrganizationMembershipDto MapMembership(OrganizationMembershipSnapshot membership) =>
        new(
            membership.OrganizationId,
            membership.KeycloakSubject,
            membership.DisplayName,
            membership.Email,
            membership.Role,
            membership.JoinedAtUtc,
            membership.UpdatedAtUtc);

    private static Dictionary<string, string[]> ValidateOrganizationRequest(OrganizationRequest request)
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

        return errors;
    }

    private static Dictionary<string, string[]> ValidateMembershipRequest(
        string memberKeycloakSubject,
        UpsertOrganizationMembershipRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(memberKeycloakSubject))
        {
            errors["memberKeycloakSubject"] = ["Member Keycloak subject is required."];
        }

        var normalizedRole = request.Role?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedRole) ||
            normalizedRole is not (OrganizationRoles.Owner or OrganizationRoles.Admin or OrganizationRoles.Editor))
        {
            errors["role"] = ["Role must be one of: owner, admin, editor."];
        }

        return errors;
    }

    private static string NormalizeSlug(string slug) => slug.Trim().ToLowerInvariant();

    private static IResult CreateProblemResult(int statusCode, string title, string detail, string code) =>
        Results.Json(
            new OrganizationProblemEnvelope(
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
/// Shared organization create/update request contract.
/// </summary>
internal interface OrganizationRequest
{
    string Slug { get; }

    string DisplayName { get; }

    string? Description { get; }

    string? LogoUrl { get; }
}

/// <summary>
/// Request payload for creating an organization.
/// </summary>
/// <param name="Slug">Human-readable unique route key.</param>
/// <param name="DisplayName">Public display name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
internal sealed record CreateOrganizationRequest(string Slug, string DisplayName, string? Description, string? LogoUrl) : OrganizationRequest;

/// <summary>
/// Request payload for updating an organization.
/// </summary>
/// <param name="Slug">Human-readable unique route key.</param>
/// <param name="DisplayName">Public display name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
internal sealed record UpdateOrganizationRequest(string Slug, string DisplayName, string? Description, string? LogoUrl) : OrganizationRequest;

/// <summary>
/// Request payload for adding or changing an organization membership.
/// </summary>
/// <param name="Role">Organization-scoped role.</param>
internal sealed record UpsertOrganizationMembershipRequest(string Role);

/// <summary>
/// Public organization DTO.
/// </summary>
/// <param name="Id">Organization identifier.</param>
/// <param name="Slug">Human-readable unique route key.</param>
/// <param name="DisplayName">Public display name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
/// <param name="CreatedAt">UTC timestamp when the organization was created.</param>
/// <param name="UpdatedAt">UTC timestamp when the organization was last updated.</param>
internal sealed record OrganizationDto(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    string? LogoUrl,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

/// <summary>
/// Response wrapper for organization lists.
/// </summary>
/// <param name="Organizations">Organizations visible to the caller.</param>
internal sealed record OrganizationListResponse(IReadOnlyList<OrganizationDto> Organizations);

/// <summary>
/// Developer-visible organization summary DTO.
/// </summary>
/// <param name="Id">Organization identifier.</param>
/// <param name="Slug">Human-readable unique route key.</param>
/// <param name="DisplayName">Public display name.</param>
/// <param name="Description">Optional public description.</param>
/// <param name="LogoUrl">Optional public logo URL.</param>
/// <param name="Role">Caller membership role within the organization.</param>
internal sealed record DeveloperOrganizationDto(
    Guid Id,
    string Slug,
    string DisplayName,
    string? Description,
    string? LogoUrl,
    string Role);

/// <summary>
/// Response wrapper for developer-visible organization lists.
/// </summary>
/// <param name="Organizations">Organizations the caller can manage.</param>
internal sealed record DeveloperOrganizationListResponse(IReadOnlyList<DeveloperOrganizationDto> Organizations);

/// <summary>
/// Response wrapper for an organization.
/// </summary>
/// <param name="Organization">Organization details.</param>
internal sealed record OrganizationResponse(OrganizationDto Organization);

/// <summary>
/// Organization membership DTO.
/// </summary>
/// <param name="OrganizationId">Organization identifier.</param>
/// <param name="KeycloakSubject">Member Keycloak subject.</param>
/// <param name="DisplayName">Cached display name for the member.</param>
/// <param name="Email">Cached email for the member.</param>
/// <param name="Role">Organization-scoped role.</param>
/// <param name="JoinedAt">UTC timestamp when the membership was created.</param>
/// <param name="UpdatedAt">UTC timestamp when the membership was last updated.</param>
internal sealed record OrganizationMembershipDto(
    Guid OrganizationId,
    string KeycloakSubject,
    string? DisplayName,
    string? Email,
    string Role,
    DateTime JoinedAt,
    DateTime UpdatedAt);

/// <summary>
/// Response wrapper for organization memberships.
/// </summary>
/// <param name="Memberships">Memberships visible to the caller.</param>
internal sealed record OrganizationMembershipListResponse(IReadOnlyList<OrganizationMembershipDto> Memberships);

/// <summary>
/// Response wrapper for an organization membership.
/// </summary>
/// <param name="Membership">Organization membership details.</param>
internal sealed record OrganizationMembershipResponse(OrganizationMembershipDto Membership);

/// <summary>
/// Problem-details envelope used by organization endpoints.
/// </summary>
/// <param name="Type">Problem type URI.</param>
/// <param name="Title">Problem title.</param>
/// <param name="Status">HTTP status code.</param>
/// <param name="Detail">Problem detail message.</param>
/// <param name="Code">Machine-readable problem code.</param>
internal sealed record OrganizationProblemEnvelope(string? Type, string Title, int Status, string? Detail, string? Code);
