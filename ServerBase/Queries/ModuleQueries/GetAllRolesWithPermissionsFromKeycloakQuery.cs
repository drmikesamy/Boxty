using Boxty.ServerBase.Services;
using Boxty.SharedBase.DTOs.Auth;
using FS.Keycloak.RestApiClient.Model;

namespace Boxty.ServerBase.Queries.ModuleQueries
{
    public class GetAllRolesWithPermissionsFromKeycloakQuery : IGetAllRolesWithPermissionsQuery
    {
        private readonly IKeycloakService _keycloakService;

        public GetAllRolesWithPermissionsFromKeycloakQuery(IKeycloakService keycloakService)
        {
            _keycloakService = keycloakService;
        }

        public async Task<IEnumerable<RoleDto>> Handle()
        {
            var roles = await _keycloakService.GetAllRolesAsync();

            var roleDtos = new List<RoleDto>();

            foreach (var role in roles)
            {
                if (string.IsNullOrWhiteSpace(role.Name))
                {
                    continue;
                }

                RoleRepresentation fullRole;
                try
                {
                    fullRole = await _keycloakService.GetRoleByNameAsync(role.Name);
                }
                catch
                {
                    fullRole = role;
                }

                roleDtos.Add(new RoleDto
                {
                    Id = Guid.TryParse(fullRole.Id, out var roleId) ? roleId : Guid.NewGuid(),
                    Name = fullRole.Name ?? string.Empty,
                    Permissions = MapPermissions(fullRole)
                });
            }

            return roleDtos;
        }

        private static List<PermissionDto> MapPermissions(RoleRepresentation role)
        {
            var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (string.Equals(role.Name, "administrator", StringComparison.OrdinalIgnoreCase))
            {
                permissions.Add("*");
            }

            var attributes = role.Attributes;
            if (attributes != null && attributes.TryGetValue("permissions", out var rawPermissions) && rawPermissions != null)
            {
                foreach (var rawPermission in rawPermissions)
                {
                    if (string.IsNullOrWhiteSpace(rawPermission))
                    {
                        continue;
                    }

                    var splitPermissions = rawPermission
                        .Split([',', ';'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

                    foreach (var permission in splitPermissions)
                    {
                        permissions.Add(permission);
                    }
                }
            }

            return permissions.Select(name => new PermissionDto
            {
                Id = Guid.NewGuid(),
                Name = name
            }).ToList();
        }
    }
}