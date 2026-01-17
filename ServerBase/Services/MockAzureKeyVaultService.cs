using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Boxty.ServerBase.Services
{
    /// <summary>
    /// Mock implementation of Azure Key Vault service for development
    /// Uses a static in-memory key instead of Azure Key Vault
    /// </summary>
    public class MockAzureKeyVaultService : IAzureKeyVaultService
    {
        private readonly ILogger<MockAzureKeyVaultService> _logger;
        private static readonly byte[] _mockMasterKey = GenerateMockKey();

        public MockAzureKeyVaultService(ILogger<MockAzureKeyVaultService> logger)
        {
            _logger = logger;
            _logger.LogWarning("Using MOCK Azure Key Vault Service - NOT for production use!");
        }

        private static byte[] GenerateMockKey()
        {
            // Generate a consistent mock key for development
            using var rng = RandomNumberGenerator.Create();
            var key = new byte[32]; // 256 bits
            rng.GetBytes(key);
            return key;
        }

        public async Task<byte[]> GetMasterKeyAsync()
        {
            _logger.LogDebug("Returning mock master key");
            await Task.Delay(10); // Simulate network delay
            return _mockMasterKey;
        }

        public async Task<string> GetMasterKeyVersionAsync()
        {
            await Task.Delay(10);
            return "mock-version-1.0";
        }

        public async Task<byte[]> EncryptUserKeyAsync(byte[] userKey)
        {
            _logger.LogDebug("Encrypting user key with mock master key");

            using var aes = Aes.Create();
            aes.Key = _mockMasterKey;
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            using var msEncrypt = new MemoryStream();

            // Write IV first, then encrypted data
            await msEncrypt.WriteAsync(aes.IV, 0, aes.IV.Length);

            using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
            await csEncrypt.WriteAsync(userKey, 0, userKey.Length);
            csEncrypt.FlushFinalBlock();

            return msEncrypt.ToArray();
        }

        public async Task<byte[]> DecryptUserKeyAsync(byte[] encryptedData, byte[] iv)
        {
            _logger.LogDebug("Decrypting user key with mock master key");

            using var aes = Aes.Create();
            aes.Key = _mockMasterKey;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            using var msDecrypt = new MemoryStream(encryptedData);
            using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);

            var decryptedKey = new byte[32]; // Assuming 256-bit user keys
            var bytesRead = await csDecrypt.ReadAsync(decryptedKey, 0, decryptedKey.Length);

            // Trim to actual size read
            var result = new byte[bytesRead];
            Array.Copy(decryptedKey, result, bytesRead);

            return result;
        }
    }
}
