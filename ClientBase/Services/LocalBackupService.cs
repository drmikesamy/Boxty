using System.Net.Http.Json;
using System.Text.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using MudBlazor;
using Microsoft.JSInterop;
using Blazor.SubtleCrypto;

namespace Boxty.ClientBase.Services;

/// <summary>
/// Implementation of local backup service using server-generated encryption keys
/// This service provides a simplified encryption approach for local storage backups
/// Maintains up to 5 backups per key, rotating oldest when limit is reached
/// Only backs up when data has actually changed
/// </summary>
public class LocalBackupService : ILocalBackupService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILogger<LocalBackupService> _logger;
    private readonly ISnackbar _snackbar;
    private readonly IJSRuntime _jsRuntime;

    private string? _encryptionKey;
    private ICryptoService? _cryptoService;
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initSemaphore = new(1, 1);

    private const int MaxBackupsPerKey = 5;

    public bool IsReady => _isInitialized && !string.IsNullOrEmpty(_encryptionKey) && _cryptoService != null;

    public event EventHandler<bool>? ReadyStateChanged;

    public LocalBackupService(
        HttpClient httpClient,
        ILocalStorageService localStorage,
        AuthenticationStateProvider authStateProvider,
        ILogger<LocalBackupService> logger,
        ISnackbar snackbar,
        IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
        _logger = logger;
        _snackbar = snackbar;
        _jsRuntime = jsRuntime;
    }

    public async Task<bool> InitializeAsync()
    {
        await _initSemaphore.WaitAsync();
        try
        {
            if (_isInitialized)
                return IsReady;

            var authState = await _authStateProvider.GetAuthenticationStateAsync();
            if (!authState.User.Identity?.IsAuthenticated == true)
            {
                _logger.LogWarning("User is not authenticated, cannot initialize encryption service");
                return false;
            }

            try
            {
                // Fetch the user's encryption key from the server  
                var keyBytes = await _httpClient.GetFromJsonAsync<byte[]>("api/encryption/userkey");

                if (keyBytes != null && keyBytes.Length > 0)
                {
                    // Store the raw key for encryption
                    _encryptionKey = Convert.ToBase64String(keyBytes);

                    // Initialize the CryptoService with the encryption key
                    var options = new CryptoOptions() { Key = _encryptionKey };
                    _cryptoService = new CryptoService(_jsRuntime, options);

                    _isInitialized = true;
                    _logger.LogInformation("Encryption service initialized successfully");
                    ReadyStateChanged?.Invoke(this, true);
                    return true;
                }
            }
            catch (HttpRequestException ex)
            {
                _snackbar.Add("Failed to fetch encryption key from server (network error). Please manually save your work and refresh the screen.", Severity.Error);
                _logger.LogWarning(ex, "Failed to fetch encryption key from server (network error)");
            }
            catch (TaskCanceledException ex)
            {
                _snackbar.Add("Request to fetch local backup encryption key timed out. Please manually save your work and refresh the screen.", Severity.Error);
                _logger.LogWarning(ex, "Request to fetch local backup encryption key timed out");
            }
            catch (Exception ex)
            {
                _snackbar.Add("Failed to fetch local backup encryption key from server (unexpected error). Please manually save your work and refresh the screen.", Severity.Error);
                _logger.LogError(ex, "Failed to initialize encryption service");
            }

            ReadyStateChanged?.Invoke(this, false);
            return false;
        }
        finally
        {
            _initSemaphore.Release();
        }
    }

    public async Task<bool> BackupAsync<T>(string key, T data, BackupMetadata? metadata = null) where T : class
    {
        return await BackupInternalAsync(key, data, metadata, showNotifications: true);
    }

    public async Task<bool> BackupSilentAsync<T>(string key, T data, BackupMetadata? metadata = null) where T : class
    {
        return await BackupInternalAsync(key, data, metadata, showNotifications: false);
    }

    private async Task<bool> BackupInternalAsync<T>(string key, T data, BackupMetadata? metadata, bool showNotifications) where T : class
    {
        if (!IsReady)
        {
            _logger.LogWarning("Backup service not ready, attempting to initialize");
            if (!await InitializeAsync())
            {
                _logger.LogError("Cannot backup data: encryption service is not ready");
                if (showNotifications)
                    _snackbar.Add("Backup service not ready", Severity.Warning);
                return false;
            }
        }

        try
        {
            // Check if data has changed compared to the most recent backup
            var hasChanged = await HasDataChangedAsync(key, data);
            if (!hasChanged)
            {
                _logger.LogDebug("Data has not changed for key: {Key}, skipping backup", key);
                return true; // Return true since no backup is needed
            }

            metadata ??= new BackupMetadata();
            metadata.ObjectType = typeof(T).Name;
            metadata.LastModified = DateTime.UtcNow;

            var dataJson = JsonSerializer.Serialize(data);

            // Use CryptoService for proper encryption
            var encryptedResult = await _cryptoService!.EncryptAsync(dataJson);
            if (string.IsNullOrEmpty(encryptedResult.Value))
            {
                _logger.LogError("Failed to encrypt data for backup");
                if (showNotifications)
                    _snackbar.Add("Failed to encrypt backup data", Severity.Error);
                return false;
            }

            var backup = new EncryptedBackup
            {
                EncryptedData = encryptedResult.Value,
                Metadata = metadata,
                CreatedAt = DateTime.UtcNow,
                DataHash = ComputeDataHash(dataJson) // Store hash for change detection
            };

            // Get existing backup rotation data
            var rotationData = await GetBackupRotationDataAsync(key);

            // Determine the next backup slot (0-4)
            var nextSlot = (rotationData.CurrentSlot + 1) % MaxBackupsPerKey;

            // Store the backup with slot suffix
            var backupKey = $"backup_{key}_{nextSlot}";
            var backupJson = JsonSerializer.Serialize(backup);
            await _localStorage.SetItemAsync(backupKey, backupJson);

            // Update rotation data
            rotationData.CurrentSlot = nextSlot;
            rotationData.BackupCount = Math.Min(rotationData.BackupCount + 1, MaxBackupsPerKey);
            rotationData.LastBackupTime = DateTime.UtcNow;

            await SaveBackupRotationDataAsync(key, rotationData);

            // Update the backup index
            await UpdateBackupIndexAsync(key, true);

            _logger.LogDebug("Successfully backed up data for key: {Key} in slot: {Slot}", key, nextSlot);
            if (showNotifications)
                _snackbar.Add("Offline backup saved successfully", Severity.Success);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to backup data for key: {Key}", key);
            if (showNotifications)
                _snackbar.Add($"Error saving offline backup: {ex.Message}", Severity.Error);
            return false;
        }
    }

    /// <summary>
    /// Checks if the data has changed compared to the most recent backup
    /// </summary>
    private async Task<bool> HasDataChangedAsync<T>(string key, T data) where T : class
    {
        try
        {
            var rotationData = await GetBackupRotationDataAsync(key);
            if (rotationData.BackupCount == 0)
            {
                // No existing backup, so data has "changed" (needs first backup)
                return true;
            }

            var mostRecentSlot = rotationData.CurrentSlot;
            var backupKey = $"backup_{key}_{mostRecentSlot}";

            var backupJson = await _localStorage.GetItemAsync<string>(backupKey);
            if (string.IsNullOrEmpty(backupJson))
            {
                // Backup data missing, treat as changed
                return true;
            }

            var backup = JsonSerializer.Deserialize<EncryptedBackup>(backupJson);
            if (backup == null)
            {
                // Corrupted backup, treat as changed
                return true;
            }

            // Compute hash of current data
            var currentDataJson = JsonSerializer.Serialize(data);
            var currentHash = ComputeDataHash(currentDataJson);

            // Compare with stored hash if available
            if (!string.IsNullOrEmpty(backup.DataHash))
            {
                return currentHash != backup.DataHash;
            }

            // Fallback: decrypt and compare actual data if no hash is stored
            var decryptedData = await _cryptoService!.DecryptAsync(backup.EncryptedData);
            if (string.IsNullOrEmpty(decryptedData))
            {
                // Can't decrypt, treat as changed
                return true;
            }

            // Compare JSON strings (normalized)
            return NormalizeJson(currentDataJson) != NormalizeJson(decryptedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check if data has changed for key: {Key}", key);
            // On error, assume data has changed to ensure backup happens
            return true;
        }
    }

    /// <summary>
    /// Computes a SHA256 hash of the data for change detection
    /// </summary>
    private string ComputeDataHash(string data)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Normalizes JSON string for comparison by removing whitespace variations
    /// </summary>
    private string NormalizeJson(string json)
    {
        try
        {
            // Parse and re-serialize to normalize formatting
            var obj = JsonSerializer.Deserialize<object>(json);
            return JsonSerializer.Serialize(obj);
        }
        catch
        {
            // If parsing fails, return original string
            return json;
        }
    }

    public async Task<T?> RestoreAsync<T>(string key) where T : class
    {
        return await RestoreInternalAsync<T>(key, showNotifications: true);
    }

    public async Task<T?> RestoreSilentAsync<T>(string key) where T : class
    {
        return await RestoreInternalAsync<T>(key, showNotifications: false);
    }

    private async Task<T?> RestoreInternalAsync<T>(string key, bool showNotifications) where T : class
    {
        if (!IsReady)
        {
            _logger.LogWarning("Backup service not ready, attempting to initialize");
            if (!await InitializeAsync())
            {
                _logger.LogError("Cannot restore data: encryption service is not ready");
                if (showNotifications)
                    _snackbar.Add("Backup service not ready", Severity.Warning);
                return null;
            }
        }

        try
        {
            // Get the most recent backup
            var rotationData = await GetBackupRotationDataAsync(key);
            if (rotationData.BackupCount == 0)
            {
                _logger.LogDebug("No backup found for key: {Key}", key);
                if (showNotifications)
                    _snackbar.Add("No offline backup found", Severity.Info);
                return null;
            }

            var mostRecentSlot = rotationData.CurrentSlot;
            var backupKey = $"backup_{key}_{mostRecentSlot}";

            var backupJson = await _localStorage.GetItemAsync<string>(backupKey);
            if (string.IsNullOrEmpty(backupJson))
            {
                _logger.LogWarning("Backup data missing for key: {Key}, slot: {Slot}", key, mostRecentSlot);
                if (showNotifications)
                    _snackbar.Add("Backup data is missing", Severity.Error);
                return null;
            }

            var backup = JsonSerializer.Deserialize<EncryptedBackup>(backupJson);
            if (backup == null)
            {
                _logger.LogWarning("Failed to deserialize backup for key: {Key}", key);
                if (showNotifications)
                    _snackbar.Add("Backup data is corrupted", Severity.Error);
                return null;
            }

            var decryptedData = await _cryptoService!.DecryptAsync(backup.EncryptedData);
            if (string.IsNullOrEmpty(decryptedData))
            {
                _logger.LogError("Failed to decrypt backup data for key: {Key}", key);
                if (showNotifications)
                    _snackbar.Add("Failed to decrypt backup data", Severity.Error);
                return null;
            }

            var result = JsonSerializer.Deserialize<T>(decryptedData);
            _logger.LogDebug("Successfully restored data for key: {Key}", key);
            if (showNotifications)
                _snackbar.Add("Offline backup restored successfully", Severity.Success);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore data for key: {Key}", key);
            if (showNotifications)
                _snackbar.Add($"Error restoring offline backup: {ex.Message}", Severity.Error);
            return null;
        }
    }

    /// <summary>
    /// Gets the last 4 backups for a given key, ordered from newest to oldest
    /// </summary>
    public async Task<List<T?>> GetLastFiveBackupsAsync<T>(string key) where T : class
    {
        var backups = new List<T?>();

        if (!IsReady)
        {
            _logger.LogWarning("Backup service not ready, attempting to initialize");
            if (!await InitializeAsync())
            {
                _logger.LogError("Cannot get backups: encryption service is not ready");
                return backups;
            }
        }

        try
        {
            var rotationData = await GetBackupRotationDataAsync(key);
            if (rotationData.BackupCount == 0)
            {
                _logger.LogDebug("No backups found for key: {Key}", key);
                return backups;
            }

            // Get up to 4 most recent backups (excluding the current/newest one)
            var backupsToRetrieve = Math.Min(5, rotationData.BackupCount - 1);

            for (int i = 1; i <= backupsToRetrieve; i++)
            {
                // Calculate slot for backup that is 'i' positions before current
                var slotIndex = (rotationData.CurrentSlot - i + MaxBackupsPerKey) % MaxBackupsPerKey;
                var backupKey = $"backup_{key}_{slotIndex}";

                var backupJson = await _localStorage.GetItemAsync<string>(backupKey);
                if (!string.IsNullOrEmpty(backupJson))
                {
                    var backup = JsonSerializer.Deserialize<EncryptedBackup>(backupJson);
                    if (backup != null)
                    {
                        var decryptedData = await _cryptoService!.DecryptAsync(backup.EncryptedData);
                        if (!string.IsNullOrEmpty(decryptedData))
                        {
                            var result = JsonSerializer.Deserialize<T>(decryptedData);
                            backups.Add(result);
                        }
                        else
                        {
                            backups.Add(null);
                        }
                    }
                    else
                    {
                        backups.Add(null);
                    }
                }
                else
                {
                    backups.Add(null);
                }
            }

            _logger.LogDebug("Retrieved {Count} previous backups for key: {Key}", backups.Count, key);
            return backups;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get previous backups for key: {Key}", key);
            return backups;
        }
    }

    public async Task<BackupMetadata?> GetBackupMetadataAsync(string key)
    {
        try
        {
            var rotationData = await GetBackupRotationDataAsync(key);
            if (rotationData.BackupCount == 0)
                return null;

            var mostRecentSlot = rotationData.CurrentSlot;
            var backupKey = $"backup_{key}_{mostRecentSlot}";

            var backupJson = await _localStorage.GetItemAsync<string>(backupKey);
            if (string.IsNullOrEmpty(backupJson))
                return null;

            var backup = JsonSerializer.Deserialize<EncryptedBackup>(backupJson);
            return backup?.Metadata;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backup metadata for key: {Key}", key);
            return null;
        }
    }

    public async Task<bool> HasBackupAsync(string key)
    {
        try
        {
            var rotationData = await GetBackupRotationDataAsync(key);
            return rotationData.BackupCount > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check backup existence for key: {Key}", key);
            return false;
        }
    }

    public async Task<bool> RemoveBackupAsync(string key)
    {
        try
        {
            var rotationData = await GetBackupRotationDataAsync(key);

            // Remove all backup slots for this key
            for (int i = 0; i < MaxBackupsPerKey; i++)
            {
                await _localStorage.RemoveItemAsync($"backup_{key}_{i}");
            }

            // Remove rotation data
            await _localStorage.RemoveItemAsync($"rotation_{key}");

            await UpdateBackupIndexAsync(key, false);
            _logger.LogDebug("Successfully removed all backups for key: {Key}", key);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove backup for key: {Key}", key);
            return false;
        }
    }

    public async Task<List<string>> GetBackupKeysAsync(string pattern)
    {
        try
        {
            // Get keys from the maintained index
            var indexJson = await _localStorage.GetItemAsync<string>("backup_index");
            if (!string.IsNullOrEmpty(indexJson))
            {
                var index = JsonSerializer.Deserialize<List<string>>(indexJson) ?? new List<string>();

                // Simple pattern matching (you could use regex for more complex patterns)
                var patternWithoutWildcard = pattern.Replace("*", "").Replace("backup_", "");
                var keys = index.Where(k => k.Contains(patternWithoutWildcard)).ToList();
                return keys;
            }

            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backup keys for pattern: {Pattern}", pattern);
            return new List<string>();
        }
    }
    public async Task<DateTime?> GetLastBackupTime(Guid modelId)
    {
        try
        {
            var rotationData = await GetBackupRotationDataAsync(modelId.ToString());
            return rotationData.LastBackupTime;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get last backup time for model ID: {ModelId}", modelId);
            return null;
        }
    }

    public async Task<DateTime?> GetLastBackupTime(string key)
    {
        try
        {
            var rotationData = await GetBackupRotationDataAsync(key);
            return rotationData.LastBackupTime;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get last backup time for key: {Key}", key);
            return null;
        }
    }

    public void ClearKey()
    {
        _encryptionKey = null;
        _cryptoService = null;
        _isInitialized = false;
        ReadyStateChanged?.Invoke(this, false);
        _logger.LogInformation("Encryption key cleared from memory");
    }

    // Helper method to maintain backup index
    private async Task UpdateBackupIndexAsync(string key, bool add = true)
    {
        try
        {
            var indexJson = await _localStorage.GetItemAsync<string>("backup_index");
            var index = string.IsNullOrEmpty(indexJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(indexJson) ?? new List<string>();

            if (add && !index.Contains(key))
            {
                index.Add(key);
            }
            else if (!add)
            {
                index.Remove(key);
            }

            await _localStorage.SetItemAsync("backup_index", JsonSerializer.Serialize(index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update backup index");
        }
    }

    // Rotation data methods
    private async Task<BackupRotationData> GetBackupRotationDataAsync(string key)
    {
        try
        {
            var dataJson = await _localStorage.GetItemAsync<string>($"rotation_{key}");
            if (!string.IsNullOrEmpty(dataJson))
            {
                return JsonSerializer.Deserialize<BackupRotationData>(dataJson) ?? new BackupRotationData();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backup rotation data for key: {Key}", key);
        }

        return new BackupRotationData();
    }

    private async Task SaveBackupRotationDataAsync(string key, BackupRotationData rotationData)
    {
        try
        {
            await _localStorage.SetItemAsync($"rotation_{key}", JsonSerializer.Serialize(rotationData));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save backup rotation data for key: {Key}", key);
        }
    }

    public void Dispose()
    {
        ClearKey();
        _initSemaphore?.Dispose();
    }
}

/// <summary>
/// Tracks backup rotation state for a given key
/// </summary>
public class BackupRotationData
{
    public int CurrentSlot { get; set; } = -1; // Start at -1 so first backup goes to slot 0
    public int BackupCount { get; set; } = 0;
    public DateTime? LastBackupTime { get; set; }
}
