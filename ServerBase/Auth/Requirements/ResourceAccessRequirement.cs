using Microsoft.AspNetCore.Authorization;

namespace Boxty.ServerBase.Auth.Requirements
{
    public class ResourceAccessRequirement : IAuthorizationRequirement
    {
        public string Description => "User must have access to resource based on their role, tenant and relationship to the resource";
    }
}
