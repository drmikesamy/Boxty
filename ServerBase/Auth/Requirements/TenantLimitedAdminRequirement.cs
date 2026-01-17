using Microsoft.AspNetCore.Authorization;

namespace Boxty.ServerBase.Auth.Requirements
{
    public class TenantLimitedAdminRequirement : IAuthorizationRequirement
    {
        public TenantLimitedAdminRequirement() { }
    }
}
