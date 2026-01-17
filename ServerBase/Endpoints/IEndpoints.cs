using Microsoft.AspNetCore.Routing;

namespace Boxty.ServerBase.Endpoints
{
    public interface IEndpoints
    {
        IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints);
    }
}
