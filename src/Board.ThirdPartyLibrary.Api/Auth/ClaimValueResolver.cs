using System.Security.Claims;

namespace Board.ThirdPartyLibrary.Api.Auth;

/// <summary>
/// Resolves common authenticated claim values from either raw JWT names or framework-mapped claim types.
/// </summary>
internal static class ClaimValueResolver
{
    /// <summary>
    /// Gets the stable subject identifier from the supplied claims.
    /// </summary>
    /// <param name="claims">Authenticated claims to inspect.</param>
    /// <returns>The resolved subject claim when available; otherwise <see langword="null" />.</returns>
    public static string? GetSubject(IEnumerable<Claim> claims) =>
        GetClaimValue(claims, "sub") ?? GetClaimValue(claims, ClaimTypes.NameIdentifier);

    /// <summary>
    /// Gets the first matching claim value from the supplied claims.
    /// </summary>
    /// <param name="claims">Authenticated claims to inspect.</param>
    /// <param name="type">Claim type to resolve.</param>
    /// <returns>The matching claim value when available; otherwise <see langword="null" />.</returns>
    public static string? GetClaimValue(IEnumerable<Claim> claims, string type) =>
        claims.FirstOrDefault(claim => string.Equals(claim.Type, type, StringComparison.OrdinalIgnoreCase))?.Value;
}
