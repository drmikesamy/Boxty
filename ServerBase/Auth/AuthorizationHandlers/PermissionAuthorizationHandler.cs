using System.Security.Claims;
using Boxty.ServerBase.Auth.Requirements;
using Boxty.ServerBase.Services;
using Microsoft.AspNetCore.Authorization;

namespace Boxty.ServerBase.Auth.AuthorizationHandlers
{
    /// <summary>
    /// Generic authorization handler that checks if a user has the required permission
    /// using the role permission cache service
    /// </summary>
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IRolePermissionCacheService _rolePermissionCacheService;
        private readonly IUserContextService _userContextService;

        public PermissionAuthorizationHandler(
            IRolePermissionCacheService rolePermissionCacheService,
            IUserContextService userContextService)
        {
            _rolePermissionCacheService = rolePermissionCacheService ?? throw new ArgumentNullException(nameof(rolePermissionCacheService));
            _userContextService = userContextService ?? throw new ArgumentNullException(nameof(userContextService));
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            // Get user roles from the context
            var userRoles = _userContextService.GetRoles(context.User);

            if (userRoles == null || !userRoles.Any())
            {
                // No roles found, deny access
                return Task.CompletedTask;
            }

            // Check if any of the user's roles has the required permission
            foreach (var role in userRoles)
            {
                if (_rolePermissionCacheService.HasPermission(requirement.Permission, role))
                {
                    context.Succeed(requirement);
                    return Task.CompletedTask;
                }
            }

            // No role had the required permission, deny access
            return Task.CompletedTask;
        }
    }
}
