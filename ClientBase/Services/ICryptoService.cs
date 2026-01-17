namespace Boxty.ClientBase.Services;

/// <summary>
/// Service for managing cryptographic operations
/// </summary>
public interface ILocalCryptoService
{
    /// <summary>
    /// Encrypts a string and returns the encrypted data with the key
    /// </summary>
    /// <param name="data">The data to encrypt</param>
    /// <returns>Result containing encrypted value and secret key</returns>
    Task<CryptoResult> EncryptAsync(string data);

    /// <summary>
    /// Decrypts data using the provided input
    /// </summary>
    /// <param name="input">The encrypted data and key</param>
    /// <returns>The decrypted string</returns>
    Task<string> DecryptAsync(CryptoInput input);
}

/// <summary>
/// Result of encryption operation
/// </summary>
public class CryptoResult
{
    public string Value { get; set; } = string.Empty;
    public CryptoSecret Secret { get; set; } = new();
}

/// <summary>
/// Secret information for encryption/decryption
/// </summary>
public class CryptoSecret
{
    public string Key { get; set; } = string.Empty;
}

/// <summary>
/// Input for decryption operation
/// </summary>
public class CryptoInput
{
    public string Value { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
}
