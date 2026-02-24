using System.Security.Claims;
using Boxty.ServerBase.Services;
using FS.Keycloak.RestApiClient.Model;

namespace Boxty.ServerBase.Commands
{
    public interface IManageRoleCommand
    {
        Task<bool> CreateRole(string roleName, string? description, ClaimsPrincipal user);
        Task<bool> DeleteRole(string roleName, ClaimsPrincipal user);
    }

    public class ManageRoleCommand : IManageRoleCommand
    {
        private readonly IKeycloakService _keycloakService;
        private readonly IUserContextService _userContextService;

        public ManageRoleCommand(
            IKeycloakService keycloakService,
            IUserContextService userContextService)
        {
            _keycloakService = keycloakService;
            _userContextService = userContextService;
        }

        public async Task<bool> CreateRole(string roleName, string? description, ClaimsPrincipal user)
        {
            try
            {
                // Validate the user has permission to manage roles
                ValidateAuthorization(user);

                // Check if role already exists
                try
                {
                    var existingRole = await _keycloakService.GetRoleByNameAsync(roleName);
                    if (existingRole != null)
                    {
                        throw new InvalidOperationException($"Role '{roleName}' already exists.");
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("404") || ex.Message.Contains("not found"))
                {
                    // Role doesn't exist, which is what we want
                }

                // Create the role
                var newRole = new RoleRepresentation
                {
                    Name = roleName.ToLowerInvariant(),
                    Description = description
                };

                await _keycloakService.CreateRoleAsync(newRole);

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create role: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteRole(string roleName, ClaimsPrincipal user)
        {
            try
            {
                // Validate the user has permission to manage roles
                ValidateAuthorization(user);

                // Check if role exists
                var existingRole = await _keycloakService.GetRoleByNameAsync(roleName);
                if (existingRole == null)
                {
                    throw new InvalidOperationException($"Role '{roleName}' not found.");
                }

                // Delete the role
                await _keycloakService.DeleteRoleAsync(roleName);

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to delete role: {ex.Message}", ex);
            }
        }

        private void ValidateAuthorization(ClaimsPrincipal user)
        {
            // Check if user has permission to manage roles
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                throw new UnauthorizedAccessException("User must be authenticated to manage roles.");
            }

            // Additional authorization checks can be added here
            // For example, check if user has admin role
        }
    }
}
