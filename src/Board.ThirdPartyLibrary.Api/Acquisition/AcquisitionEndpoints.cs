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
        var developerStudioGroup = app.MapGroup("/developer/studios");
        var developerTitleGroup = app.MapGroup("/developer/titles");

        publicGroup.MapGet("/supported-publishers", async (
            IAcquisitionService acquisitionService,
            CancellationToken cancellationToken) =>
        {
            var publishers = await acquisitionService.ListSupportedPublishersAsync(cancellationToken);
            return Results.Ok(new SupportedPublisherListResponse(publishers.Select(MapSupportedPublisher).ToArray()));
        });

        developerStudioGroup.MapGet("/{studioId:guid}/integration-connections", [Authorize] async (
            ClaimsPrincipal user,
            Guid studioId,
            IAcquisitionService acquisitionService,
            CancellationToken cancellationToken) =>
        {
            var result = await acquisitionService.ListIntegrationConnectionsAsync(user.Claims, studioId, cancellationToken);
            return result.Status switch
            {
                AcquisitionListStatus.Success => Results.Ok(
                    new IntegrationConnectionListResponse(result.IntegrationConnections!.Select(MapIntegrationConnection).ToArray())),
                AcquisitionListStatus.NotFound => Results.NotFound(),
                AcquisitionListStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerStudioGroup.MapPost("/{studioId:guid}/integration-connections", [Authorize] async (
            ClaimsPrincipal user,
            Guid studioId,
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
                studioId,
                MapIntegrationConnectionCommand(request),
                cancellationToken);

            return result.Status switch
            {
                AcquisitionMutationStatus.Success => Results.Created(
                    $"/developer/studios/{studioId}/integration-connections/{result.IntegrationConnection!.Id}",
                    new IntegrationConnectionResponse(MapIntegrationConnection(result.IntegrationConnection))),
                AcquisitionMutationStatus.NotFound => Results.NotFound(),
                AcquisitionMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerStudioGroup.MapGet("/{studioId:guid}/integration-connections/{connectionId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid studioId,
            Guid connectionId,
            IAcquisitionService acquisitionService,
            CancellationToken cancellationToken) =>
        {
            var result = await acquisitionService.GetIntegrationConnectionAsync(user.Claims, studioId, connectionId, cancellationToken);
            return result.Status switch
            {
                AcquisitionMutationStatus.Success => Results.Ok(
                    new IntegrationConnectionResponse(MapIntegrationConnection(result.IntegrationConnection!))),
                AcquisitionMutationStatus.NotFound => Results.NotFound(),
                AcquisitionMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerStudioGroup.MapPut("/{studioId:guid}/integration-connections/{connectionId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid studioId,
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
                studioId,
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

        developerStudioGroup.MapDelete("/{studioId:guid}/integration-connections/{connectionId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid studioId,
            Guid connectionId,
            IAcquisitionService acquisitionService,
            CancellationToken cancellationToken) =>
        {
            var result = await acquisitionService.DeleteIntegrationConnectionAsync(user.Claims, studioId, connectionId, cancellationToken);
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
            connection.StudioId,
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
            AcquisitionErrorCodes.TitleIntegrationStudioConflict => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "Title integration conflict",
                "The supplied integration connection must belong to the same studio as the title.",
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

/// <summary>
/// Request payload for creating or updating a studio integration connection.
/// </summary>
/// <param name="SupportedPublisherId">Canonical supported publisher identifier when using a registry entry.</param>
/// <param name="CustomPublisherDisplayName">Custom publisher display name when using a studio-owned custom connection.</param>
/// <param name="CustomPublisherHomepageUrl">Custom publisher homepage URL when using a studio-owned custom connection.</param>
/// <param name="Configuration">Optional provider-specific non-secret configuration object.</param>
/// <param name="IsEnabled">Whether the connection can be selected for enabled bindings.</param>
internal sealed record UpsertIntegrationConnectionRequest(
    Guid? SupportedPublisherId,
    string? CustomPublisherDisplayName,
    string? CustomPublisherHomepageUrl,
    JsonElement? Configuration,
    bool IsEnabled);

/// <summary>
/// Request payload for creating or updating a title acquisition binding.
/// </summary>
/// <param name="IntegrationConnectionId">Studio-owned integration connection identifier.</param>
/// <param name="AcquisitionUrl">Player-facing external acquisition URL.</param>
/// <param name="AcquisitionLabel">Optional player-facing acquisition label.</param>
/// <param name="Configuration">Optional provider-specific non-secret configuration object.</param>
/// <param name="IsPrimary">Whether the binding is the active primary binding for the title.</param>
/// <param name="IsEnabled">Whether the binding is available to players.</param>
internal sealed record UpsertTitleIntegrationBindingRequest(
    Guid IntegrationConnectionId,
    string AcquisitionUrl,
    string? AcquisitionLabel,
    JsonElement? Configuration,
    bool IsPrimary,
    bool IsEnabled);

/// <summary>
/// Supported publisher DTO.
/// </summary>
/// <param name="Id">Supported publisher identifier.</param>
/// <param name="Key">Stable machine-friendly publisher key.</param>
/// <param name="DisplayName">Public publisher name.</param>
/// <param name="HomepageUrl">Canonical publisher homepage URL.</param>
internal sealed record SupportedPublisherDto(
    Guid Id,
    string Key,
    string DisplayName,
    string HomepageUrl);

/// <summary>
/// Integration connection DTO.
/// </summary>
/// <param name="Id">Integration connection identifier.</param>
/// <param name="StudioId">Owning studio identifier.</param>
/// <param name="SupportedPublisherId">Linked supported publisher identifier when present.</param>
/// <param name="SupportedPublisher">Canonical supported publisher details when present.</param>
/// <param name="CustomPublisherDisplayName">Custom publisher display name when using a custom connection.</param>
/// <param name="CustomPublisherHomepageUrl">Custom publisher homepage URL when using a custom connection.</param>
/// <param name="Configuration">Optional provider-specific non-secret configuration object.</param>
/// <param name="IsEnabled">Whether the connection can be selected for enabled bindings.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="UpdatedAt">UTC update timestamp.</param>
internal sealed record IntegrationConnectionDto(
    Guid Id,
    Guid StudioId,
    Guid? SupportedPublisherId,
    SupportedPublisherDto? SupportedPublisher,
    string? CustomPublisherDisplayName,
    string? CustomPublisherHomepageUrl,
    JsonElement? Configuration,
    bool IsEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Title integration binding DTO.
/// </summary>
/// <param name="Id">Binding identifier.</param>
/// <param name="TitleId">Owning title identifier.</param>
/// <param name="IntegrationConnectionId">Referenced integration connection identifier.</param>
/// <param name="IntegrationConnection">Referenced integration connection details.</param>
/// <param name="AcquisitionUrl">Player-facing external acquisition URL.</param>
/// <param name="AcquisitionLabel">Optional player-facing acquisition label.</param>
/// <param name="Configuration">Optional provider-specific non-secret configuration object.</param>
/// <param name="IsPrimary">Whether the binding is the active primary binding for the title.</param>
/// <param name="IsEnabled">Whether the binding is available to players.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="UpdatedAt">UTC update timestamp.</param>
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

/// <summary>
/// Response wrapper for supported publisher listings.
/// </summary>
/// <param name="SupportedPublishers">Supported publishers visible to the caller.</param>
internal sealed record SupportedPublisherListResponse(IReadOnlyList<SupportedPublisherDto> SupportedPublishers);

/// <summary>
/// Response wrapper for integration connection listings.
/// </summary>
/// <param name="IntegrationConnections">Integration connections visible to the caller.</param>
internal sealed record IntegrationConnectionListResponse(IReadOnlyList<IntegrationConnectionDto> IntegrationConnections);

/// <summary>
/// Response wrapper for a single integration connection.
/// </summary>
/// <param name="IntegrationConnection">Integration connection details.</param>
internal sealed record IntegrationConnectionResponse(IntegrationConnectionDto IntegrationConnection);

/// <summary>
/// Response wrapper for title acquisition binding listings.
/// </summary>
/// <param name="IntegrationBindings">Title acquisition bindings visible to the caller.</param>
internal sealed record TitleIntegrationBindingListResponse(IReadOnlyList<TitleIntegrationBindingDto> IntegrationBindings);

/// <summary>
/// Response wrapper for a single title acquisition binding.
/// </summary>
/// <param name="IntegrationBinding">Title acquisition binding details.</param>
internal sealed record TitleIntegrationBindingResponse(TitleIntegrationBindingDto IntegrationBinding);

/// <summary>
/// Problem-details envelope used by acquisition endpoints.
/// </summary>
/// <param name="Type">Problem type URI.</param>
/// <param name="Title">Problem title.</param>
/// <param name="Status">HTTP status code.</param>
/// <param name="Detail">Problem detail message.</param>
/// <param name="Code">Machine-readable problem code.</param>
internal sealed record AcquisitionProblemEnvelope(string? Type, string Title, int Status, string? Detail, string? Code);
