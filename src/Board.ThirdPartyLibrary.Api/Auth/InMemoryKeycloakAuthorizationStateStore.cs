using Microsoft.Extensions.Caching.Memory;

namespace Board.ThirdPartyLibrary.Api.Auth;

/// <summary>
/// In-memory authorization state store for local browser login flows.
/// </summary>
internal sealed class InMemoryKeycloakAuthorizationStateStore : IKeycloakAuthorizationStateStore
{
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(10);
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryKeycloakAuthorizationStateStore"/> class.
    /// </summary>
    /// <param name="memoryCache">Application memory cache.</param>
    public InMemoryKeycloakAuthorizationStateStore(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    /// <inheritdoc />
    public KeycloakAuthorizationState Create()
    {
        var authorizationState = new KeycloakAuthorizationState(
            State: KeycloakPkce.GenerateOpaqueValue(),
            CodeVerifier: KeycloakPkce.GenerateCodeVerifier());

        _memoryCache.Set(authorizationState.State, authorizationState, DefaultLifetime);
        return authorizationState;
    }

    /// <inheritdoc />
    public bool TryTake(string state, out KeycloakAuthorizationState? authorizationState)
    {
        if (_memoryCache.TryGetValue(state, out authorizationState))
        {
            _memoryCache.Remove(state);
            return true;
        }

        authorizationState = null;
        return false;
    }
}
