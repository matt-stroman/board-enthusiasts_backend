using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;

namespace Board.ThirdPartyLibrary.Api.Acquisition;

/// <summary>
/// Maps supported publisher, integration connection, and title acquisition binding endpoints.
/// </summary>
internal static class AcquisitionEndpoints
{
    /// <summary>
    /// Maps acquisition endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapAcquisitionEndpoints(this IEndpointRouteBuilder app)
    {
        var publicGroup = app.MapGroup(string.Empty);
        var developerOrganizationGroup = app.MapGroup("/developer/organizations");
        var developerTitleGroup = app.MapGroup("/developer/titles");

        publicGroup.MapGet("/supported-publishers", async (
            IAcquisitionService acquisitionService,
            CancellationToken cancellationToken) =>
        {
            var publishers = await acquisitionService.ListSupportedPublishersAsync(cancellationToken);
            return Results.Ok(new SupportedPublisherListResponse(publishers.Select(MapSupportedPublisher).ToArray()));
        });

        developerOrganizationGroup.MapGet("/{organizationId:guid}/integration-connections", [Authorize] async (
            ClaimsPrincipal user,
            Guid organizationId,
            IAcquisitionService acquisitionService,
            CancellationToken cancellationToken) =>
        {
            var result = await acquisitionService.ListIntegrationConnectionsAsync(user.Claims, organizationId, cancellationToken);
            return result.Status switch
            {
                AcquisitionListStatus.Success => Results.Ok(
                    new IntegrationConnectionListResponse(result.IntegrationConnections!.Select(MapIntegrationConnection).ToArray())),
                AcquisitionListStatus.NotFound => Results.NotFound(),
                AcquisitionListStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerOrganizationGroup.MapPost("/{organizationId:guid}/integration-connections", [Authorize] async (
            ClaimsPrincipal user,
            Guid organizationId,
            UpsertIntegrationConnectionRequest request,
            IAcquisitionService acquisitionService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateIntegrationConnectionRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await acquisitionService.CreateIntegrationConnectionAsync(
                user.Claims,
                organizationId,
                MapIntegrationConnectionCommand(request),
                cancellationToken);

            return result.Status switch
            {
                AcquisitionMutationStatus.Success => Results.Created(
                    $"/developer/organizations/{organizationId}/integration-connections/{result.IntegrationConnection!.Id}",
                    new IntegrationConnectionResponse(MapIntegrationConnection(result.IntegrationConnection))),
                AcquisitionMutationStatus.NotFound => Results.NotFound(),
                AcquisitionMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerOrganizationGroup.MapGet("/{organizationId:guid}/integration-connections/{connectionId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid organizationId,
            Guid connectionId,
            IAcquisitionService acquisitionService,
            CancellationToken cancellationToken) =>
        {
            var result = await acquisitionService.GetIntegrationConnectionAsync(user.Claims, organizationId, connectionId, cancellationToken);
            return result.Status switch
            {
                AcquisitionMutationStatus.Success => Results.Ok(
                    new IntegrationConnectionResponse(MapIntegrationConnection(result.IntegrationConnection!))),
                AcquisitionMutationStatus.NotFound => Results.NotFound(),
                AcquisitionMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerOrganizationGroup.MapPut("/{organizationId:guid}/integration-connections/{connectionId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid organizationId,
            Guid connectionId,
            UpsertIntegrationConnectionRequest request,
            IAcquisitionService acquisitionService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateIntegrationConnectionRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await acquisitionService.UpdateIntegrationConnectionAsync(
                user.Claims,
                organizationId,
                connectionId,
                MapIntegrationConnectionCommand(request),
                cancellationToken);

            return result.Status switch
            {
                AcquisitionMutationStatus.Success => Results.Ok(
                    new IntegrationConnectionResponse(MapIntegrationConnection(result.IntegrationConnection!))),
                AcquisitionMutationStatus.NotFound => Results.NotFound(),
                AcquisitionMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerOrganizationGroup.MapDelete("/{organizationId:guid}/integration-connections/{connectionId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid organizationId,
            Guid connectionId,
            IAcquisitionService acquisitionService,
            CancellationToken cancellationToken) =>
        {
            var result = await acquisitionService.DeleteIntegrationConnectionAsync(user.Claims, organizationId, connectionId, cancellationToken);
            return result.Status switch
            {
                AcquisitionMutationStatus.Success => Results.NoContent(),
                AcquisitionMutationStatus.NotFound => Results.NotFound(),
                AcquisitionMutationStatus.Forbidden => Results.Forbid(),
                AcquisitionMutationStatus.Conflict => CreateProblemResult(
                    StatusCodes.Status409Conflict,
                    "Integration connection in use",
                    "The integration connection is still referenced by one or more title acquisition bindings.",
                    result.ErrorCode ?? AcquisitionErrorCodes.IntegrationConnectionInUse),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapGet("/{titleId:guid}/integration-bindings", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            IAcquisitionService acquisitionService,
            CancellationToken cancellationToken) =>
        {
            var result = await acquisitionService.ListTitleIntegrationBindingsAsync(user.Claims, titleId, cancellationToken);
            return result.Status switch
            {
                AcquisitionListStatus.Success => Results.Ok(
                    new TitleIntegrationBindingListResponse(result.IntegrationBindings!.Select(MapTitleIntegrationBinding).ToArray())),
                AcquisitionListStatus.NotFound => Results.NotFound(),
                AcquisitionListStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapPost("/{titleId:guid}/integration-bindings", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            UpsertTitleIntegrationBindingRequest request,
            IAcquisitionService acquisitionService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateTitleIntegrationBindingRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await acquisitionService.CreateTitleIntegrationBindingAsync(
                user.Claims,
                titleId,
                MapTitleIntegrationBindingCommand(request),
                cancellationToken);

            return result.Status switch
            {
                AcquisitionMutationStatus.Success => Results.Created(
                    $"/developer/titles/{titleId}/integration-bindings/{result.IntegrationBinding!.Id}",
                    new TitleIntegrationBindingResponse(MapTitleIntegrationBinding(result.IntegrationBinding))),
                AcquisitionMutationStatus.NotFound => Results.NotFound(),
                AcquisitionMutationStatus.Forbidden => Results.Forbid(),
                AcquisitionMutationStatus.Conflict => CreateConflictResult(result.ErrorCode),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapGet("/{titleId:guid}/integration-bindings/{bindingId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            Guid bindingId,
            IAcquisitionService acquisitionService,
            CancellationToken cancellationToken) =>
        {
            var result = await acquisitionService.GetTitleIntegrationBindingAsync(user.Claims, titleId, bindingId, cancellationToken);
            return result.Status switch
            {
                AcquisitionMutationStatus.Success => Results.Ok(
                    new TitleIntegrationBindingResponse(MapTitleIntegrationBinding(result.IntegrationBinding!))),
                AcquisitionMutationStatus.NotFound => Results.NotFound(),
                AcquisitionMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapPut("/{titleId:guid}/integration-bindings/{bindingId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            Guid bindingId,
            UpsertTitleIntegrationBindingRequest request,
            IAcquisitionService acquisitionService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateTitleIntegrationBindingRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await acquisitionService.UpdateTitleIntegrationBindingAsync(
                user.Claims,
                titleId,
                bindingId,
                MapTitleIntegrationBindingCommand(request),
                cancellationToken);

            return result.Status switch
            {
                AcquisitionMutationStatus.Success => Results.Ok(
                    new TitleIntegrationBindingResponse(MapTitleIntegrationBinding(result.IntegrationBinding!))),
                AcquisitionMutationStatus.NotFound => Results.NotFound(),
                AcquisitionMutationStatus.Forbidden => Results.Forbid(),
                AcquisitionMutationStatus.Conflict => CreateConflictResult(result.ErrorCode),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapDelete("/{titleId:guid}/integration-bindings/{bindingId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            Guid bindingId,
            IAcquisitionService acquisitionService,
            CancellationToken cancellationToken) =>
        {
            var result = await acquisitionService.DeleteTitleIntegrationBindingAsync(user.Claims, titleId, bindingId, cancellationToken);
            return result.Status switch
            {
                AcquisitionMutationStatus.Success => Results.NoContent(),
                AcquisitionMutationStatus.NotFound => Results.NotFound(),
                AcquisitionMutationStatus.Forbidden => Results.Forbid(),
                AcquisitionMutationStatus.Conflict => CreateConflictResult(result.ErrorCode),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        return app;
    }

    private static Dictionary<string, string[]> ValidateIntegrationConnectionRequest(UpsertIntegrationConnectionRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var hasSupportedPublisher = request.SupportedPublisherId is not null && request.SupportedPublisherId != Guid.Empty;
        var hasCustomPublisherDisplayName = !string.IsNullOrWhiteSpace(request.CustomPublisherDisplayName);
        var hasCustomPublisherHomepageUrl = !string.IsNullOrWhiteSpace(request.CustomPublisherHomepageUrl);

        if (hasSupportedPublisher && (hasCustomPublisherDisplayName || hasCustomPublisherHomepageUrl))
        {
            errors["publisher"] = ["Choose either a supported publisher or custom publisher details, but not both."];
        }
        else if (!hasSupportedPublisher && !hasCustomPublisherDisplayName && !hasCustomPublisherHomepageUrl)
        {
            errors["publisher"] = ["Choose a supported publisher or provide custom publisher details."];
        }
        else if (!hasSupportedPublisher)
        {
            if (!hasCustomPublisherDisplayName)
            {
                errors["customPublisherDisplayName"] = ["Custom publisher display name is required when using a custom publisher."];
            }

            if (!hasCustomPublisherHomepageUrl)
            {
                errors["customPublisherHomepageUrl"] = ["Custom publisher homepage URL is required when using a custom publisher."];
            }
            else if (!TryCreateHttpsUri(request.CustomPublisherHomepageUrl!, out _))
            {
                errors["customPublisherHomepageUrl"] = ["Custom publisher homepage URL must be an absolute https URL."];
            }
        }

        if (request.Configuration is not null &&
            request.Configuration.Value.ValueKind is not JsonValueKind.Object and not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            errors["configuration"] = ["Configuration must be a JSON object when provided."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateTitleIntegrationBindingRequest(UpsertTitleIntegrationBindingRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request.IntegrationConnectionId == Guid.Empty)
        {
            errors["integrationConnectionId"] = ["Integration connection ID is required."];
        }

        if (!TryCreateHttpsUri(request.AcquisitionUrl, out _))
        {
            errors["acquisitionUrl"] = ["Acquisition URL must be an absolute https URL."];
        }

        if (request.IsPrimary && !request.IsEnabled)
        {
            errors["isPrimary"] = ["Primary bindings must be enabled."];
        }

        if (request.Configuration is not null &&
            request.Configuration.Value.ValueKind is not JsonValueKind.Object and not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            errors["configuration"] = ["Configuration must be a JSON object when provided."];
        }

        return errors;
    }

    private static bool TryCreateHttpsUri(string? value, out Uri? uri)
    {
        if (Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var created) && created.Scheme == Uri.UriSchemeHttps)
        {
            uri = created;
            return true;
        }

        uri = null;
        return false;
    }

    private static UpsertIntegrationConnectionCommand MapIntegrationConnectionCommand(UpsertIntegrationConnectionRequest request) =>
        new(
            request.SupportedPublisherId == Guid.Empty ? null : request.SupportedPublisherId,
            string.IsNullOrWhiteSpace(request.CustomPublisherDisplayName) ? null : request.CustomPublisherDisplayName.Trim(),
            string.IsNullOrWhiteSpace(request.CustomPublisherHomepageUrl) ? null : request.CustomPublisherHomepageUrl.Trim(),
            request.Configuration?.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : request.Configuration?.Clone(),
            request.IsEnabled);

    private static UpsertTitleIntegrationBindingCommand MapTitleIntegrationBindingCommand(UpsertTitleIntegrationBindingRequest request) =>
        new(
            request.IntegrationConnectionId,
            request.AcquisitionUrl.Trim(),
            string.IsNullOrWhiteSpace(request.AcquisitionLabel) ? null : request.AcquisitionLabel.Trim(),
            request.Configuration?.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : request.Configuration?.Clone(),
            request.IsPrimary,
            request.IsEnabled);

    private static SupportedPublisherDto MapSupportedPublisher(SupportedPublisherSnapshot publisher) =>
        new(
            publisher.Id,
            publisher.Key,
            publisher.DisplayName,
            publisher.HomepageUrl);

    private static IntegrationConnectionDto MapIntegrationConnection(IntegrationConnectionSnapshot connection) =>
        new(
            connection.Id,
            connection.OrganizationId,
            connection.SupportedPublisherId,
            connection.SupportedPublisher is null ? null : MapSupportedPublisher(connection.SupportedPublisher),
            connection.CustomPublisherDisplayName,
            connection.CustomPublisherHomepageUrl,
            connection.Configuration,
            connection.IsEnabled,
            connection.CreatedAtUtc,
            connection.UpdatedAtUtc);

    private static TitleIntegrationBindingDto MapTitleIntegrationBinding(TitleIntegrationBindingSnapshot binding) =>
        new(
            binding.Id,
            binding.TitleId,
            binding.IntegrationConnectionId,
            MapIntegrationConnection(binding.IntegrationConnection),
            binding.AcquisitionUrl,
            binding.AcquisitionLabel,
            binding.Configuration,
            binding.IsPrimary,
            binding.IsEnabled,
            binding.CreatedAtUtc,
            binding.UpdatedAtUtc);

    private static IResult CreateConflictResult(string? errorCode) =>
        errorCode switch
        {
            AcquisitionErrorCodes.TitleIntegrationOrganizationConflict => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "Title integration conflict",
                "The supplied integration connection must belong to the same organization as the title.",
                errorCode),
            AcquisitionErrorCodes.TitleIntegrationConnectionDisabled => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "Integration connection disabled",
                "Enabled title acquisition bindings cannot point at disabled integration connections.",
                errorCode),
            _ => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "Primary binding required",
                "Titles with enabled acquisition bindings must keep exactly one enabled primary binding.",
                errorCode ?? AcquisitionErrorCodes.TitleIntegrationPrimaryRequired)
        };

    private static IResult CreateProblemResult(int statusCode, string title, string detail, string code) =>
        Results.Json(
            new AcquisitionProblemEnvelope(
                Type: $"https://boardtpl.dev/problems/{code.Replace('_', '-')}",
                Title: title,
                Status: statusCode,
                Detail: detail,
                Code: code),
            statusCode: statusCode);
}

internal sealed record UpsertIntegrationConnectionRequest(
    Guid? SupportedPublisherId,
    string? CustomPublisherDisplayName,
    string? CustomPublisherHomepageUrl,
    JsonElement? Configuration,
    bool IsEnabled);

internal sealed record UpsertTitleIntegrationBindingRequest(
    Guid IntegrationConnectionId,
    string AcquisitionUrl,
    string? AcquisitionLabel,
    JsonElement? Configuration,
    bool IsPrimary,
    bool IsEnabled);

internal sealed record SupportedPublisherDto(
    Guid Id,
    string Key,
    string DisplayName,
    string HomepageUrl);

internal sealed record IntegrationConnectionDto(
    Guid Id,
    Guid OrganizationId,
    Guid? SupportedPublisherId,
    SupportedPublisherDto? SupportedPublisher,
    string? CustomPublisherDisplayName,
    string? CustomPublisherHomepageUrl,
    JsonElement? Configuration,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt);

internal sealed record TitleIntegrationBindingDto(
    Guid Id,
    Guid TitleId,
    Guid IntegrationConnectionId,
    IntegrationConnectionDto IntegrationConnection,
    string AcquisitionUrl,
    string? AcquisitionLabel,
    JsonElement? Configuration,
    bool IsPrimary,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt);

internal sealed record SupportedPublisherListResponse(IReadOnlyList<SupportedPublisherDto> SupportedPublishers);

internal sealed record IntegrationConnectionListResponse(IReadOnlyList<IntegrationConnectionDto> IntegrationConnections);

internal sealed record IntegrationConnectionResponse(IntegrationConnectionDto IntegrationConnection);

internal sealed record TitleIntegrationBindingListResponse(IReadOnlyList<TitleIntegrationBindingDto> IntegrationBindings);

internal sealed record TitleIntegrationBindingResponse(TitleIntegrationBindingDto IntegrationBinding);

internal sealed record AcquisitionProblemEnvelope(string? Type, string Title, int Status, string? Detail, string? Code);
