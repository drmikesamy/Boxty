using Boxty.ServerBase.Auth.Constants;
using Boxty.ServerBase.Queries.ModuleQueries;
using Boxty.ServerBase.Services;
using Boxty.SharedBase.DTOs.Auth;
using FS.Keycloak.RestApiClient.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Reflection;

namespace Boxty.ServerBase.Auth.Endpoints
{
    public static class AuthManagementEndpoints
    {
        public static IEndpointRouteBuilder MapAuthManagementEndpoints(this IEndpointRouteBuilder endpoints)
        {
            var authGroup = endpoints.MapGroup("/api/auth")
                .RequireAuthorization(policy => policy.RequireRole("administrator"));

            authGroup.MapGet("/roles/getall", async (IGetAllRolesWithPermissionsQuery getAllRolesWithPermissionsQuery) =>
            {
                var roles = await getAllRolesWithPermissionsQuery.Handle();
                return Results.Ok(roles);
            });

            authGroup.MapGet("/permissions/getall", () =>
            {
                var permissions = GetAllSystemPermissions();
                return Results.Ok(permissions);
            });

            authGroup.MapPost("/roles/create", async (
                RoleDto roleDto,
                IKeycloakService keycloakService,
                IRolePermissionCacheService cacheService) =>
            {
                if (string.IsNullOrWhiteSpace(roleDto.Name))
                {
                    return Results.BadRequest("Role name is required.");
                }

                var permissions = ExtractPermissionNames(roleDto.Permissions);

                var role = new RoleRepresentation
                {
                    Name = roleDto.Name.ToLowerInvariant(),
                    Attributes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                };

                if (permissions.Count > 0)
                {
                    role.Attributes["permissions"] = permissions;
                }

                await keycloakService.CreateRoleAsync(role);
                await cacheService.InitAsync();

                roleDto.Permissions = permissions.Select(name => new PermissionDto
                {
                    Id = Guid.NewGuid(),
                    Name = name
                }).ToList();

                return Results.Ok(roleDto);
            });

            authGroup.MapPut("/roles/update", async (
                RoleDto roleDto,
                IKeycloakService keycloakService,
                IRolePermissionCacheService cacheService) =>
            {
                if (string.IsNullOrWhiteSpace(roleDto.Name))
                {
                    return Results.BadRequest("Role name is required.");
                }

                var existingRole = await keycloakService.GetRoleByNameAsync(roleDto.Name);
                existingRole.Attributes ??= new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                existingRole.Attributes["permissions"] = ExtractPermissionNames(roleDto.Permissions);

                await keycloakService.UpdateRoleAsync(roleDto.Name, existingRole);
                await cacheService.InitAsync();

                return Results.Ok(roleDto);
            });

            return endpoints;
        }

        private static List<string> ExtractPermissionNames(IEnumerable<PermissionDto>? permissions)
        {
            if (permissions == null)
            {
                return [];
            }

            return permissions
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => x.Name.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<PermissionDto> GetAllSystemPermissions()
        {
            var operations = new[]
            {
                PermissionOperations.Create,
                PermissionOperations.View,
                PermissionOperations.Update,
                PermissionOperations.Delete,
                PermissionOperations.Finalise
            };

            var entityTypeNames = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.GetInterfaces().Any(i => i.Name == "IEntity" || i.Name == "ISimpleEntity"))
                .Select(t => t.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var permissionNames = entityTypeNames
                .SelectMany(entityName => operations.Select(op => PermissionHelper.GeneratePermission(op, entityName)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return permissionNames.Select(name => new PermissionDto
            {
                Id = Guid.NewGuid(),
                Name = name
            }).ToList();
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null)!;
            }
        }
    }
}
