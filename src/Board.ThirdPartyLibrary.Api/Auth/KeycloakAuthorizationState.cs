namespace Board.ThirdPartyLibrary.Api.Auth;

/// <summary>
/// Ephemeral state stored between the authorization redirect and callback exchange.
/// </summary>
/// <param name="State">Opaque anti-forgery state value sent to Keycloak.</param>
/// <param name="CodeVerifier">PKCE code verifier paired with the outgoing challenge.</param>
internal sealed record KeycloakAuthorizationState(string State, string CodeVerifier);
