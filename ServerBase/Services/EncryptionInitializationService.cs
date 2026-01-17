using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Boxty.ServerBase.Services;

namespace Boxty.ServerBase.Modules.Auth.Setup
{
    /// <summary>
    /// Background service for initializing encryption system on startup
    /// Ensures master key exists in Azure Key Vault
    /// </summary>
    public class EncryptionInitializationService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EncryptionInitializationService> _logger;

        public EncryptionInitializationService(
            IServiceProvider serviceProvider,
            ILogger<EncryptionInitializationService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _logger.LogInformation("Starting encryption system initialization...");

                using var scope = _serviceProvider.CreateScope();
                var keyVaultService = scope.ServiceProvider.GetRequiredService<IAzureKeyVaultService>();

                // Try to get the master key to verify it exists
                try
                {
                    await keyVaultService.GetMasterKeyAsync();
                    _logger.LogInformation("Master key found in Azure Key Vault - encryption system ready");
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("Secret not found"))
                {
                    _logger.LogWarning("Master key not found in Azure Key Vault. This is expected for initial setup.");
                    _logger.LogWarning("Please create the master key manually using the setup guide or Azure portal.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to verify master key in Azure Key Vault");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize encryption system");
            }
        }
    }
}
