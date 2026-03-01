using Microsoft.Extensions.Options;

namespace Board.ThirdPartyLibrary.Api.Auth;

/// <summary>
/// Default resolver for Keycloak realm endpoint URIs.
/// </summary>
internal sealed class KeycloakEndpointResolver : IKeycloakEndpointResolver
{
    private readonly KeycloakOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="KeycloakEndpointResolver"/> class.
    /// </summary>
    /// <param name="options">Bound Keycloak configuration.</param>
    public KeycloakEndpointResolver(IOptions<KeycloakOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public Uri GetIssuerUri() => new($"{GetRealmBaseUrl()}/");

    /// <inheritdoc />
    public Uri GetWellKnownConfigurationUri() => new($"{GetRealmBaseUrl()}/.well-known/openid-configuration");

    /// <inheritdoc />
    public Uri GetAuthorizationEndpointUri() => new($"{GetRealmBaseUrl()}/protocol/openid-connect/auth");

    /// <inheritdoc />
    public Uri GetTokenEndpointUri() => new($"{GetRealmBaseUrl()}/protocol/openid-connect/token");

    /// <inheritdoc />
    public Uri GetLogoutEndpointUri() => new($"{GetRealmBaseUrl()}/protocol/openid-connect/logout");

    /// <inheritdoc />
    public Uri GetJwksUri() => new($"{GetRealmBaseUrl()}/protocol/openid-connect/certs");

    /// <inheritdoc />
    public Uri GetAccountManagementUri() => new($"{GetRealmBaseUrl()}/account");

    /// <inheritdoc />
    public Uri GetCallbackUri() => new($"{_options.PublicBackendBaseUrl.TrimEnd('/')}/identity/auth/callback");

    private string GetRealmBaseUrl() =>
        $"{_options.BaseUrl.TrimEnd('/')}/realms/{Uri.EscapeDataString(_options.Realm)}";
}
