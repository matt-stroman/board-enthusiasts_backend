using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Board.ThirdPartyLibrary.Api.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Board.ThirdPartyLibrary.Api.Identity;

/// <summary>
/// Maps identity-related endpoints.
/// </summary>
internal static class IdentityEndpoints
{
    private static readonly HashSet<string> SupportedAvatarContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    ];

    private const long MaxAvatarBytes = 2L * 1024L * 1024L;

    /// <summary>
    /// Maps identity-related endpoints to the application.
    /// </summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The route builder.</returns>
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/identity");

        group.MapGet("/roles", () => Results.Ok(new PlatformRoleListResponse(PlatformRoleCatalog.Roles)));

        group.MapGet("/auth/config", (
            IKeycloakEndpointResolver endpointResolver,
            IOptions<KeycloakOptions> options) =>
        {
            var keycloakOptions = options.Value;

            return Results.Ok(new AuthenticationConfigurationResponse(
                Issuer: endpointResolver.GetIssuerUri().AbsoluteUri,
                AuthorizationEndpoint: endpointResolver.GetAuthorizationEndpointUri().AbsoluteUri,
                TokenEndpoint: endpointResolver.GetTokenEndpointUri().AbsoluteUri,
                LogoutEndpoint: endpointResolver.GetLogoutEndpointUri().AbsoluteUri,
                JwksUri: endpointResolver.GetJwksUri().AbsoluteUri,
                AccountManagementUrl: endpointResolver.GetAccountManagementUri().AbsoluteUri,
                ClientId: keycloakOptions.ClientId,
                CallbackUrl: endpointResolver.GetCallbackUri().AbsoluteUri,
                Scopes: keycloakOptions.Scopes,
                RegistrationEnabled: true,
                ExternalIdentityProviders: keycloakOptions.ExternalIdentityProviders));
        });

        group.MapGet("/auth/login", (
            string? provider,
            IKeycloakAuthorizationStateStore authorizationStateStore,
            IKeycloakEndpointResolver endpointResolver,
            IOptions<KeycloakOptions> options) =>
        {
            var authorizationState = authorizationStateStore.Create();
            var keycloakOptions = options.Value;
            var parameters = new Dictionary<string, string?>
            {
                ["client_id"] = keycloakOptions.ClientId,
                ["redirect_uri"] = endpointResolver.GetCallbackUri().AbsoluteUri,
                ["response_type"] = "code",
                ["scope"] = string.Join(' ', keycloakOptions.Scopes),
                ["state"] = authorizationState.State,
                ["code_challenge"] = KeycloakPkce.CreateCodeChallenge(authorizationState.CodeVerifier),
                ["code_challenge_method"] = "S256"
            };

            if (!string.IsNullOrWhiteSpace(provider))
            {
                parameters["kc_idp_hint"] = provider;
            }

            var redirectUri = QueryHelpers.AddQueryString(
                endpointResolver.GetAuthorizationEndpointUri().AbsoluteUri,
                parameters!);

            return Results.Redirect(redirectUri);
        });

        group.MapGet("/auth/callback", async (
            string? code,
            string? state,
            string? error,
            string? error_description,
            IKeycloakAuthorizationStateStore authorizationStateStore,
            IKeycloakTokenClient tokenClient,
            IKeycloakEndpointResolver endpointResolver,
            IIdentityPersistenceService identityPersistenceService,
            CancellationToken cancellationToken) =>
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                return Results.Problem(
                    title: "Authentication failed.",
                    detail: error_description is null ? error : $"{error}: {error_description}",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
            {
                return Results.Problem(
                    title: "Authentication callback is invalid.",
                    detail: "Both the authorization code and state are required.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (!authorizationStateStore.TryTake(state, out var authorizationState) || authorizationState is null)
            {
                return Results.Problem(
                    title: "Authentication callback is invalid.",
                    detail: "The authorization state was missing, expired, or already used.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            var exchangeResult = await tokenClient.ExchangeAuthorizationCodeAsync(
                new KeycloakTokenExchangeRequest(code, authorizationState.CodeVerifier, endpointResolver.GetCallbackUri()),
                cancellationToken);

            if (!exchangeResult.Succeeded || string.IsNullOrWhiteSpace(exchangeResult.AccessToken))
            {
                return Results.Problem(
                    title: "Keycloak token exchange failed.",
                    detail: exchangeResult.ErrorDescription ?? exchangeResult.Error ?? "The token endpoint returned an unknown error.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            var accessTokenClaims = ReadClaims(exchangeResult.AccessToken).ToArray();
            await identityPersistenceService.EnsureCurrentUserProjectionAsync(accessTokenClaims, cancellationToken);

            return Results.Ok(new AuthenticationCallbackResponse(
                AccessToken: exchangeResult.AccessToken,
                RefreshToken: exchangeResult.RefreshToken,
                IdToken: exchangeResult.IdToken,
                TokenType: exchangeResult.TokenType ?? JwtBearerDefaults.AuthenticationScheme,
                ExpiresInSeconds: exchangeResult.ExpiresInSeconds,
                Scope: exchangeResult.Scope,
                User: BuildCurrentUserResponse(accessTokenClaims)));
        });

        group.MapGet("/me", [Authorize] async (
            ClaimsPrincipal user,
            IIdentityPersistenceService identityPersistenceService,
            CancellationToken cancellationToken) =>
        {
            await identityPersistenceService.EnsureCurrentUserProjectionAsync(user.Claims, cancellationToken);
            return Results.Ok(BuildCurrentUserResponse(user.Claims));
        });

        group.MapGet("/me/profile", [Authorize] async (
            ClaimsPrincipal user,
            IIdentityPersistenceService identityPersistenceService,
            CancellationToken cancellationToken) =>
        {
            var profile = await identityPersistenceService.GetCurrentUserProfileAsync(user.Claims, cancellationToken);
            return Results.Ok(new UserProfileResponse(MapUserProfile(profile)));
        });

        group.MapPut("/me/profile", [Authorize] async (
            ClaimsPrincipal user,
            UpdateUserProfileRequest request,
            IIdentityPersistenceService identityPersistenceService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateUserProfileRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var profile = await identityPersistenceService.UpdateCurrentUserProfileAsync(
                user.Claims,
                new UpdateUserProfileCommand(
                    NormalizeOptionalValue(request.DisplayName)),
                cancellationToken);

            return Results.Ok(new UserProfileResponse(MapUserProfile(profile)));
        });

        group.MapPut("/me/profile/avatar-url", [Authorize] async (
            ClaimsPrincipal user,
            SetAvatarUrlRequest request,
            IIdentityPersistenceService identityPersistenceService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.AvatarUrl))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["avatarUrl"] = ["Avatar URL is required."]
                    },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var avatarUrl = request.AvatarUrl.Trim();
            if (!Uri.TryCreate(avatarUrl, UriKind.Absolute, out _))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["avatarUrl"] = ["Avatar URL must be an absolute URI."]
                    },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var profile = await identityPersistenceService.SetCurrentUserAvatarUrlAsync(user.Claims, avatarUrl, cancellationToken);
            return Results.Ok(new UserProfileResponse(MapUserProfile(profile)));
        });

        group.MapPost("/me/profile/avatar-upload", [Authorize] async (
            ClaimsPrincipal user,
            [FromForm] UploadAvatarForm request,
            IIdentityPersistenceService identityPersistenceService,
            CancellationToken cancellationToken) =>
        {
            if (request.Avatar is null)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["avatar"] = ["Avatar image is required."]
                    },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            if (request.Avatar.Length <= 0)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["avatar"] = ["Avatar image cannot be empty."]
                    },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            if (request.Avatar.Length > MaxAvatarBytes)
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["avatar"] = [$"Avatar image size must be {MaxAvatarBytes / 1024 / 1024} MB or less."]
                    },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var contentType = request.Avatar.ContentType.Trim().ToLowerInvariant();
            if (!SupportedAvatarContentTypes.Contains(contentType))
            {
                return Results.ValidationProblem(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["avatar"] = ["Avatar image format must be JPEG, PNG, WEBP, or GIF."]
                    },
                    statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            await using var avatarStream = request.Avatar.OpenReadStream();
            using var memoryStream = new MemoryStream();
            await avatarStream.CopyToAsync(memoryStream, cancellationToken);

            var profile = await identityPersistenceService.UploadCurrentUserAvatarAsync(
                user.Claims,
                new UploadedAvatarCommand(contentType, memoryStream.ToArray()),
                cancellationToken);

            return Results.Ok(new UserProfileResponse(MapUserProfile(profile)));
        }).DisableAntiforgery();

        group.MapDelete("/me/profile/avatar", [Authorize] async (
            ClaimsPrincipal user,
            IIdentityPersistenceService identityPersistenceService,
            CancellationToken cancellationToken) =>
        {
            var profile = await identityPersistenceService.RemoveCurrentUserAvatarAsync(user.Claims, cancellationToken);
            return Results.Ok(new UserProfileResponse(MapUserProfile(profile)));
        });

        group.MapGet("/me/developer-enrollment", [Authorize] async (
            ClaimsPrincipal user,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var enrollment = await developerEnrollmentService.GetCurrentEnrollmentAsync(user.Claims, cancellationToken);
            return Results.Ok(new DeveloperEnrollmentResponse(MapDeveloperEnrollment(enrollment)));
        });

        group.MapPost("/me/developer-enrollment", [Authorize] async (
            ClaimsPrincipal user,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var enrollment = await developerEnrollmentService.SubmitEnrollmentAsync(user.Claims, cancellationToken);
            return Results.Ok(new DeveloperEnrollmentResponse(MapDeveloperEnrollment(enrollment)));
        });

        group.MapPost("/me/developer-enrollment/{requestId:guid}/cancel", [Authorize] async (
            ClaimsPrincipal user,
            Guid requestId,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await developerEnrollmentService.CancelEnrollmentAsync(user.Claims, requestId, cancellationToken);
            return result.Status switch
            {
                DeveloperEnrollmentMutationStatus.Success => Results.Ok(new DeveloperEnrollmentResponse(MapDeveloperEnrollment(result.Enrollment!))),
                DeveloperEnrollmentMutationStatus.NotFound => Results.NotFound(),
                DeveloperEnrollmentMutationStatus.Conflict => CreateProblemResult(
                    StatusCodes.Status409Conflict,
                    "Developer enrollment cancellation conflict.",
                    "Only open developer enrollment requests can be cancelled.",
                    "developer_enrollment_cancellation_conflict"),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        group.MapGet("/me/developer-enrollment/{requestId:guid}/conversation", [Authorize] async (
            ClaimsPrincipal user,
            Guid requestId,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await developerEnrollmentService.GetCurrentEnrollmentConversationAsync(user.Claims, requestId, cancellationToken);
            return result.Status switch
            {
                DeveloperEnrollmentConversationStatus.Success => Results.Ok(new DeveloperEnrollmentConversationResponse(MapConversation(result.Conversation!))),
                DeveloperEnrollmentConversationStatus.NotFound => Results.NotFound(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        group.MapPost("/me/developer-enrollment/{requestId:guid}/messages", [Authorize] async (
            ClaimsPrincipal user,
            Guid requestId,
            [FromForm] ConversationMessageForm request,
            HttpRequest httpRequest,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var form = await httpRequest.ReadFormAsync(cancellationToken);
            var (attachments, attachmentErrors) = await DeveloperEnrollmentAttachmentReader.ReadAsync(form.Files, cancellationToken);
            if (attachmentErrors.Count > 0)
            {
                return Results.ValidationProblem(attachmentErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var result = await developerEnrollmentService.ReplyToEnrollmentAsync(
                user.Claims,
                requestId,
                request.Message,
                attachments,
                cancellationToken);

            return result.Status switch
            {
                DeveloperEnrollmentMutationStatus.Success => Results.Ok(new DeveloperEnrollmentResponse(MapDeveloperEnrollment(result.Enrollment!))),
                DeveloperEnrollmentMutationStatus.NotFound => Results.NotFound(),
                DeveloperEnrollmentMutationStatus.Conflict => CreateProblemResult(
                    StatusCodes.Status409Conflict,
                    "Developer enrollment reply conflict.",
                    "The current request is not waiting for an applicant reply.",
                    "developer_enrollment_reply_conflict"),
                DeveloperEnrollmentMutationStatus.Validation => Results.ValidationProblem(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["message"] = ["A message body or at least one attachment is required."]
                    },
                    statusCode: StatusCodes.Status422UnprocessableEntity),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        }).DisableAntiforgery();

        group.MapGet("/me/developer-enrollment/{requestId:guid}/attachments/{attachmentId:guid}", [Authorize] async (
            ClaimsPrincipal user,
            Guid requestId,
            Guid attachmentId,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await developerEnrollmentService.GetCurrentEnrollmentAttachmentAsync(user.Claims, requestId, attachmentId, cancellationToken);
            return result.Status switch
            {
                DeveloperEnrollmentAttachmentStatus.Success => Results.File(
                    result.Attachment!.Content,
                    result.Attachment.ContentType,
                    result.Attachment.FileName),
                DeveloperEnrollmentAttachmentStatus.NotFound => Results.NotFound(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        group.MapGet("/me/notifications", [Authorize] async (
            ClaimsPrincipal user,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var notifications = await developerEnrollmentService.ListNotificationsAsync(user.Claims, cancellationToken);
            return Results.Ok(new NotificationListResponse(notifications.Notifications.Select(MapNotification).ToArray()));
        });

        group.MapPost("/me/notifications/{notificationId:guid}/read", [Authorize] async (
            ClaimsPrincipal user,
            Guid notificationId,
            IDeveloperEnrollmentService developerEnrollmentService,
            CancellationToken cancellationToken) =>
        {
            var result = await developerEnrollmentService.MarkNotificationReadAsync(user.Claims, notificationId, cancellationToken);
            return result.Status switch
            {
                NotificationMutationStatus.Success => Results.Ok(new NotificationResponse(MapNotification(result.Notification!))),
                NotificationMutationStatus.NotFound => Results.NotFound(),
                _ => Results.StatusCode(StatusCodes.Status500InternalServerError)
            };
        });

        group.MapGet("/me/board-profile", [Authorize] async (
            ClaimsPrincipal user,
            IIdentityPersistenceService identityPersistenceService,
            CancellationToken cancellationToken) =>
        {
            var boardProfile = await identityPersistenceService.GetBoardProfileAsync(user.Claims, cancellationToken);
            return boardProfile is null
                ? CreateProblemResult(
                    StatusCodes.Status404NotFound,
                    "Board profile not linked",
                    "No linked Board profile exists for the current user.",
                    "board_profile_not_linked")
                : Results.Ok(new BoardProfileResponse(new BoardProfile(
                    boardProfile.BoardUserId,
                    boardProfile.DisplayName,
                    boardProfile.AvatarUrl,
                    boardProfile.LinkedAtUtc,
                    boardProfile.LastSyncedAtUtc)));
        });

        group.MapPut("/me/board-profile", [Authorize] async (
            ClaimsPrincipal user,
            UpsertBoardProfileRequest request,
            IIdentityPersistenceService identityPersistenceService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = ValidateBoardProfileRequest(request);
            if (validationErrors.Count > 0)
            {
                return Results.ValidationProblem(validationErrors, statusCode: StatusCodes.Status422UnprocessableEntity);
            }

            var boardProfile = await identityPersistenceService.UpsertBoardProfileAsync(
                user.Claims,
                new UpsertBoardProfileCommand(
                    request.BoardUserId.Trim(),
                    request.DisplayName.Trim(),
                    request.AvatarUrl?.Trim()),
                cancellationToken);

            return Results.Ok(new BoardProfileResponse(new BoardProfile(
                boardProfile.BoardUserId,
                boardProfile.DisplayName,
                boardProfile.AvatarUrl,
                boardProfile.LinkedAtUtc,
                boardProfile.LastSyncedAtUtc)));
        });

        group.MapDelete("/me/board-profile", [Authorize] async (
            ClaimsPrincipal user,
            IIdentityPersistenceService identityPersistenceService,
            CancellationToken cancellationToken) =>
        {
            var deleted = await identityPersistenceService.DeleteBoardProfileAsync(user.Claims, cancellationToken);
            return deleted
                ? Results.NoContent()
                : CreateProblemResult(
                    StatusCodes.Status404NotFound,
                    "Board profile not linked",
                    "No linked Board profile exists for the current user.",
                    "board_profile_not_linked");
        });

        return app;
    }

    private static Dictionary<string, string[]> ValidateBoardProfileRequest(UpsertBoardProfileRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(request.BoardUserId))
        {
            errors["boardUserId"] = ["Board user ID is required."];
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors["displayName"] = ["Display name is required."];
        }

        if (!string.IsNullOrWhiteSpace(request.AvatarUrl) &&
            !Uri.TryCreate(request.AvatarUrl, UriKind.Absolute, out _))
        {
            errors["avatarUrl"] = ["Avatar URL must be an absolute URI."];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateUserProfileRequest(UpdateUserProfileRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(request.DisplayName) && request.DisplayName.Trim().Length > 200)
        {
            errors["displayName"] = ["Display name cannot exceed 200 characters."];
        }

        return errors;
    }

    private static string? NormalizeOptionalValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CurrentUserResponse BuildCurrentUserResponse(IEnumerable<Claim> claims)
    {
        var claimList = claims.ToList();

        return new CurrentUserResponse(
            Subject: ClaimValueResolver.GetSubject(claimList) ?? string.Empty,
            DisplayName: ClaimValueResolver.GetClaimValue(claimList, "name") ?? ClaimValueResolver.GetClaimValue(claimList, "preferred_username") ?? string.Empty,
            Email: ClaimValueResolver.GetClaimValue(claimList, "email"),
            EmailVerified: bool.TryParse(ClaimValueResolver.GetClaimValue(claimList, "email_verified"), out var emailVerified) && emailVerified,
            IdentityProvider: ClaimValueResolver.GetClaimValue(claimList, "identity_provider") ?? ClaimValueResolver.GetClaimValue(claimList, "idp"),
            Roles: claimList
                .Where(claim => claim.Type == ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    private static IEnumerable<Claim> ReadClaims(string accessToken) =>
        new JwtSecurityTokenHandler().ReadJwtToken(accessToken).Claims;

    private static DeveloperEnrollment MapDeveloperEnrollment(DeveloperEnrollmentStateSnapshot snapshot) =>
        new(
            snapshot.RequestId,
            snapshot.Status,
            snapshot.ActionRequiredBy,
            snapshot.DeveloperAccessEnabled,
            snapshot.CanSubmitRequest,
            snapshot.CanCancelRequest,
            snapshot.CanReply,
            snapshot.RequestedAtUtc,
            snapshot.UpdatedAtUtc,
            snapshot.ReviewedAtUtc,
            snapshot.ReapplyAvailableAtUtc,
            snapshot.ReviewerSubject);

    private static DeveloperEnrollmentConversation MapConversation(DeveloperEnrollmentConversationSnapshot snapshot) =>
        new(
            snapshot.RequestId,
            snapshot.Status,
            snapshot.ActionRequiredBy,
            snapshot.ReviewedAtUtc,
            snapshot.ReviewerSubject,
            snapshot.Messages.Select(message => new DeveloperEnrollmentConversationMessage(
                message.MessageId,
                message.AuthorRole,
                message.AuthorSubject,
                message.AuthorDisplayName,
                message.MessageKind,
                message.Body,
                message.CreatedAtUtc,
                message.Attachments.Select(attachment => new DeveloperEnrollmentConversationAttachment(
                    attachment.AttachmentId,
                    attachment.FileName,
                    attachment.ContentType,
                    attachment.SizeBytes)).ToArray())).ToArray());

    private static UserNotificationDto MapNotification(NotificationSnapshot snapshot) =>
        new(
            snapshot.NotificationId,
            snapshot.Category,
            snapshot.Title,
            snapshot.Body,
            snapshot.ActionUrl,
            snapshot.IsRead,
            snapshot.CreatedAtUtc,
            snapshot.ReadAtUtc);

    private static UserProfileDto MapUserProfile(UserProfileSnapshot snapshot) =>
        new(
            snapshot.Subject,
            snapshot.DisplayName,
            snapshot.UserName,
            snapshot.FirstName,
            snapshot.LastName,
            snapshot.Email,
            snapshot.EmailVerified,
            snapshot.AvatarUrl,
            snapshot.UploadedAvatar is null
                ? null
                : $"data:{snapshot.UploadedAvatar.ContentType};base64,{Convert.ToBase64String(snapshot.UploadedAvatar.Content)}",
            snapshot.Initials,
            snapshot.UpdatedAtUtc);

    private static IResult CreateProblemResult(int statusCode, string title, string detail, string code) =>
        Results.Json(
            new ProblemEnvelope(
                Type: $"https://boardtpl.dev/problems/{code.Replace('_', '-')}",
                Title: title,
                Status: statusCode,
                Detail: detail,
                Code: code),
            statusCode: statusCode);
}

/// <summary>
/// Response for the platform roles endpoint.
/// </summary>
/// <param name="Roles">Supported platform roles.</param>
internal sealed record PlatformRoleListResponse(IReadOnlyList<PlatformRoleDefinition> Roles);

/// <summary>
/// Response describing Keycloak-backed authentication settings for clients.
/// </summary>
/// <param name="Issuer">OIDC issuer URI.</param>
/// <param name="AuthorizationEndpoint">OIDC authorization endpoint URI.</param>
/// <param name="TokenEndpoint">OIDC token endpoint URI.</param>
/// <param name="LogoutEndpoint">OIDC logout endpoint URI.</param>
/// <param name="JwksUri">OIDC JWK set URI.</param>
/// <param name="AccountManagementUrl">Keycloak account console URL.</param>
/// <param name="ClientId">Configured client identifier.</param>
/// <param name="CallbackUrl">Backend callback URI.</param>
/// <param name="Scopes">Scopes requested by the backend login flow.</param>
/// <param name="RegistrationEnabled">Whether self-registration is enabled in the realm.</param>
/// <param name="ExternalIdentityProviders">Configured external identity provider aliases.</param>
internal sealed record AuthenticationConfigurationResponse(
    string Issuer,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string LogoutEndpoint,
    string JwksUri,
    string AccountManagementUrl,
    string ClientId,
    string CallbackUrl,
    IReadOnlyList<string> Scopes,
    bool RegistrationEnabled,
    IReadOnlyList<string> ExternalIdentityProviders);

/// <summary>
/// Response returned after a successful Keycloak authorization-code callback.
/// </summary>
/// <param name="AccessToken">OIDC access token.</param>
/// <param name="RefreshToken">Optional refresh token.</param>
/// <param name="IdToken">Optional ID token.</param>
/// <param name="TokenType">OAuth token type.</param>
/// <param name="ExpiresInSeconds">Access token lifetime in seconds.</param>
/// <param name="Scope">Granted scope string.</param>
/// <param name="User">Resolved user profile derived from the access token.</param>
internal sealed record AuthenticationCallbackResponse(
    string AccessToken,
    string? RefreshToken,
    string? IdToken,
    string TokenType,
    int ExpiresInSeconds,
    string? Scope,
    CurrentUserResponse User);

/// <summary>
/// Current authenticated user summary.
/// </summary>
/// <param name="Subject">Stable identity subject identifier.</param>
/// <param name="DisplayName">Display name or preferred username.</param>
/// <param name="Email">Primary email address.</param>
/// <param name="EmailVerified">Whether the email address is verified.</param>
/// <param name="IdentityProvider">External identity provider alias when applicable.</param>
/// <param name="Roles">Assigned platform roles.</param>
internal sealed record CurrentUserResponse(
    string Subject,
    string DisplayName,
    string? Email,
    bool EmailVerified,
    string? IdentityProvider,
    IReadOnlyList<string> Roles);

/// <summary>
/// Application-managed profile details for the current user.
/// </summary>
/// <param name="Subject">Stable user subject identifier.</param>
/// <param name="DisplayName">Application-managed display name.</param>
/// <param name="UserName">Username sourced from Keycloak claims.</param>
/// <param name="FirstName">First name sourced from Keycloak claims.</param>
/// <param name="LastName">Last name sourced from Keycloak claims.</param>
/// <param name="Email">Cached identity email address.</param>
/// <param name="EmailVerified">Cached identity email verification flag.</param>
/// <param name="AvatarUrl">Hosted avatar URL when configured.</param>
/// <param name="AvatarDataUrl">Inline data URL when the avatar is uploaded directly.</param>
/// <param name="Initials">Initials used for avatar fallback rendering.</param>
/// <param name="UpdatedAt">UTC profile update timestamp.</param>
internal sealed record UserProfileDto(
    string Subject,
    string? DisplayName,
    string? UserName,
    string? FirstName,
    string? LastName,
    string? Email,
    bool EmailVerified,
    string? AvatarUrl,
    string? AvatarDataUrl,
    string Initials,
    DateTime UpdatedAt);

/// <summary>
/// Response wrapper for the current user profile endpoint.
/// </summary>
/// <param name="Profile">Current user profile payload.</param>
internal sealed record UserProfileResponse(UserProfileDto Profile);

/// <summary>
/// Request payload for updating current user profile fields.
/// </summary>
/// <param name="DisplayName">Application-managed display name.</param>
internal sealed record UpdateUserProfileRequest(string? DisplayName);

/// <summary>
/// Request payload for setting avatar URL.
/// </summary>
/// <param name="AvatarUrl">Absolute avatar URL.</param>
internal sealed record SetAvatarUrlRequest(string AvatarUrl);

internal sealed class UploadAvatarForm
{
    public IFormFile? Avatar { get; set; }
}

/// <summary>
/// Developer-enrollment state for the current user.
/// </summary>
/// <param name="RequestId">Application-owned request identifier when one exists.</param>
/// <param name="Status">Player-facing enrollment status.</param>
/// <param name="ActionRequiredBy">Which workflow actor currently needs to act.</param>
/// <param name="DeveloperAccessEnabled">Whether developer access is enabled in Keycloak.</param>
/// <param name="CanSubmitRequest">Whether the caller can still submit a request.</param>
/// <param name="CanCancelRequest">Whether the caller can cancel the active request.</param>
/// <param name="CanReply">Whether the caller can reply with more information.</param>
/// <param name="RequestedAt">UTC timestamp when the request was submitted.</param>
/// <param name="UpdatedAt">UTC timestamp of the latest workflow activity.</param>
/// <param name="ReviewedAt">UTC timestamp when the request was reviewed.</param>
/// <param name="ReapplyAvailableAt">UTC timestamp when the caller may submit a new request again.</param>
/// <param name="ReviewerSubject">Reviewer Keycloak subject when the request was reviewed.</param>
internal sealed record DeveloperEnrollment(
    Guid? RequestId,
    string Status,
    string ActionRequiredBy,
    bool DeveloperAccessEnabled,
    bool CanSubmitRequest,
    bool CanCancelRequest,
    bool CanReply,
    DateTime? RequestedAt,
    DateTime? UpdatedAt,
    DateTime? ReviewedAt,
    DateTime? ReapplyAvailableAt,
    string? ReviewerSubject);

/// <summary>
/// Response wrapper for developer enrollment.
/// </summary>
/// <param name="DeveloperEnrollment">Developer enrollment result.</param>
internal sealed record DeveloperEnrollmentResponse(DeveloperEnrollment DeveloperEnrollment);

internal sealed record DeveloperEnrollmentConversationAttachment(
    Guid AttachmentId,
    string FileName,
    string ContentType,
    long SizeBytes);

internal sealed record DeveloperEnrollmentConversationMessage(
    Guid MessageId,
    string AuthorRole,
    string AuthorSubject,
    string? AuthorDisplayName,
    string MessageKind,
    string? Body,
    DateTime CreatedAt,
    IReadOnlyList<DeveloperEnrollmentConversationAttachment> Attachments);

internal sealed record DeveloperEnrollmentConversation(
    Guid RequestId,
    string Status,
    string ActionRequiredBy,
    DateTime? ReviewedAt,
    string? ReviewerSubject,
    IReadOnlyList<DeveloperEnrollmentConversationMessage> Messages);

internal sealed record DeveloperEnrollmentConversationResponse(DeveloperEnrollmentConversation Conversation);

internal sealed class ConversationMessageForm
{
    public string? Message { get; set; }

    public List<IFormFile> Attachments { get; set; } = [];
}

internal sealed record UserNotificationDto(
    Guid NotificationId,
    string Category,
    string Title,
    string Body,
    string? ActionUrl,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt);

internal sealed record NotificationListResponse(IReadOnlyList<UserNotificationDto> Notifications);

internal sealed record NotificationResponse(UserNotificationDto Notification);

/// <summary>
/// Linked Board profile summary for the current user.
/// </summary>
/// <param name="BoardUserId">Board-owned user identifier.</param>
/// <param name="DisplayName">Board display name.</param>
/// <param name="AvatarUrl">Board avatar image URL.</param>
/// <param name="LinkedAt">UTC timestamp when the profile was first linked.</param>
/// <param name="LastSyncedAt">UTC timestamp when the cached profile was last updated.</param>
internal sealed record BoardProfile(
    string BoardUserId,
    string DisplayName,
    string? AvatarUrl,
    DateTime LinkedAt,
    DateTime LastSyncedAt);

/// <summary>
/// Response wrapper for the linked Board profile endpoint.
/// </summary>
/// <param name="BoardProfile">Current user's linked Board profile.</param>
internal sealed record BoardProfileResponse(BoardProfile BoardProfile);

/// <summary>
/// Request payload for linking or updating a Board profile.
/// </summary>
/// <param name="BoardUserId">Board-owned user identifier.</param>
/// <param name="DisplayName">Board display name.</param>
/// <param name="AvatarUrl">Optional avatar image URL.</param>
internal sealed record UpsertBoardProfileRequest(string BoardUserId, string DisplayName, string? AvatarUrl);

internal sealed record ProblemEnvelope(string? Type, string Title, int Status, string? Detail, string? Code);
