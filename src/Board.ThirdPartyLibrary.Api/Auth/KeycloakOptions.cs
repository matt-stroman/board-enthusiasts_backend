namespace Board.ThirdPartyLibrary.Api.Auth;

/// <summary>
/// Configuration for the local Keycloak integration.
/// </summary>
internal sealed class KeycloakOptions
{
    /// <summary>
    /// Gets the configuration section name.
    /// </summary>
    public const string SectionName = "Authentication:Keycloak";

    /// <summary>
    /// Gets or sets the Keycloak server base URL.
    /// </summary>
    public string BaseUrl { get; set; } = "https://localhost:8443";

    /// <summary>
    /// Gets or sets the Keycloak realm name.
    /// </summary>
    public string Realm { get; set; } = "board-enthusiasts";

    /// <summary>
    /// Gets or sets the confidential client identifier used by the backend.
    /// </summary>
    public string ClientId { get; set; } = "board-enthusiasts-backend";

    /// <summary>
    /// Gets or sets the confidential client secret used by the backend.
    /// </summary>
    public string ClientSecret { get; set; } = "board-enthusiasts-backend-secret";

    /// <summary>
    /// Gets or sets the public backend base URL used for redirect URI generation.
    /// </summary>
    public string PublicBackendBaseUrl { get; set; } = "https://localhost:7085";

    /// <summary>
    /// Gets or sets a value indicating whether OpenID metadata must use HTTPS.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Gets or sets the scopes requested during login.
    /// </summary>
    public string[] Scopes { get; set; } = [];

    /// <summary>
    /// Gets or sets the configured external provider aliases advertised to clients.
    /// </summary>
    public string[] ExternalIdentityProviders { get; set; } = [];
}
