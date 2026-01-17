using Microsoft.AspNetCore.Authorization;
namespace Boxty.ServerBase.Auth.Requirements
{
    public class OperationAuthorizationRequirement : IAuthorizationRequirement
    {
        public string Name { get; }
        public OperationAuthorizationRequirement(string name) => Name = name;
    }

    public static class Operations
    {
        public static OperationAuthorizationRequirement Create = new("Create");
        public static OperationAuthorizationRequirement Read = new("Read");
        public static OperationAuthorizationRequirement Update = new("Update");
        public static OperationAuthorizationRequirement Delete = new("Delete");
    }
}
