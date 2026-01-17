using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Boxty.ServerBase.Auth.Providers
{
    public static class ResourceAccessPolicyExtensions
    {
        public static IServiceCollection BuildResourceAccessPolicy(this IServiceCollection services)
        {
            services.AddAuthorization(options =>
            {
                ResourceAccessPolicyProvider.BuildPolicy(options);
            });
            return services;
        }
    }
}
