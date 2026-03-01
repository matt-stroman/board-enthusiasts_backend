namespace Board.ThirdPartyLibrary.Api.Auth;

/// <summary>
/// Resolves realm-specific Keycloak endpoint URIs.
/// </summary>
internal interface IKeycloakEndpointResolver
{
    /// <summary>
    /// Gets the realm issuer URI.
    /// </summary>
    Uri GetIssuerUri();

    /// <summary>
    /// Gets the OpenID Connect discovery endpoint URI.
    /// </summary>
    Uri GetWellKnownConfigurationUri();

    /// <summary>
    /// Gets the OpenID Connect authorization endpoint URI.
    /// </summary>
    Uri GetAuthorizationEndpointUri();

    /// <summary>
    /// Gets the OpenID Connect token endpoint URI.
    /// </summary>
    Uri GetTokenEndpointUri();

    /// <summary>
    /// Gets the OpenID Connect logout endpoint URI.
    /// </summary>
    Uri GetLogoutEndpointUri();

    /// <summary>
    /// Gets the OpenID Connect JWK set URI.
    /// </summary>
    Uri GetJwksUri();

    /// <summary>
    /// Gets the account management console URI.
    /// </summary>
    Uri GetAccountManagementUri();

    /// <summary>
    /// Gets the backend callback URI used for the authorization code flow.
    /// </summary>
    Uri GetCallbackUri();
}
