namespace Board.ThirdPartyLibrary.Api.Auth;

/// <summary>
/// Result of an authorization-code token exchange.
/// </summary>
internal sealed class KeycloakTokenExchangeResult
{
    /// <summary>
    /// Gets a successful token exchange result.
    /// </summary>
    /// <param name="accessToken">OIDC access token.</param>
    /// <param name="tokenType">OAuth token type.</param>
    /// <param name="expiresInSeconds">Access token lifetime in seconds.</param>
    /// <param name="scope">Granted scope string.</param>
    /// <param name="refreshToken">Optional refresh token.</param>
    /// <param name="idToken">Optional ID token.</param>
    /// <returns>A successful token exchange result.</returns>
    public static KeycloakTokenExchangeResult Success(
        string accessToken,
        string tokenType,
        int expiresInSeconds,
        string? scope,
        string? refreshToken,
        string? idToken) =>
        new()
        {
            Succeeded = true,
            AccessToken = accessToken,
            TokenType = tokenType,
            ExpiresInSeconds = expiresInSeconds,
            Scope = scope,
            RefreshToken = refreshToken,
            IdToken = idToken
        };

    /// <summary>
    /// Gets a failed token exchange result.
    /// </summary>
    /// <param name="error">Upstream OAuth error code.</param>
    /// <param name="errorDescription">Optional upstream OAuth error description.</param>
    /// <returns>A failed token exchange result.</returns>
    public static KeycloakTokenExchangeResult Failure(string? error, string? errorDescription) =>
        new()
        {
            Succeeded = false,
            Error = error,
            ErrorDescription = errorDescription
        };

    /// <summary>
    /// Gets a value indicating whether the token exchange succeeded.
    /// </summary>
    public bool Succeeded { get; private init; }

    /// <summary>
    /// Gets the access token returned by Keycloak.
    /// </summary>
    public string? AccessToken { get; private init; }

    /// <summary>
    /// Gets the optional refresh token returned by Keycloak.
    /// </summary>
    public string? RefreshToken { get; private init; }

    /// <summary>
    /// Gets the optional ID token returned by Keycloak.
    /// </summary>
    public string? IdToken { get; private init; }

    /// <summary>
    /// Gets the OAuth token type.
    /// </summary>
    public string? TokenType { get; private init; }

    /// <summary>
    /// Gets the access token lifetime in seconds.
    /// </summary>
    public int ExpiresInSeconds { get; private init; }

    /// <summary>
    /// Gets the granted scope string.
    /// </summary>
    public string? Scope { get; private init; }

    /// <summary>
    /// Gets the upstream OAuth error code for failed exchanges.
    /// </summary>
    public string? Error { get; private init; }

    /// <summary>
    /// Gets the upstream OAuth error description for failed exchanges.
    /// </summary>
    public string? ErrorDescription { get; private init; }
}
