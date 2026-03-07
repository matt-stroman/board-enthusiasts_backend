using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Titles;
using Microsoft.AspNetCore.Authorization;

namespace Board.ThirdPartyLibrary.Api.Players;

/// <summary>
/// Maps authenticated player-library, wishlist, and title-report submission endpoints.
/// </summary>
internal static class PlayerEndpoints
{
    /// <summary>
    /// Maps player endpoints to the application.
    /// </summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapPlayerEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/player");

        group.MapGet("/library", [Authorize] async (
            ClaimsPrincipal user,
            IPlayerLibraryService playerLibraryService,
            CancellationToken cancellationToken) =>
        {
            var result = await playerLibraryService.GetLibraryAsync(user.Claims, cancellationToken);
            return Results.Ok(new PlayerTitleListResponse(result.Titles!.Select(MapCatalogTitleSummary).ToArray()));
        });

        group.MapPut("/library/titles/{titleId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            IPlayerLibraryService playerLibraryService,
            CancellationToken cancellationToken) =>
        {
            var result = await playerLibraryService.AddToLibraryAsync(user.Claims, titleId, cancellationToken);
            return result.Status switch
            {
                PlayerCollectionMutationStatus.Success => Results.Ok(
                    new PlayerCollectionMutationResponse(titleId, true, result.AlreadyInRequestedState)),
                PlayerCollectionMutationStatus.NotFound => Results.NotFound(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        group.MapDelete("/library/titles/{titleId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            IPlayerLibraryService playerLibraryService,
            CancellationToken cancellationToken) =>
        {
            var result = await playerLibraryService.RemoveFromLibraryAsync(user.Claims, titleId, cancellationToken);
            return result.Status switch
            {
                PlayerCollectionMutationStatus.Success => Results.Ok(
                    new PlayerCollectionMutationResponse(titleId, false, result.AlreadyInRequestedState)),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        group.MapGet("/wishlist", [Authorize] async (
            ClaimsPrincipal user,
            IPlayerLibraryService playerLibraryService,
            CancellationToken cancellationToken) =>
        {
            var result = await playerLibraryService.GetWishlistAsync(user.Claims, cancellationToken);
            return Results.Ok(new PlayerTitleListResponse(result.Titles!.Select(MapCatalogTitleSummary).ToArray()));
        });

        group.MapPut("/wishlist/titles/{titleId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            IPlayerLibraryService playerLibraryService,
            CancellationToken cancellationToken) =>
        {
            var result = await playerLibraryService.AddToWishlistAsync(user.Claims, titleId, cancellationToken);
            return result.Status switch
            {
                PlayerCollectionMutationStatus.Success => Results.Ok(
                    new PlayerCollectionMutationResponse(titleId, true, result.AlreadyInRequestedState)),
                PlayerCollectionMutationStatus.NotFound => Results.NotFound(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        group.MapDelete("/wishlist/titles/{titleId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid titleId,
            IPlayerLibraryService playerLibraryService,
            CancellationToken cancellationToken) =>
        {
            var result = await playerLibraryService.RemoveFromWishlistAsync(user.Claims, titleId, cancellationToken);
            return result.Status switch
            {
                PlayerCollectionMutationStatus.Success => Results.Ok(
                    new PlayerCollectionMutationResponse(titleId, false, result.AlreadyInRequestedState)),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        group.MapGet("/reports", [Authorize] async (
            ClaimsPrincipal user,
            IPlayerLibraryService playerLibraryService,
            CancellationToken cancellationToken) =>
        {
            var result = await playerLibraryService.GetReportsAsync(user.Claims, cancellationToken);
            return Results.Ok(new PlayerTitleReportListResponse(result.Reports!));
        });

        group.MapPost("/reports", [Authorize] async (
            ClaimsPrincipal user,
            CreatePlayerTitleReportRequest request,
            IPlayerLibraryService playerLibraryService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateCreateReportRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await playerLibraryService.CreateReportAsync(
                user.Claims,
                new CreatePlayerTitleReportCommand(request.TitleId, request.Reason.Trim()),
                cancellationToken);

            return result.Status switch
            {
                PlayerReportMutationStatus.Success => Results.Created(
                    $"/player/reports/{result.Report!.Id}",
                    new PlayerTitleReportResponse(result.Report)),
                PlayerReportMutationStatus.NotFound => Results.NotFound(),
                PlayerReportMutationStatus.Conflict => Results.Json(
                    new PlayerProblemEnvelope(
                        $"https://boardtpl.dev/problems/title-report-already-open",
                        "Report already open",
                        StatusCodes.Status409Conflict,
                        "You already have an open report for this title.",
                        "title_report_already_open"),
                    statusCode: StatusCodes.Status409Conflict),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        return app;
    }

    private static Dictionary<string, string[]> ValidateCreateReportRequest(CreatePlayerTitleReportRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (request.TitleId == Guid.Empty)
        {
            errors["titleId"] = ["Title identifier is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            errors["reason"] = ["Report reason is required."];
        }
        else if (request.Reason.Trim().Length > 2000)
        {
            errors["reason"] = ["Report reason cannot exceed 2000 characters."];
        }

        return errors;
    }

    private static PlayerCatalogTitleDto MapCatalogTitleSummary(TitleSnapshot title) =>
        new(
            title.Id,
            title.StudioId,
            title.StudioSlug,
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

    private static string BuildPlayerCountDisplay(int minPlayers, int maxPlayers) =>
        minPlayers == maxPlayers
            ? $"{minPlayers} {(minPlayers == 1 ? "player" : "players")}"
            : $"{minPlayers}-{maxPlayers} players";

    private static string BuildAgeDisplay(string ageRatingAuthority, string ageRatingValue) =>
        $"{ageRatingAuthority.Trim()} {ageRatingValue.Trim()}";
}

/// <summary>
/// Player title-list response wrapper.
/// </summary>
internal sealed record PlayerTitleListResponse(IReadOnlyList<PlayerCatalogTitleDto> Titles);

/// <summary>
/// Title summary returned by player library and wishlist endpoints.
/// </summary>
internal sealed record PlayerCatalogTitleDto(
    Guid Id,
    Guid StudioId,
    string StudioSlug,
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
/// Create-report request payload.
/// </summary>
internal sealed record CreatePlayerTitleReportRequest(Guid TitleId, string Reason);

/// <summary>
/// Player report list response wrapper.
/// </summary>
internal sealed record PlayerTitleReportListResponse(IReadOnlyList<PlayerTitleReportSummarySnapshot> Reports);

/// <summary>
/// Player report response wrapper.
/// </summary>
internal sealed record PlayerTitleReportResponse(PlayerTitleReportSummarySnapshot Report);

/// <summary>
/// Collection toggle mutation response.
/// </summary>
internal sealed record PlayerCollectionMutationResponse(Guid TitleId, bool Included, bool AlreadyInRequestedState);

/// <summary>
/// Problem-details envelope returned by player workflow endpoints.
/// </summary>
internal sealed record PlayerProblemEnvelope(string? Type, string Title, int Status, string? Detail, string? Code);
