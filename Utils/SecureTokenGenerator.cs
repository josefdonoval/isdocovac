using System.Security.Cryptography;

namespace Isdocovac.Utils;

public static class SecureTokenGenerator
{
    /// <summary>
    /// Generates a cryptographically secure random token.
    /// </summary>
    /// <param name="length">The length of the token in bytes (default: 32)</param>
    /// <returns>A Base64URL-encoded secure random token</returns>
    public static string GenerateToken(int length = 32)
    {
        var randomBytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);

        // Convert to Base64URL encoding (URL-safe)
        return Convert.ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
