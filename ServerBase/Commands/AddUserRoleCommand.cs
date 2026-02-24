using System.Security.Claims;
using Boxty.ServerBase.Services;
using FS.Keycloak.RestApiClient.Model;

namespace Boxty.ServerBase.Commands
{
    public interface IAddUserRoleCommand
    {
        Task<bool> Handle(Guid userId, List<string> roleNames, ClaimsPrincipal user);
    }

    public class AddUserRoleCommand : IAddUserRoleCommand
    {
        private readonly IKeycloakService _keycloakService;
        private readonly IUserContextService _userContextService;

        public AddUserRoleCommand(
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

                // Get the role representations
                var roles = new List<RoleRepresentation>();
                foreach (var roleName in roleNames)
                {
                    var role = await _keycloakService.GetRoleByNameAsync(roleName);
                    if (role == null)
                    {
                        throw new InvalidOperationException($"Role '{roleName}' not found in Keycloak.");
                    }
                    roles.Add(role);
                }

                // Add the roles to the user
                await _keycloakService.PostUserRoleMappingAsync(userId.ToString(), roles);

                return true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to add roles to user: {ex.Message}", ex);
            }
        }

        private void ValidateAuthorization(ClaimsPrincipal user)
        {
            // Check if user has permission to manage roles
            // You can customize this based on your authorization requirements
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                throw new UnauthorizedAccessException("User must be authenticated to manage roles.");
            }

            // Additional authorization checks can be added here
            // For example, check if user has a specific role or permission
        }
    }
}
