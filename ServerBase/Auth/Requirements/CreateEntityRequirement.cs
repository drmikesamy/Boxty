using Microsoft.AspNetCore.Authorization;

namespace Boxty.ServerBase.Auth.Requirements
{
    public class CreateEntityRequirement : IAuthorizationRequirement
    {
        public string Description => "User must have permission to create an entity in the requested business context";
    }
}