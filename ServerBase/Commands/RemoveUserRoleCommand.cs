using System.Security.Claims;
using Boxty.ServerBase.Services;
using FS.Keycloak.RestApiClient.Model;

namespace Boxty.ServerBase.Commands
{
    public interface IRemoveUserRoleCommand
    {
        Task<bool> Handle(Guid userId, List<string> roleNames, ClaimsPrincipal user);
    }

    public class RemoveUserRoleCommand : IRemoveUserRoleCommand
    {
        private readonly IKeycloakService _keycloakService;
        private readonly IUserContextService _userContextService;

        public RemoveUserRoleCommand(
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

                // Get current user roles to verify the roles exist
                var currentRoles = await _keycloakService.GetUserRolesAsync(userId.ToString());

                // Get the role representations to remove
                var rolesToRemove = new List<RoleRepresentation>();
                foreach (var roleName in roleNames)
                {
                    var role = currentRoles.FirstOrDefault(r => 
                        r.Name?.Equals(roleName, StringComparison.OrdinalIgnoreCase) ?? false);
                    
                    if (role == null)
                    {
                        throw new InvalidOperationException($"User does not have role '{roleName}'.");
                    }
                    rolesToRemove.Add(role);
                }

                // Remove the roles from the user
                await _keycloakService.DeleteUserRoleMappingAsync(userId.ToString(), rolesToRemove);

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to remove roles from user: {ex.Message}", ex);
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
