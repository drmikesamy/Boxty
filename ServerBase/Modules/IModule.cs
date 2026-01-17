using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boxty.ServerBase.Modules
{
    public interface IModule
    {
        IServiceCollection RegisterModuleServices(IServiceCollection services, IConfiguration configuration);
        WebApplication ConfigureModuleServices(WebApplication app, bool isDevelopment);
    }
}
