using System.Security.Claims;
using Boxty.ServerBase.Services;
using FS.Keycloak.RestApiClient.Model;

namespace Boxty.ServerBase.Queries
{
    public interface IGetUserRolesQuery
    {
        Task<ICollection<RoleRepresentation>> Handle(Guid userId, ClaimsPrincipal user);
    }

    public class GetUserRolesQuery : IGetUserRolesQuery
    {
        private readonly IKeycloakService _keycloakService;
        private readonly IUserContextService _userContextService;

        public GetUserRolesQuery(
            IKeycloakService keycloakService,
            IUserContextService userContextService)
        {
            _keycloakService = keycloakService;
            _userContextService = userContextService;
        }

        public async Task<ICollection<RoleRepresentation>> Handle(Guid userId, ClaimsPrincipal user)
        {
            try
            {
                // Validate authorization
                ValidateAuthorization(user, userId);

                // Get the user to ensure they exist
                var keycloakUser = await _keycloakService.GetUserByIdAsync(userId.ToString());
                if (keycloakUser == null)
                {
                    throw new InvalidOperationException($"User with ID '{userId}' not found in Keycloak.");
                }

                // Get and return the user's roles
                var roles = await _keycloakService.GetUserRolesAsync(userId.ToString());
                return roles;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to retrieve user roles: {ex.Message}", ex);
            }
        }

        private void ValidateAuthorization(ClaimsPrincipal user, Guid targetUserId)
        {
            // Check if user is authenticated
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                throw new UnauthorizedAccessException("User must be authenticated to view roles.");
            }

            // Add additional authorization logic here as needed
            // For example, check if user has admin role to view other users' roles
        }
    }
}
