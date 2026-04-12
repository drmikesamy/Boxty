using System.Collections.Concurrent;
using Boxty.ServerBase.Queries.ModuleQueries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Boxty.ServerBase.Services
{
    public class RolePermissionCacheService : IRolePermissionCacheService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<RolePermissionCacheService> _logger;
        private readonly ConcurrentDictionary<string, HashSet<string>> _rolePermissionCache = new(StringComparer.OrdinalIgnoreCase);

        public RolePermissionCacheService(IServiceProvider serviceProvider, ILogger<RolePermissionCacheService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task InitAsync()
        {
            try
            {
                // Create a scope to resolve the scoped query service
                using var scope = _serviceProvider.CreateScope();
                var getAllRolesQuery = scope.ServiceProvider.GetRequiredService<IGetAllRolesWithPermissionsQuery>();

                var roles = await getAllRolesQuery.Handle();

                _logger.LogInformation("Loaded {RoleCount} roles from permission provider", roles.Count());

                _rolePermissionCache.Clear();

                foreach (var role in roles)
                {
                    var permissions = new HashSet<string>(role.Permissions.Select(p => p.Name));
                    _rolePermissionCache.TryAdd(role.Name, permissions);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't throw to prevent application startup issues
                _logger.LogWarning(ex, "Failed to initialize role permission cache");
            }
        }

        public bool HasPermission(string permissionName, string roleName)
        {
            return _rolePermissionCache.TryGetValue(roleName, out var permissions) &&
                   (permissions.Contains("*") || permissions.Contains(permissionName));
        }
    }
}
