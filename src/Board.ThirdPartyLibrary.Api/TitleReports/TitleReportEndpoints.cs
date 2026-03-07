using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Players;
using Microsoft.AspNetCore.Authorization;

namespace Board.ThirdPartyLibrary.Api.TitleReports;

/// <summary>
/// Maps moderator and developer title-report review endpoints.
/// </summary>
internal static class TitleReportEndpoints
{
    /// <summary>
    /// Maps title-report review endpoints to the application.
    /// </summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapTitleReportEndpoints(this IEndpointRouteBuilder app)
    {
        var moderationGroup = app.MapGroup("/moderation/title-reports");
        var developerTitleGroup = app.MapGroup("/developer/titles/{titleId:guid}/reports");

        moderationGroup.MapGet("/", [Authorize] async (
            ClaimsPrincipal user,
            ITitleReportService titleReportService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleReportService.GetModerationReportsAsync(user.Claims, cancellationToken);
            return result.Status switch
            {
                TitleReportListStatus.Success => Results.Ok(new TitleReportListResponse(result.Reports!)),
                TitleReportListStatus.Forbidden => CreateModeratorProblemResult(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        moderationGroup.MapGet("/{reportId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid reportId,
            ITitleReportService titleReportService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleReportService.GetModerationReportAsync(user.Claims, reportId, cancellationToken);
            return MapMutationResult(result);
        });

        moderationGroup.MapPost("/{reportId:guid}/messages", [Authorize] async (
            ClaimsPrincipal user,
            Guid reportId,
            AddModerationTitleReportMessageRequest request,
            ITitleReportService titleReportService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateModeratorMessageRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await titleReportService.AddModeratorMessageAsync(user.Claims, reportId, request.Message.Trim(), request.RecipientRole.Trim(), cancellationToken);
            return MapMutationResult(result);
        });

        moderationGroup.MapPost("/{reportId:guid}/validate", [Authorize] async (
            ClaimsPrincipal user,
            Guid reportId,
            ModerateTitleReportDecisionRequest? request,
            ITitleReportService titleReportService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateDecisionRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await titleReportService.ValidateReportAsync(user.Claims, reportId, request?.Note?.Trim(), cancellationToken);
            return MapMutationResult(result);
        });

        moderationGroup.MapPost("/{reportId:guid}/invalidate", [Authorize] async (
            ClaimsPrincipal user,
            Guid reportId,
            ModerateTitleReportDecisionRequest? request,
            ITitleReportService titleReportService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateDecisionRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await titleReportService.InvalidateReportAsync(user.Claims, reportId, request?.Note?.Trim(), cancellationToken);
            return MapMutationResult(result);
        });

        developerTitleGroup.MapGet("/", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            ITitleReportService titleReportService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleReportService.GetDeveloperReportsAsync(user.Claims, titleId, cancellationToken);
            return result.Status switch
            {
                TitleReportListStatus.Success => Results.Ok(new TitleReportListResponse(result.Reports!)),
                TitleReportListStatus.NotFound => Results.NotFound(),
                TitleReportListStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapGet("/{reportId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            Guid reportId,
            ITitleReportService titleReportService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleReportService.GetDeveloperReportAsync(user.Claims, titleId, reportId, cancellationToken);
            return result.Status switch
            {
                TitleReportMutationStatus.Success => Results.Ok(new TitleReportDetailResponse(result.Report!)),
                TitleReportMutationStatus.NotFound => Results.NotFound(),
                TitleReportMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        developerTitleGroup.MapPost("/{reportId:guid}/messages", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            Guid reportId,
            AddTitleReportMessageRequest request,
            ITitleReportService titleReportService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateMessageRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await titleReportService.AddDeveloperMessageAsync(user.Claims, titleId, reportId, request.Message.Trim(), cancellationToken);
            return result.Status switch
            {
                TitleReportMutationStatus.Success => Results.Ok(new TitleReportDetailResponse(result.Report!)),
                TitleReportMutationStatus.NotFound => Results.NotFound(),
                TitleReportMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        var playerReportGroup = app.MapGroup("/player/reports");

        playerReportGroup.MapGet("/{reportId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid reportId,
            ITitleReportService titleReportService,
            CancellationToken cancellationToken) =>
        {
            var result = await titleReportService.GetPlayerReportAsync(user.Claims, reportId, cancellationToken);
            return result.Status switch
            {
                TitleReportMutationStatus.Success => Results.Ok(new TitleReportDetailResponse(result.Report!)),
                TitleReportMutationStatus.NotFound => Results.NotFound(),
                TitleReportMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        playerReportGroup.MapPost("/{reportId:guid}/messages", [Authorize] async (
            ClaimsPrincipal user,
            Guid reportId,
            AddTitleReportMessageRequest request,
            ITitleReportService titleReportService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateMessageRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await titleReportService.AddPlayerMessageAsync(user.Claims, reportId, request.Message.Trim(), cancellationToken);
            return result.Status switch
            {
                TitleReportMutationStatus.Success => Results.Ok(new TitleReportDetailResponse(result.Report!)),
                TitleReportMutationStatus.NotFound => Results.NotFound(),
                TitleReportMutationStatus.Forbidden => Results.Forbid(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        return app;
    }

    private static Dictionary<string, string[]> ValidateMessageRequest(AddTitleReportMessageRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            errors["message"] = ["Message is required."];
        }
        else if (request.Message.Trim().Length > 4000)
        {
            errors["message"] = ["Message cannot exceed 4000 characters."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateModeratorMessageRequest(AddModerationTitleReportMessageRequest request)
    {
        var errors = ValidateMessageRequest(new AddTitleReportMessageRequest(request.Message));
        if (string.IsNullOrWhiteSpace(request.RecipientRole))
        {
            errors["recipientRole"] = ["Recipient role is required."];
        }
        else if (!string.Equals(request.RecipientRole.Trim(), TitleReportAuthorRoles.Player, StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(request.RecipientRole.Trim(), TitleReportAuthorRoles.Developer, StringComparison.OrdinalIgnoreCase))
        {
            errors["recipientRole"] = ["Recipient role must be either player or developer."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateDecisionRequest(ModerateTitleReportDecisionRequest? request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(request?.Note) && request.Note.Trim().Length > 4000)
        {
            errors["note"] = ["Decision note cannot exceed 4000 characters."];
        }

        return errors;
    }

    private static IResult MapMutationResult(TitleReportMutationResult result) =>
        result.Status switch
        {
            TitleReportMutationStatus.Success => Results.Ok(new TitleReportDetailResponse(result.Report!)),
            TitleReportMutationStatus.NotFound => Results.NotFound(),
            TitleReportMutationStatus.Forbidden => CreateModeratorProblemResult(),
            TitleReportMutationStatus.ValidationFailed => Results.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["recipientRole"] = ["Recipient role must be either player or developer."]
                },
                statusCode: StatusCodes.Status422UnprocessableEntity),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };

    private static IResult CreateModeratorProblemResult() =>
        Results.Json(
            new TitleReportProblemEnvelope(
                "https://boardtpl.dev/problems/moderator-access-required",
                "Moderator access is required.",
                StatusCodes.Status403Forbidden,
                "Only moderators and above can manage title-report moderation workflows.",
                "moderator_access_required"),
            statusCode: StatusCodes.Status403Forbidden);
}

/// <summary>
/// Request payload for adding a title-report thread message.
/// </summary>
internal sealed record AddTitleReportMessageRequest(string Message);

/// <summary>
/// Request payload for adding a moderator-authored title-report message.
/// </summary>
internal sealed record AddModerationTitleReportMessageRequest(string Message, string RecipientRole);

/// <summary>
/// Request payload for resolving a title report.
/// </summary>
internal sealed record ModerateTitleReportDecisionRequest(string? Note);

/// <summary>
/// Title-report list response wrapper.
/// </summary>
internal sealed record TitleReportListResponse(IReadOnlyList<TitleReportSummarySnapshot> Reports);

/// <summary>
/// Title-report detail response wrapper.
/// </summary>
internal sealed record TitleReportDetailResponse(TitleReportDetailSnapshot Report);

/// <summary>
/// Problem envelope returned by title-report moderation endpoints.
/// </summary>
internal sealed record TitleReportProblemEnvelope(string? Type, string Title, int Status, string? Detail, string? Code);
