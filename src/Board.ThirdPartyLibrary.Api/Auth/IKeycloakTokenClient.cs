namespace Board.ThirdPartyLibrary.Api.Auth;

/// <summary>
/// Exchanges authorization codes for Keycloak tokens.
/// </summary>
internal interface IKeycloakTokenClient
{
    /// <summary>
    /// Exchanges an authorization code for tokens.
    /// </summary>
    /// <param name="request">Authorization code exchange request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The token exchange result.</returns>
    Task<KeycloakTokenExchangeResult> ExchangeAuthorizationCodeAsync(
        KeycloakTokenExchangeRequest request,
        CancellationToken cancellationToken = default);
}
