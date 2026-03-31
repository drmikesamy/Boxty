using Microsoft.AspNetCore.Authorization;

namespace Boxty.ServerBase.Auth.Policies
{
    public static class ResourceAccessPolicyRegistry
    {
        private static readonly List<IAuthorizationRequirement> Requirements = [];

        public static void AddRequirement(IAuthorizationRequirement requirement)
        {
            Requirements.Add(requirement);
        }

        public static void BuildPolicy(AuthorizationOptions options)
        {
            options.AddPolicy("resource-access", policy =>
            {
                foreach (var requirement in Requirements)
                {
                    policy.Requirements.Add(requirement);
                }
            });
        }
    }
}
