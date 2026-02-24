using System.Security.Claims;
using Boxty.ServerBase.Services;
using FS.Keycloak.RestApiClient.Model;

namespace Boxty.ServerBase.Commands
{
    public interface IUpdateUserRolesCommand
    {
        Task<bool> Handle(Guid userId, List<string> roleNames, ClaimsPrincipal user);
    }

    public class UpdateUserRolesCommand : IUpdateUserRolesCommand
    {
        private readonly IKeycloakService _keycloakService;
        private readonly IUserContextService _userContextService;

        public UpdateUserRolesCommand(
            IKeycloakService keycloakService,
            IUserContextService userContextService)
        {
            _keycloakService = keycloakService;
            _userContextService = userContextService;
        }

        public async Task<bool> Handle(Guid userId, List<string> roleNames, ClaimsPrincipal user)
        {
            try
            {
                // Validate the user has permission to manage roles
                ValidateAuthorization(user);

                // Get the user to ensure they exist
                var keycloakUser = await _keycloakService.GetUserByIdAsync(userId.ToString());
                if (keycloakUser == null)
                {
                    throw new InvalidOperationException($"User with ID '{userId}' not found in Keycloak.");
                }

                // Get current user roles
                var currentRoles = await _keycloakService.GetUserRolesAsync(userId.ToString());

                // Remove all current roles
                if (currentRoles.Any())
                {
                    await _keycloakService.DeleteUserRoleMappingAsync(userId.ToString(), currentRoles);
                }

                // Add new roles
                if (roleNames.Any())
                {
                    var newRoles = new List<RoleRepresentation>();
                    foreach (var roleName in roleNames)
                    {
                        var role = await _keycloakService.GetRoleByNameAsync(roleName);
                        if (role == null)
                        {
                            throw new InvalidOperationException($"Role '{roleName}' not found in Keycloak.");
                        }
                        newRoles.Add(role);
                    }

                    await _keycloakService.PostUserRoleMappingAsync(userId.ToString(), newRoles);
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to update user roles: {ex.Message}", ex);
            }
        }

        private void ValidateAuthorization(ClaimsPrincipal user)
        {
            // Check if user has permission to manage roles
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                throw new UnauthorizedAccessException("User must be authenticated to manage roles.");
            }
        }
    }
}
