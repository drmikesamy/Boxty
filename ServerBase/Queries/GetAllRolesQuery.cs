using System.Security.Claims;
using Boxty.ServerBase.Services;
using FS.Keycloak.RestApiClient.Model;

namespace Boxty.ServerBase.Queries
{
    public interface IGetAllRolesQuery
    {
        Task<ICollection<RoleRepresentation>> Handle(ClaimsPrincipal user);
    }

    public class GetAllRolesQuery : IGetAllRolesQuery
    {
        private readonly IKeycloakService _keycloakService;
        private readonly IUserContextService _userContextService;

        public GetAllRolesQuery(
            IKeycloakService keycloakService,
            IUserContextService userContextService)
        {
            _keycloakService = keycloakService;
            _userContextService = userContextService;
        }

        public async Task<ICollection<RoleRepresentation>> Handle(ClaimsPrincipal user)
        {
            try
            {
                // Validate authorization
                ValidateAuthorization(user);

                // Get and return all roles
                var roles = await _keycloakService.GetAllRolesAsync();
                return roles;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to retrieve all roles: {ex.Message}", ex);
            }
        }

        private void ValidateAuthorization(ClaimsPrincipal user)
        {
            // Check if user is authenticated
            if (!user.Identity?.IsAuthenticated ?? true)
            {
                throw new UnauthorizedAccessException("User must be authenticated to view roles.");
            }

            // Add additional authorization logic here as needed
            // For example, check if user has admin role
        }
    }
}
