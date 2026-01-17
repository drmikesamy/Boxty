using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Boxty.ServerBase.Services
{
    public interface IAzureKeyVaultService
    {
        Task<byte[]> GetMasterKeyAsync();
        Task<byte[]> EncryptUserKeyAsync(byte[] userKey);
        Task<byte[]> DecryptUserKeyAsync(byte[] encryptedKey, byte[] iv);
        Task<string> GetMasterKeyVersionAsync();
    }

    /// <summary>
    /// Service for managing encryption keys using Azure Key Vault
    /// Provides master key management and AES encryption/decryption operations
    /// </summary>
    public class AzureKeyVaultService : IAzureKeyVaultService
    {
        private readonly SecretClient _secretClient;
        private readonly ILogger<AzureKeyVaultService> _logger;
        private readonly string _masterKeyName;
        private byte[]? _cachedMasterKey;
        private string? _cachedMasterKeyVersion;
        private DateTime _cacheExpiry = DateTime.MinValue;
        private readonly TimeSpan _cacheTimeout = TimeSpan.FromMinutes(15); // Cache for 15 minutes

        public AzureKeyVaultService(IConfiguration configuration, ILogger<AzureKeyVaultService> logger)
        {
            _logger = logger;

            var keyVaultUrl = configuration["KeyVault:VaultUri"]
                ?? throw new InvalidOperationException("KeyVault:VaultUri not configured");

            _masterKeyName = configuration["AzureKeyVault:MasterKeyName"] ?? "user-encryption-master-key";

            // Use DefaultAzureCredential for authentication
            // This supports multiple authentication methods in order:
            // 1. Environment variables (for local development)
            // 2. Managed Identity (for Azure deployments)
            // 3. Visual Studio / Azure CLI (for local development)
            var credential = new DefaultAzureCredential();
            _secretClient = new SecretClient(new Uri(keyVaultUrl), credential);

            _logger.LogInformation("Azure Key Vault service initialized with vault: {VaultUrl}", keyVaultUrl);
        }

        /// <summary>
        /// Gets the master encryption key from Azure Key Vault
        /// </summary>
        public async Task<byte[]> GetMasterKeyAsync()
        {
            try
            {
                // Check cache first
                if (_cachedMasterKey != null && DateTime.UtcNow < _cacheExpiry)
                {
                    _logger.LogDebug("Returning cached master key");
                    return _cachedMasterKey;
                }

                _logger.LogDebug("Fetching master key from Azure Key Vault");
                var response = await _secretClient.GetSecretAsync(_masterKeyName);
                var secret = response.Value;

                // Convert base64 secret to bytes
                var masterKeyBytes = Convert.FromBase64String(secret.Value);

                // Cache the key and version
                _cachedMasterKey = masterKeyBytes;
                _cachedMasterKeyVersion = secret.Properties.Version;
                _cacheExpiry = DateTime.UtcNow.Add(_cacheTimeout);

                _logger.LogInformation("Master key retrieved from Azure Key Vault, version: {Version}", secret.Properties.Version);
                return masterKeyBytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve master key from Azure Key Vault");
                throw new InvalidOperationException("Unable to retrieve master encryption key", ex);
            }
        }

        /// <summary>
        /// Gets the current master key version
        /// </summary>
        public async Task<string> GetMasterKeyVersionAsync()
        {
            try
            {
                // If we have cached version, return it
                if (_cachedMasterKeyVersion != null && DateTime.UtcNow < _cacheExpiry)
                {
                    return _cachedMasterKeyVersion;
                }

                // Otherwise fetch the secret to get the version
                await GetMasterKeyAsync(); // This will populate the cache
                return _cachedMasterKeyVersion ?? "unknown";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get master key version");
                return "unknown";
            }
        }

        /// <summary>
        /// Encrypts a user key using the master key from Azure Key Vault
        /// </summary>
        public async Task<byte[]> EncryptUserKeyAsync(byte[] userKey)
        {
            try
            {
                var masterKey = await GetMasterKeyAsync();

                using var aes = Aes.Create();
                aes.Key = masterKey;
                aes.GenerateIV();

                using var encryptor = aes.CreateEncryptor();
                using var msEncrypt = new MemoryStream();

                // Write IV first, then encrypted data
                await msEncrypt.WriteAsync(aes.IV, 0, aes.IV.Length);

                using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
                await csEncrypt.WriteAsync(userKey, 0, userKey.Length);
                csEncrypt.FlushFinalBlock();

                var result = msEncrypt.ToArray();
                _logger.LogDebug("User key encrypted successfully, output size: {Size} bytes", result.Length);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to encrypt user key");
                throw new InvalidOperationException("Failed to encrypt user key", ex);
            }
        }

        /// <summary>
        /// Decrypts a user key using the master key from Azure Key Vault
        /// </summary>
        public async Task<byte[]> DecryptUserKeyAsync(byte[] encryptedData, byte[] iv)
        {
            try
            {
                var masterKey = await GetMasterKeyAsync();

                using var aes = Aes.Create();
                aes.Key = masterKey;
                aes.IV = iv;

                using var decryptor = aes.CreateDecryptor();
                using var msDecrypt = new MemoryStream(encryptedData);
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);

                var decryptedKey = new byte[32]; // Assuming 256-bit user keys
                var bytesRead = await csDecrypt.ReadAsync(decryptedKey, 0, decryptedKey.Length);

                // Trim to actual size read
                var result = new byte[bytesRead];
                Array.Copy(decryptedKey, result, bytesRead);

                _logger.LogDebug("User key decrypted successfully, size: {Size} bytes", result.Length);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to decrypt user key");
                throw new InvalidOperationException("Failed to decrypt user key", ex);
            }
        }

        /// <summary>
        /// Creates and stores a new master key in Azure Key Vault (for initial setup)
        /// This method should only be called during initial deployment
        /// </summary>
        public async Task<string> CreateMasterKeyAsync()
        {
            try
            {
                // Generate a new 256-bit key
                using var rng = RandomNumberGenerator.Create();
                var keyBytes = new byte[32]; // 256 bits
                rng.GetBytes(keyBytes);

                var keyBase64 = Convert.ToBase64String(keyBytes);

                var secretResponse = await _secretClient.SetSecretAsync(_masterKeyName, keyBase64);

                _logger.LogWarning("NEW MASTER KEY CREATED in Azure Key Vault. Version: {Version}",
                    secretResponse.Value.Properties.Version);

                return secretResponse.Value.Properties.Version;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create master key in Azure Key Vault");
                throw new InvalidOperationException("Failed to create master key", ex);
            }
        }
    }
}
