namespace Board.ThirdPartyLibrary.Api.Auth;

/// <summary>
/// Stores short-lived authorization state for the browser login flow.
/// </summary>
internal interface IKeycloakAuthorizationStateStore
{
    /// <summary>
    /// Creates and stores a new authorization state.
    /// </summary>
    /// <returns>The stored authorization state.</returns>
    KeycloakAuthorizationState Create();

    /// <summary>
    /// Retrieves and removes an authorization state.
    /// </summary>
    /// <param name="state">Opaque state value returned by Keycloak.</param>
    /// <param name="authorizationState">Resolved authorization state when present.</param>
    /// <returns><see langword="true"/> when a matching state was found; otherwise <see langword="false"/>.</returns>
    bool TryTake(string state, out KeycloakAuthorizationState? authorizationState);
}
