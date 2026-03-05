using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Board.ThirdPartyLibrary.Api.Auth;

/// <summary>
/// Keycloak admin client that ensures realm roles are assigned to users.
/// </summary>
internal sealed class KeycloakUserRoleClient : IKeycloakUserRoleClient
{
    private readonly HttpClient _httpClient;
    private readonly IKeycloakEndpointResolver _endpointResolver;
    private readonly KeycloakOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeycloakUserRoleClient"/> class.
    /// </summary>
    /// <param name="httpClient">Injected HTTP client.</param>
    /// <param name="endpointResolver">Resolver for Keycloak endpoint URIs.</param>
    /// <param name="options">Bound Keycloak configuration.</param>
    public KeycloakUserRoleClient(
        HttpClient httpClient,
        IKeycloakEndpointResolver endpointResolver,
        IOptions<KeycloakOptions> options)
    {
        _httpClient = httpClient;
        _endpointResolver = endpointResolver;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<KeycloakUserRoleMutationResult> EnsureRealmRoleAssignedAsync(
        string userSubject,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        var adminTokenResult = await GetAdminAccessTokenAsync(cancellationToken);
        if (!adminTokenResult.Succeeded || string.IsNullOrWhiteSpace(adminTokenResult.AccessToken))
        {
            return KeycloakUserRoleMutationResult.Failure(adminTokenResult.ErrorDetail ?? "Keycloak admin token could not be acquired.");
        }
        var adminToken = adminTokenResult.AccessToken;

        var userRoleRead = await GetUserRoleMappingsAsync(userSubject, adminToken, cancellationToken);
        if (!userRoleRead.Succeeded)
        {
            return userRoleRead.UserNotFound
                ? KeycloakUserRoleMutationResult.NotFound()
                : KeycloakUserRoleMutationResult.Failure(userRoleRead.ErrorDetail ?? "Keycloak user role mappings could not be read.");
        }

        if (ContainsRole(userRoleRead.Roles, roleName))
        {
            return KeycloakUserRoleMutationResult.Success(alreadyInRequestedState: true);
        }

        var roleResolution = await GetRealmRoleRepresentationAsync(roleName, adminToken, cancellationToken);
        if (!roleResolution.Succeeded)
        {
            return KeycloakUserRoleMutationResult.Failure(roleResolution.ErrorDetail ?? $"Keycloak realm role '{roleName}' could not be resolved.");
        }

        using var assignRequest = CreateAdminRequest(
            HttpMethod.Post,
            _endpointResolver.GetAdminUserRealmRoleMappingsUri(userSubject),
            adminToken);

        assignRequest.Content = JsonContent.Create(
            new[]
            {
                roleResolution.Role!
            },
            options: SerializerOptions);

        using var assignResponse = await _httpClient.SendAsync(assignRequest, cancellationToken);
        var assignPayload = await assignResponse.Content.ReadAsStringAsync(cancellationToken);

        if (assignResponse.IsSuccessStatusCode)
        {
            return KeycloakUserRoleMutationResult.Success(alreadyInRequestedState: false);
        }

        return assignResponse.StatusCode == System.Net.HttpStatusCode.NotFound
            ? KeycloakUserRoleMutationResult.NotFound()
            : KeycloakUserRoleMutationResult.Failure(BuildFailureDetail(
                assignResponse.StatusCode,
                "Keycloak role assignment failed.",
                assignPayload));
    }

    /// <inheritdoc />
    public async Task<KeycloakUserRoleMutationResult> EnsureRealmRoleRemovedAsync(
        string userSubject,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        var adminTokenResult = await GetAdminAccessTokenAsync(cancellationToken);
        if (!adminTokenResult.Succeeded || string.IsNullOrWhiteSpace(adminTokenResult.AccessToken))
        {
            return KeycloakUserRoleMutationResult.Failure(adminTokenResult.ErrorDetail ?? "Keycloak admin token could not be acquired.");
        }
        var adminToken = adminTokenResult.AccessToken;

        var userRoleRead = await GetUserRoleMappingsAsync(userSubject, adminToken, cancellationToken);
        if (!userRoleRead.Succeeded)
        {
            return userRoleRead.UserNotFound
                ? KeycloakUserRoleMutationResult.NotFound()
                : KeycloakUserRoleMutationResult.Failure(userRoleRead.ErrorDetail ?? "Keycloak user role mappings could not be read.");
        }

        if (!ContainsRole(userRoleRead.Roles, roleName))
        {
            return KeycloakUserRoleMutationResult.Success(alreadyInRequestedState: true);
        }

        var roleResolution = await GetRealmRoleRepresentationAsync(roleName, adminToken, cancellationToken);
        if (!roleResolution.Succeeded)
        {
            return KeycloakUserRoleMutationResult.Failure(roleResolution.ErrorDetail ?? $"Keycloak realm role '{roleName}' could not be resolved.");
        }

        using var removeRequest = CreateAdminRequest(
            HttpMethod.Delete,
            _endpointResolver.GetAdminUserRealmRoleMappingsUri(userSubject),
            adminToken);

        removeRequest.Content = JsonContent.Create(
            new[]
            {
                roleResolution.Role!
            },
            options: SerializerOptions);

        using var removeResponse = await _httpClient.SendAsync(removeRequest, cancellationToken);
        var removePayload = await removeResponse.Content.ReadAsStringAsync(cancellationToken);

        if (removeResponse.IsSuccessStatusCode)
        {
            return KeycloakUserRoleMutationResult.Success(alreadyInRequestedState: false);
        }

        return removeResponse.StatusCode == System.Net.HttpStatusCode.NotFound
            ? KeycloakUserRoleMutationResult.NotFound()
            : KeycloakUserRoleMutationResult.Failure(BuildFailureDetail(
                removeResponse.StatusCode,
                "Keycloak role removal failed.",
                removePayload));
    }

    /// <inheritdoc />
    public async Task<KeycloakUserRoleCheckResult> IsRealmRoleAssignedAsync(
        string userSubject,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        var adminTokenResult = await GetAdminAccessTokenAsync(cancellationToken);
        if (!adminTokenResult.Succeeded || string.IsNullOrWhiteSpace(adminTokenResult.AccessToken))
        {
            return KeycloakUserRoleCheckResult.Failure(adminTokenResult.ErrorDetail ?? "Keycloak admin token could not be acquired.");
        }
        var adminToken = adminTokenResult.AccessToken;

        var userRoleRead = await GetUserRoleMappingsAsync(userSubject, adminToken, cancellationToken);
        if (!userRoleRead.Succeeded)
        {
            return userRoleRead.UserNotFound
                ? KeycloakUserRoleCheckResult.NotFound()
                : KeycloakUserRoleCheckResult.Failure(userRoleRead.ErrorDetail ?? "Keycloak user role mappings could not be read.");
        }

        return KeycloakUserRoleCheckResult.Success(ContainsRole(userRoleRead.Roles, roleName));
    }

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private async Task<UserRoleReadResult> GetUserRoleMappingsAsync(string userSubject, string adminToken, CancellationToken cancellationToken)
    {
        using var request = CreateAdminRequest(
            HttpMethod.Get,
            _endpointResolver.GetAdminUserRealmRoleMappingsUri(userSubject),
            adminToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return response.StatusCode == System.Net.HttpStatusCode.NotFound
                ? UserRoleReadResult.NotFound()
                : UserRoleReadResult.Failure(BuildFailureDetail(
                    response.StatusCode,
                    "Keycloak user role mappings could not be read.",
                    payload));
        }

        try
        {
            var roles = JsonSerializer.Deserialize<List<KeycloakRealmRoleRepresentation>>(payload, SerializerOptions) ?? [];
            return UserRoleReadResult.Success(roles);
        }
        catch (JsonException)
        {
            return UserRoleReadResult.Failure("Keycloak user role mappings payload was invalid.");
        }
    }

    private async Task<RoleResolutionResult> GetRealmRoleRepresentationAsync(string roleName, string adminToken, CancellationToken cancellationToken)
    {
        using var request = CreateAdminRequest(
            HttpMethod.Get,
            _endpointResolver.GetAdminRealmRoleUri(roleName),
            adminToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return RoleResolutionResult.Failure(BuildFailureDetail(
                response.StatusCode,
                $"Keycloak realm role '{roleName}' could not be resolved.",
                payload));
        }

        var role = JsonSerializer.Deserialize<KeycloakRealmRoleRepresentation>(payload, SerializerOptions);
        if (role is null ||
            string.IsNullOrWhiteSpace(role.Id) ||
            string.IsNullOrWhiteSpace(role.Name))
        {
            return RoleResolutionResult.Failure("Keycloak returned an invalid realm role representation.");
        }

        return RoleResolutionResult.Success(role);
    }

    private async Task<AdminAccessTokenResult> GetAdminAccessTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _endpointResolver.GetTokenEndpointUri())
        {
            Content = new FormUrlEncodedContent(
            [
                KeyValuePair.Create("grant_type", "client_credentials"),
                KeyValuePair.Create("client_id", _options.ClientId),
                KeyValuePair.Create("client_secret", _options.ClientSecret)
            ])
        };

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return AdminAccessTokenResult.Failure(
                    BuildFailureDetail(
                        response.StatusCode,
                        "Keycloak admin token request failed.",
                        payload));
            }

            using var document = JsonDocument.Parse(payload);
            if (!document.RootElement.TryGetProperty("access_token", out var accessTokenElement) ||
                string.IsNullOrWhiteSpace(accessTokenElement.GetString()))
            {
                return AdminAccessTokenResult.Failure("Keycloak admin token response did not include an access token.");
            }

            return AdminAccessTokenResult.Success(accessTokenElement.GetString()!);
        }
        catch (HttpRequestException exception)
        {
            return AdminAccessTokenResult.Failure($"Keycloak admin token request could not reach the token endpoint. {exception.Message}");
        }
        catch (TaskCanceledException exception)
        {
            return AdminAccessTokenResult.Failure($"Keycloak admin token request timed out or was canceled. {exception.Message}");
        }
        catch (JsonException)
        {
            return AdminAccessTokenResult.Failure("Keycloak admin token response payload was invalid.");
        }
    }

    private static HttpRequestMessage CreateAdminRequest(HttpMethod method, Uri uri, string accessToken)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private static string BuildFailureDetail(System.Net.HttpStatusCode statusCode, string prefix, string payload)
    {
        var suffix = string.IsNullOrWhiteSpace(payload)
            ? string.Empty
            : $" Response: {payload}";

        return $"{prefix} Status: {(int)statusCode}.{suffix}";
    }

    private static bool ContainsRole(IEnumerable<KeycloakRealmRoleRepresentation> roles, string roleName) =>
        roles.Any(candidate => string.Equals(candidate.Name, roleName, StringComparison.OrdinalIgnoreCase));

    private sealed record UserRoleReadResult(bool Succeeded, IReadOnlyList<KeycloakRealmRoleRepresentation> Roles, bool UserNotFound, string? ErrorDetail)
    {
        public static UserRoleReadResult Success(IReadOnlyList<KeycloakRealmRoleRepresentation> roles) =>
            new(true, roles, false, null);

        public static UserRoleReadResult NotFound() =>
            new(false, [], true, null);

        public static UserRoleReadResult Failure(string errorDetail) =>
            new(false, [], false, errorDetail);
    }

    private sealed record RoleResolutionResult(bool Succeeded, KeycloakRealmRoleRepresentation? Role, string? ErrorDetail)
    {
        public static RoleResolutionResult Success(KeycloakRealmRoleRepresentation role) =>
            new(true, role, null);

        public static RoleResolutionResult Failure(string errorDetail) =>
            new(false, null, errorDetail);
    }

    private sealed record AdminAccessTokenResult(bool Succeeded, string? AccessToken, string? ErrorDetail)
    {
        public static AdminAccessTokenResult Success(string accessToken) =>
            new(true, accessToken, null);

        public static AdminAccessTokenResult Failure(string errorDetail) =>
            new(false, null, errorDetail);
    }
}

/// <summary>
/// Minimal Keycloak realm-role representation used for user-role mapping calls.
/// </summary>
/// <param name="Id">Role identifier.</param>
/// <param name="Name">Role name.</param>
internal sealed record KeycloakRealmRoleRepresentation(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("name")] string? Name);
