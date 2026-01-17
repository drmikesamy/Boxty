using System.Text.Json;

namespace Boxty.ClientBase.Services;

/// <summary>
/// Service for managing encrypted local backups using server-generated keys
/// </summary>
public interface ILocalBackupService
{
    /// <summary>
    /// Indicates if the service is ready to encrypt/decrypt data
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Initializes the service by fetching the user's encryption key from the server
    /// </summary>
    /// <returns>True if initialization was successful, false otherwise</returns>
    Task<bool> InitializeAsync();

    /// <summary>
    /// Encrypts and stores an object to local storage
    /// </summary>
    /// <typeparam name="T">The type of object to backup</typeparam>
    /// <param name="key">The local storage key to use</param>
    /// <param name="data">The object to encrypt and store</param>
    /// <param name="metadata">Optional metadata to include with the backup</param>
    /// <returns>True if backup was successful, false otherwise</returns>
    Task<bool> BackupAsync<T>(string key, T data, BackupMetadata? metadata = null) where T : class;

    /// <summary>
    /// Encrypts and stores an object to local storage without user notifications
    /// </summary>
    /// <typeparam name="T">The type of object to backup</typeparam>
    /// <param name="key">The local storage key to use</param>
    /// <param name="data">The object to encrypt and store</param>
    /// <param name="metadata">Optional metadata to include with the backup</param>
    /// <returns>True if backup was successful, false otherwise</returns>
    Task<bool> BackupSilentAsync<T>(string key, T data, BackupMetadata? metadata = null) where T : class;

    /// <summary>
    /// Restores and decrypts an object from local storage
    /// </summary>
    /// <typeparam name="T">The type of object to restore</typeparam>
    /// <param name="key">The local storage key to retrieve</param>
    /// <returns>The restored object or null if not found or decryption failed</returns>
    Task<T?> RestoreAsync<T>(string key) where T : class;

    /// <summary>
    /// Restores and decrypts an object from local storage without user notifications
    /// </summary>
    /// <typeparam name="T">The type of object to restore</typeparam>
    /// <param name="key">The local storage key to retrieve</param>
    /// <returns>The restored object or null if not found or decryption failed</returns>
    Task<T?> RestoreSilentAsync<T>(string key) where T : class;

    /// <summary>
    /// Restores backup metadata without decrypting the full object
    /// </summary>
    /// <param name="key">The local storage key to check</param>
    /// <returns>The backup metadata or null if not found</returns>
    Task<BackupMetadata?> GetBackupMetadataAsync(string key);

    /// <summary>
    /// Checks if a backup exists for the given key
    /// </summary>
    /// <param name="key">The local storage key to check</param>
    /// <returns>True if backup exists, false otherwise</returns>
    Task<bool> HasBackupAsync(string key);

    /// <summary>
    /// Removes a backup from local storage
    /// </summary>
    /// <param name="key">The local storage key to remove</param>
    /// <returns>True if removal was successful, false otherwise</returns>
    Task<bool> RemoveBackupAsync(string key);

    /// <summary>
    /// Gets all backup keys that match a pattern
    /// </summary>
    /// <param name="pattern">Pattern to match (e.g., "offline-case-note-*")</param>
    /// <returns>List of matching backup keys</returns>
    Task<List<string>> GetBackupKeysAsync(string pattern);

    /// <summary>
    /// Gets the last backup time for a specific model
    /// </summary>
    /// <param name="modelId">The ID of the model to check</param>
    /// <returns>The last backup time or null if not found</returns>
    Task<DateTime?> GetLastBackupTime(Guid modelId);

    /// <summary>
    /// Gets the last backup time for a specific backup key
    /// </summary>
    /// <param name="key">The backup key to check</param>
    /// <returns>The last backup time or null if not found</returns>
    Task<DateTime?> GetLastBackupTime(string key);

    /// <summary>
    /// Gets the last 4 backups for a given key, ordered from newest to oldest
    /// </summary>
    /// <typeparam name="T">The type of object to restore</typeparam>
    /// <param name="key">The backup key to retrieve history for</param>
    /// <returns>List of up to 4 previous backups, excluding the current one</returns>
    Task<List<T?>> GetLastFiveBackupsAsync<T>(string key) where T : class;

    /// <summary>
    /// Clears the encryption key from memory
    /// </summary>
    void ClearKey();

    /// <summary>
    /// Event fired when the service becomes ready or unavailable
    /// </summary>
    event EventHandler<bool>? ReadyStateChanged;
}

/// <summary>
/// Metadata associated with a backup
/// </summary>
public class BackupMetadata
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastModified { get; set; }
    public string? ObjectType { get; set; }
    public Guid? ObjectId { get; set; }
    public string? UserNotes { get; set; }
    public int Version { get; set; } = 1;
    public Dictionary<string, object> CustomData { get; set; } = new();
}

/// <summary>
/// Container for encrypted backup data
/// </summary>
internal class EncryptedBackup
{
    public string EncryptedData { get; set; } = string.Empty;
    public BackupMetadata Metadata { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Version { get; set; } = "1.0";
    public string? DataHash { get; set; } // SHA256 hash for change detection
}
