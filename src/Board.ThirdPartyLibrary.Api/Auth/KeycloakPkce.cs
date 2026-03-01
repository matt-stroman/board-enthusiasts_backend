using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Board.ThirdPartyLibrary.Api.Auth;

/// <summary>
/// Helpers for PKCE and opaque state generation.
/// </summary>
internal static class KeycloakPkce
{
    /// <summary>
    /// Generates a PKCE code verifier.
    /// </summary>
    /// <returns>A base64url-encoded verifier string.</returns>
    public static string GenerateCodeVerifier() => GenerateOpaqueValue();

    /// <summary>
    /// Creates a SHA-256 PKCE code challenge for a verifier.
    /// </summary>
    /// <param name="codeVerifier">PKCE code verifier.</param>
    /// <returns>Base64url-encoded PKCE code challenge.</returns>
    public static string CreateCodeChallenge(string codeVerifier)
    {
        var bytes = Encoding.ASCII.GetBytes(codeVerifier);
        var hash = SHA256.HashData(bytes);
        return WebEncoders.Base64UrlEncode(hash);
    }

    /// <summary>
    /// Generates an opaque base64url-encoded value suitable for state or verifier usage.
    /// </summary>
    /// <returns>Opaque base64url-encoded value.</returns>
    public static string GenerateOpaqueValue()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return WebEncoders.Base64UrlEncode(bytes);
    }
}
