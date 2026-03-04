using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Identity;
using Microsoft.AspNetCore.Authorization;

namespace Board.ThirdPartyLibrary.Api.Moderation;

/// <summary>
/// Maps moderator-only endpoints.
/// </summary>
internal static class ModerationEndpoints
{
    /// <summary>
    /// Maps moderator-only endpoints to the application.
    /// </summary>
    public static IEndpointRouteBuilder MapModerationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/moderation");

        group.MapGet("/developer-enrollment-requests", [Authorize] async (
            ClaimsPrincipal user,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await developerEnrollmentService.ListRequestsAsync(user.Claims, cancellationToken);
            return result.Status switch
            {
                DeveloperEnrollmentRequestListStatus.Success => Results.Ok(
                    new DeveloperEnrollmentRequestListResponse(result.Requests!.Select(MapRequest).ToArray())),
                DeveloperEnrollmentRequestListStatus.Forbidden => CreateProblemResult(
                    StatusCodes.Status403Forbidden,
                    "Moderator access is required.",
                    "Only moderators can review developer enrollment requests.",
                    "moderator_access_required"),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        group.MapPost("/developer-enrollment-requests/{requestId:guid}/approve", [Authorize] async (
            ClaimsPrincipal user,
            Guid requestId,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await developerEnrollmentService.ReviewRequestAsync(
                user.Claims,
                requestId,
                DeveloperEnrollmentReviewDecision.Approve,
                cancellationToken);

            return MapReviewResult(result);
        });

        group.MapPost("/developer-enrollment-requests/{requestId:guid}/reject", [Authorize] async (
            ClaimsPrincipal user,
            Guid requestId,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await developerEnrollmentService.ReviewRequestAsync(
                user.Claims,
                requestId,
                DeveloperEnrollmentReviewDecision.Reject,
                cancellationToken);

            return MapReviewResult(result);
        });

        return app;
    }

    private static IResult MapReviewResult(DeveloperEnrollmentReviewResult result) =>
        result.Status switch
        {
            DeveloperEnrollmentReviewStatus.Success => Results.Ok(
                new DeveloperEnrollmentRequestResponse(MapRequest(result.Request!))),
            DeveloperEnrollmentReviewStatus.Forbidden => CreateProblemResult(
                StatusCodes.Status403Forbidden,
                "Moderator access is required.",
                "Only moderators can review developer enrollment requests.",
                "moderator_access_required"),
            DeveloperEnrollmentReviewStatus.NotFound => Results.NotFound(),
            DeveloperEnrollmentReviewStatus.Conflict => CreateProblemResult(
                StatusCodes.Status409Conflict,
                "Developer enrollment review conflict.",
                "Only pending developer enrollment requests can be reviewed.",
                "developer_enrollment_review_conflict"),
            DeveloperEnrollmentReviewStatus.UpstreamFailure => CreateProblemResult(
                StatusCodes.Status502BadGateway,
                "Developer enrollment could not be completed.",
                result.ErrorDetail ?? "Keycloak role assignment failed for the authenticated user.",
                "keycloak_developer_enrollment_failed"),
            _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
        };

    private static DeveloperEnrollmentRequestDto MapRequest(DeveloperEnrollmentRequestSnapshot request) =>
        new(
            request.RequestId,
            request.ApplicantSubject,
            request.ApplicantDisplayName,
            request.ApplicantEmail,
            request.Status,
            request.DeveloperAccessEnabled,
            request.RequestedAtUtc,
            request.ReviewedAtUtc,
            request.ReviewerSubject);

    private static IResult CreateProblemResult(int statusCode, string title, string detail, string code) =>
        Results.Json(
            new ModerationProblemEnvelope(
                Type: $"https://boardtpl.dev/problems/{code.Replace('_', '-')}",
                Title: title,
                Status: statusCode,
                Detail: detail,
                Code: code),
            statusCode: statusCode);
}

/// <summary>
/// Moderator-visible developer enrollment request DTO.
/// </summary>
internal sealed record DeveloperEnrollmentRequestDto(
    Guid RequestId,
    string ApplicantSubject,
    string? ApplicantDisplayName,
    string? ApplicantEmail,
    string Status,
    bool DeveloperAccessEnabled,
    DateTime RequestedAt,
    DateTime? ReviewedAt,
    string? ReviewerSubject);

/// <summary>
/// Response wrapper for developer enrollment request lists.
/// </summary>
internal sealed record DeveloperEnrollmentRequestListResponse(IReadOnlyList<DeveloperEnrollmentRequestDto> Requests);

/// <summary>
/// Response wrapper for a reviewed developer enrollment request.
/// </summary>
internal sealed record DeveloperEnrollmentRequestResponse(DeveloperEnrollmentRequestDto DeveloperEnrollmentRequest);

/// <summary>
/// Problem-details envelope used by moderation endpoints.
/// </summary>
internal sealed record ModerationProblemEnvelope(string? Type, string Title, int Status, string? Detail, string? Code);
