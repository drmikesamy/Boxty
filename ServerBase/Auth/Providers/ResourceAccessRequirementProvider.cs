using Microsoft.AspNetCore.Authorization;

namespace Boxty.ServerBase.Auth.Providers
{
    public static class ResourceAccessPolicyProvider
    {
        private static readonly List<IAuthorizationRequirement> _requirements = new();

        public static void AddRequirement(IAuthorizationRequirement requirement)
        {
            _requirements.Add(requirement);
        }

        public static void BuildPolicy(AuthorizationOptions options)
        {
            options.AddPolicy("resource-access", policy =>
            {
                foreach (var requirement in _requirements)
                {
                    policy.Requirements.Add(requirement);
                }
            });
        }
    }
}
