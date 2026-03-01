namespace Board.ThirdPartyLibrary.Api.Auth;

/// <summary>
/// Parameters required to exchange an authorization code for tokens.
/// </summary>
/// <param name="Code">Authorization code returned by Keycloak.</param>
/// <param name="CodeVerifier">PKCE code verifier associated with the original login request.</param>
/// <param name="RedirectUri">Redirect URI used for the original authorization request.</param>
internal sealed record KeycloakTokenExchangeRequest(string Code, string CodeVerifier, Uri RedirectUri);
