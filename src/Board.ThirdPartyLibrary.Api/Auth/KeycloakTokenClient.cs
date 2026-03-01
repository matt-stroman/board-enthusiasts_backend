using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Board.ThirdPartyLibrary.Api.Auth;

/// <summary>
/// HTTP client that performs Keycloak authorization-code exchanges.
/// </summary>
internal sealed class KeycloakTokenClient : IKeycloakTokenClient
{
    private readonly HttpClient _httpClient;
    private readonly IKeycloakEndpointResolver _endpointResolver;
    private readonly KeycloakOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeycloakTokenClient"/> class.
    /// </summary>
    /// <param name="httpClient">Injected HTTP client.</param>
    /// <param name="endpointResolver">Resolver for realm endpoints.</param>
    /// <param name="options">Bound Keycloak configuration.</param>
    public KeycloakTokenClient(
        HttpClient httpClient,
        IKeycloakEndpointResolver endpointResolver,
        IOptions<KeycloakOptions> options)
    {
        _httpClient = httpClient;
        _endpointResolver = endpointResolver;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<KeycloakTokenExchangeResult> ExchangeAuthorizationCodeAsync(
        KeycloakTokenExchangeRequest request,
        CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, _endpointResolver.GetTokenEndpointUri())
        {
            Content = new FormUrlEncodedContent(
            [
                KeyValuePair.Create("grant_type", "authorization_code"),
                KeyValuePair.Create("code", request.Code),
                KeyValuePair.Create("redirect_uri", request.RedirectUri.AbsoluteUri),
                KeyValuePair.Create("client_id", _options.ClientId),
                KeyValuePair.Create("client_secret", _options.ClientSecret),
                KeyValuePair.Create("code_verifier", request.CodeVerifier)
            ])
        };

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        if (!response.IsSuccessStatusCode)
        {
            return KeycloakTokenExchangeResult.Failure(
                GetOptionalString(root, "error"),
                GetOptionalString(root, "error_description"));
        }

        return KeycloakTokenExchangeResult.Success(
            accessToken: root.GetProperty("access_token").GetString() ?? string.Empty,
            tokenType: root.GetProperty("token_type").GetString() ?? "Bearer",
            expiresInSeconds: root.GetProperty("expires_in").GetInt32(),
            scope: GetOptionalString(root, "scope"),
            refreshToken: GetOptionalString(root, "refresh_token"),
            idToken: GetOptionalString(root, "id_token"));
    }

    private static string? GetOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) ? value.GetString() : null;
}
