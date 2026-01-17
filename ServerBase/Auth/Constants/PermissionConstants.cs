namespace Boxty.ServerBase.Auth.Constants
{
    /// <summary>
    /// Constants for permission operations that map to CRUD operations
    /// </summary>
    public static class PermissionOperations
    {
        public const string Create = "Create";
        public const string View = "View";
        public const string Update = "Update";
        public const string Delete = "Delete";
        public const string Finalise = "Finalise";
    }

    /// <summary>
    /// Helper class to generate permission strings based on operation and entity type
    /// </summary>
    public static class PermissionHelper
    {
        /// <summary>
        /// Generates a permission string for a given operation and entity type
        /// </summary>
        /// <param name="operation">The operation (Create, View, Update, Delete)</param>
        /// <param name="entityType">The entity type (e.g., "Schedule", "Subject")</param>
        /// <returns>Permission string (e.g., "CreateSchedule", "ViewSubject")</returns>
        public static string GeneratePermission(string operation, string entityType)
        {
            if (string.IsNullOrWhiteSpace(operation))
                throw new ArgumentException("Operation cannot be null or empty", nameof(operation));

            if (string.IsNullOrWhiteSpace(entityType))
                throw new ArgumentException("Entity type cannot be null or empty", nameof(entityType));

            return $"{operation}{entityType}";
        }

        /// <summary>
        /// Generates a permission string for a given operation and generic type T
        /// </summary>
        /// <typeparam name="T">The entity type</typeparam>
        /// <param name="operation">The operation (Create, View, Update, Delete)</param>
        /// <returns>Permission string (e.g., "CreateSchedule", "ViewSubject")</returns>
        public static string GeneratePermission<T>(string operation)
        {
            var entityType = typeof(T).Name;
            return GeneratePermission(operation, entityType);
        }
    }
}
