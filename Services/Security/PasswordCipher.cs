using System.Security.Cryptography;

namespace Isdocovac.Services.Security;

public interface IPasswordCipher
{
    /// <summary>
    /// Encrypts the plaintext with AES-GCM using the configured key.
    /// Output format (Base64): nonce (12B) | ciphertext (var) | tag (16B).
    /// </summary>
    string Encrypt(string plaintext);

    /// <summary>
    /// Decrypts a Base64 ciphertext produced by <see cref="Encrypt"/>.
    /// </summary>
    string Decrypt(string encrypted);
}

public class AesGcmPasswordCipher : IPasswordCipher
{
    private const int NonceBytes = 12;
    private const int TagBytes = 16;

    private readonly byte[] _key;

    public AesGcmPasswordCipher(IConfiguration configuration)
    {
        var keyBase64 = configuration["EmailIngestion:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(keyBase64))
        {
            throw new InvalidOperationException(
                "EmailIngestion:EncryptionKey is not configured. Provide a Base64-encoded 32-byte key.");
        }

        try
        {
            _key = Convert.FromBase64String(keyBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "EmailIngestion:EncryptionKey must be valid Base64.", ex);
        }

        if (_key.Length != 32)
        {
            throw new InvalidOperationException(
                $"EmailIngestion:EncryptionKey must decode to 32 bytes (got {_key.Length}).");
        }
    }

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceBytes);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagBytes];

        using var aes = new AesGcm(_key, TagBytes);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        var output = new byte[NonceBytes + ciphertext.Length + TagBytes];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceBytes);
        Buffer.BlockCopy(ciphertext, 0, output, NonceBytes, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, output, NonceBytes + ciphertext.Length, TagBytes);

        return Convert.ToBase64String(output);
    }

    public string Decrypt(string encrypted)
    {
        ArgumentNullException.ThrowIfNull(encrypted);

        var input = Convert.FromBase64String(encrypted);
        if (input.Length < NonceBytes + TagBytes)
        {
            throw new CryptographicException("Ciphertext is shorter than minimum AES-GCM envelope.");
        }

        var nonce = new byte[NonceBytes];
        var tag = new byte[TagBytes];
        var ciphertext = new byte[input.Length - NonceBytes - TagBytes];

        Buffer.BlockCopy(input, 0, nonce, 0, NonceBytes);
        Buffer.BlockCopy(input, NonceBytes, ciphertext, 0, ciphertext.Length);
        Buffer.BlockCopy(input, NonceBytes + ciphertext.Length, tag, 0, TagBytes);

        var plaintextBytes = new byte[ciphertext.Length];
        using var aes = new AesGcm(_key, TagBytes);
        aes.Decrypt(nonce, ciphertext, tag, plaintextBytes);

        return System.Text.Encoding.UTF8.GetString(plaintextBytes);
    }
}
