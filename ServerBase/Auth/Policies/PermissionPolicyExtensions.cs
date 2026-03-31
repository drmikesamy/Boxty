using Boxty.ServerBase.Auth.Constants;
using Boxty.ServerBase.Auth.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace Boxty.ServerBase.Auth.Policies
{
    /// <summary>
    /// Extension methods for registering permission-based authorization policies.
    /// </summary>
    public static class PermissionPolicyExtensions
    {
        /// <summary>
        /// Adds permission-based authorization policies for a specific entity type.
        /// </summary>
        public static void AddPermissionPoliciesForEntity<TEntity>(this AuthorizationOptions options)
        {
            var entityName = typeof(TEntity).Name;
            AddPermissionPoliciesForEntity(options, entityName);
        }

        /// <summary>
        /// Adds permission-based authorization policies for a specific entity type by name.
        /// </summary>
        public static void AddPermissionPoliciesForEntity(this AuthorizationOptions options, string entityName)
        {
            var operations = new[]
            {
                PermissionOperations.Create,
                PermissionOperations.View,
                PermissionOperations.Update,
                PermissionOperations.Delete,
                PermissionOperations.Finalise
            };

            foreach (var operation in operations)
            {
                var permission = PermissionHelper.GeneratePermission(operation, entityName);
                options.AddPolicy($"Permission:{permission}", policy =>
                {
                    policy.Requirements.Add(new PermissionRequirement(permission));
                });
            }
        }

        /// <summary>
        /// Adds permission-based authorization policies for all entity types that implement IEntity or ISimpleEntity.
        /// </summary>
        public static void AddPermissionPoliciesForEntities(this AuthorizationOptions options)
        {
            var entityTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(t => t.IsClass && !t.IsAbstract)
                .Where(t => t.GetInterfaces().Any(i => i.Name == "IEntity" || i.Name == "ISimpleEntity"))
                .ToList();

            foreach (var entityType in entityTypes)
            {
                AddPermissionPoliciesForEntity(options, entityType.Name);
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(System.Reflection.Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null)!;
            }
        }
    }
}
