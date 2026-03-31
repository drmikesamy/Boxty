using Boxty.ServerBase.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Boxty.ServerBase.Services
{
    public class RolePermissionCacheRefreshBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RolePermissionCacheRefreshBackgroundService> _logger;
        private readonly IOptionsMonitor<AppOptions> _optionsMonitor;

        public RolePermissionCacheRefreshBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<RolePermissionCacheRefreshBackgroundService> logger,
            IOptionsMonitor<AppOptions> optionsMonitor)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _optionsMonitor = optionsMonitor;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var options = _optionsMonitor.CurrentValue.PermissionCache;
            if (!options.EnableAutoRefresh)
            {
                _logger.LogInformation("Permission cache auto-refresh is disabled.");
                return;
            }

            var intervalSeconds = options.AutoRefreshIntervalSeconds;
            if (intervalSeconds <= 0)
            {
                _logger.LogInformation("Permission cache auto-refresh interval is non-positive; skipping background refresh.");
                return;
            }

            _logger.LogInformation("Permission cache auto-refresh enabled with interval {IntervalSeconds}s.", intervalSeconds);

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(intervalSeconds));
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var hasNextTick = await timer.WaitForNextTickAsync(stoppingToken);
                    if (!hasNextTick)
                    {
                        break;
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var cacheService = scope.ServiceProvider.GetRequiredService<IRolePermissionCacheService>();
                    await cacheService.InitAsync();
                    _logger.LogDebug("Permission cache refreshed.");
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to refresh permission cache in background service.");
                }
            }
        }
    }
}
