using System.Security.Cryptography;
using System.Text;

namespace Isdocovac.Utils;

public static class TokenHasher
{
    /// <summary>
    /// Computes a SHA256 hash of the input token.
    /// </summary>
    /// <param name="token">The plain text token to hash</param>
    /// <returns>A Base64-encoded hash of the token</returns>
    public static string HashToken(string token)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(token);
        var hashBytes = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}
