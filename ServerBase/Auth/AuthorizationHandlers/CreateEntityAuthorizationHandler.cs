using Boxty.ServerBase.Auth.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace Boxty.ServerBase.Auth.AuthorizationHandlers
{
    public class CreateEntityAuthorizationHandler : AuthorizationHandler<CreateEntityRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, CreateEntityRequirement requirement)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }
}