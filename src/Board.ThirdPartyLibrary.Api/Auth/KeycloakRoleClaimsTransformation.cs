using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Board.ThirdPartyLibrary.Api.Auth;

/// <summary>
/// Adds Keycloak realm roles as standard role claims after JWT validation.
/// </summary>
internal sealed class KeycloakRoleClaimsTransformation : IClaimsTransformation
{
    /// <inheritdoc />
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return Task.FromResult(principal);
        }

        var realmAccessClaim = identity.FindFirst("realm_access");
        if (realmAccessClaim is null)
        {
            return Task.FromResult(principal);
        }

        using var document = JsonDocument.Parse(realmAccessClaim.Value);
        if (!document.RootElement.TryGetProperty("roles", out var rolesElement) || rolesElement.ValueKind != JsonValueKind.Array)
        {
            return Task.FromResult(principal);
        }

        foreach (var roleValue in rolesElement.EnumerateArray().Select(element => element.GetString()).Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var resolvedRoleValue = roleValue!;
            if (!identity.HasClaim(ClaimTypes.Role, resolvedRoleValue))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, resolvedRoleValue));
            }
        }

        return Task.FromResult(principal);
    }
}
