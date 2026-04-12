using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Boxty.ServerBase.Auth.Constants;
using Boxty.ServerBase.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Boxty.ServerBase.Services
{
    internal static class PermissionAutoSeeder
    {
        private static readonly string[] PermissionOperationNames =
        [
            PermissionOperations.Create,
            PermissionOperations.View,
            PermissionOperations.Update,
            PermissionOperations.Delete,
            PermissionOperations.Finalise
        ];

        private static readonly DateTime SeedDate = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static async Task SeedAsync(IServiceProvider services, ILogger logger)
        {
            var dbContextType = FindType("Boxty.ServerApp.Modules.UserManagement.Infrastructure.Database.UserManagementDbContext");
            var permissionType = FindType("Boxty.ServerApp.Modules.UserManagement.Entities.Permission");
            var roleType = FindType("Boxty.ServerApp.Modules.UserManagement.Entities.Role");

            if (dbContextType == null || permissionType == null || roleType == null)
            {
                logger.LogDebug("Skipping permission auto-seeding because the UserManagement permission store was not found");
                return;
            }

            using var scope = services.CreateScope();
            if (scope.ServiceProvider.GetService(dbContextType) is not DbContext dbContext)
            {
                logger.LogDebug("Skipping permission auto-seeding because {DbContextType} is not registered", dbContextType.FullName);
                return;
            }

            var administratorRole = GetAdministratorRole(dbContext, roleType);
            if (administratorRole == null)
            {
                logger.LogWarning("Skipping permission auto-seeding because the Administrator role does not exist yet");
                return;
            }

            await dbContext.Entry(administratorRole).Collection("Permissions").LoadAsync();

            var generatedPermissionSeeds = BuildPermissionSeeds(permissionType, administratorRole);
            var permissionSet = GetDbSet(dbContext, "Permissions");
            var generatedPermissionIds = generatedPermissionSeeds
                .Select(permission => GetGuidProperty(permission, "Id"))
                .ToHashSet();
            var existingPermissionIds = GetEntities(permissionSet)
                .Select(entity => GetGuidProperty(entity, "Id"))
                .ToHashSet();

            var hasNewPermissions = false;

            foreach (var permissionSeed in generatedPermissionSeeds.Where(permission => !existingPermissionIds.Contains(GetGuidProperty(permission, "Id"))))
            {
                AddEntity(permissionSet, permissionSeed);
                hasNewPermissions = true;
            }

            if (hasNewPermissions)
            {
                await dbContext.SaveChangesAsync();
            }

            var persistedPermissions = GetEntities(permissionSet)
                .Where(entity => generatedPermissionIds.Contains(GetGuidProperty(entity, "Id")))
                .ToList();

            var permissionsCollection = GetEnumerableProperty(administratorRole, "Permissions");
            var assignedPermissionIds = permissionsCollection
                .Select(entity => GetGuidProperty(entity, "Id"))
                .ToHashSet();

            var hasNewAssignments = false;

            foreach (var permission in persistedPermissions.Where(permission => !assignedPermissionIds.Contains(GetGuidProperty(permission, "Id"))))
            {
                AddEntity(GetPropertyValue(administratorRole, "Permissions")!, permission);
                hasNewAssignments = true;
            }

            if (hasNewAssignments)
            {
                await dbContext.SaveChangesAsync();
            }

            logger.LogInformation("Permission auto-seeding completed with {PermissionCount} generated permissions", generatedPermissionSeeds.Count);
        }

        private static IReadOnlyList<object> BuildPermissionSeeds(Type permissionType, object administratorRole)
        {
            var entityAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .Where(assembly => assembly.GetName().Name?.StartsWith("Boxty.ServerApp.Modules.", StringComparison.Ordinal) == true)
                .Where(assembly => assembly.GetName().Name?.EndsWith(".Entities", StringComparison.Ordinal) == true)
                .ToList();

            var entityTypes = entityAssemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsClass && !type.IsAbstract)
                .Where(type => typeof(IEntity).IsAssignableFrom(type))
                .Where(type => type.Namespace?.Contains(".Entities", StringComparison.Ordinal) == true)
                .OrderBy(type => type.Name)
                .ToList();

            var permissions = new List<object>();

            foreach (var entityType in entityTypes)
            {
                foreach (var operation in PermissionOperationNames)
                {
                    var permissionName = PermissionHelper.GeneratePermission(operation, entityType.Name);
                    var permission = Activator.CreateInstance(permissionType)
                        ?? throw new InvalidOperationException($"Failed to create permission entity of type {permissionType.FullName}.");

                    SetProperty(permission, "Id", CreateDeterministicGuid($"permission:{permissionName}"));
                    SetProperty(permission, "Name", permissionName);
                    SetProperty(permission, "IsActive", true);
                    SetProperty(permission, "CreatedBy", GetPropertyValue(administratorRole, "CreatedBy") ?? "System");
                    SetProperty(permission, "LastModifiedBy", GetPropertyValue(administratorRole, "LastModifiedBy") ?? "System");
                    SetProperty(permission, "CreatedDate", SeedDate);
                    SetProperty(permission, "ModifiedDate", SeedDate);
                    SetProperty(permission, "TenantId", GetGuidProperty(administratorRole, "TenantId"));
                    SetProperty(permission, "SubjectId", GetGuidProperty(administratorRole, "SubjectId"));
                    SetProperty(permission, "CreatedById", GetGuidProperty(administratorRole, "CreatedById"));
                    SetProperty(permission, "ModifiedById", GetGuidProperty(administratorRole, "ModifiedById"));

                    permissions.Add(permission);
                }
            }

            return permissions;
        }

        private static object? GetAdministratorRole(DbContext dbContext, Type roleType)
        {
            var roles = GetEntities(GetDbSet(dbContext, "Roles"));
            return roles.SingleOrDefault(role => string.Equals(GetPropertyValue(role, "Name")?.ToString(), "Administrator", StringComparison.Ordinal));
        }

        private static IEnumerable<object> GetEntities(object dbSet)
        {
            return ((IEnumerable)dbSet).Cast<object>();
        }

        private static object GetDbSet(DbContext dbContext, string propertyName)
        {
            return dbContext.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(dbContext)
                ?? throw new InvalidOperationException($"DbContext {dbContext.GetType().FullName} does not expose a {propertyName} set.");
        }

        private static IEnumerable<object> GetEnumerableProperty(object instance, string propertyName)
        {
            if (GetPropertyValue(instance, propertyName) is not IEnumerable enumerable)
            {
                throw new InvalidOperationException($"Property {propertyName} on {instance.GetType().FullName} is not a collection.");
            }

            return enumerable.Cast<object>();
        }

        private static object? GetPropertyValue(object instance, string propertyName)
        {
            return instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?.GetValue(instance);
        }

        private static Guid GetGuidProperty(object instance, string propertyName)
        {
            var value = GetPropertyValue(instance, propertyName);
            return value is Guid guid
                ? guid
                : throw new InvalidOperationException($"Property {propertyName} on {instance.GetType().FullName} was not a Guid.");
        }

        private static void SetProperty(object instance, string propertyName, object value)
        {
            var property = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"Property {propertyName} not found on {instance.GetType().FullName}.");

            property.SetValue(instance, value);
        }

        private static Type? FindType(string fullTypeName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .Select(assembly => assembly.GetType(fullTypeName, throwOnError: false, ignoreCase: false))
                .FirstOrDefault(type => type != null);
        }

        private static void AddEntity(object setOrCollection, object entity)
        {
            var addMethod = setOrCollection.GetType().GetMethod("Add", [entity.GetType()]);
            if (addMethod == null)
            {
                throw new InvalidOperationException($"Collection {setOrCollection.GetType().FullName} does not expose a compatible Add method.");
            }

            addMethod.Invoke(setOrCollection, [entity]);
        }

        private static Guid CreateDeterministicGuid(string value)
        {
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(value));
            return new Guid(hash);
        }
    }
}