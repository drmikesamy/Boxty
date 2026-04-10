using System.Security.Claims;
using Boxty.ServerBase.Auth.Requirements;
using Boxty.ServerBase.Entities;
using Boxty.SharedBase.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace Boxty.ServerBase.Auth.AuthorizationHandlers
{
    public class ResourceAccessAuthorizationHandler :
        AuthorizationHandler<ResourceAccessRequirement, IEntity>
    {
        private readonly IUserClaimsReader _userClaimsReader;

        public ResourceAccessAuthorizationHandler(IUserClaimsReader userClaimsReader)
        {
            _userClaimsReader = userClaimsReader;
        }

        protected override Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            ResourceAccessRequirement requirement,
            IEntity resource)
        {
            if (context.User.IsInRole("administrator"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }
            else if (context.User.IsInRole("tenantadministrator"))
            {
                var userTenantId = _userClaimsReader.GetOrganizationId(context.User);
                if (!string.IsNullOrEmpty(userTenantId)
                && (resource.TenantId == Guid.Parse(userTenantId) || resource.Id == Guid.Parse(userTenantId)))
                {
                    context.Succeed(requirement);
                }
            }
            var userSubjectId = context.User.FindFirstValue("sub");

            if (Guid.TryParse(userSubjectId, out var userGuid)
            && (resource.SubjectId == userGuid
            || resource.Id == userGuid))
            {
                context.Succeed(requirement);
            }

            return Task.CompletedTask;
        }
    }
}
