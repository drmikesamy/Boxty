using System.Security.Claims;
using System.Collections.Generic;

namespace Boxty.ServerBase.Mappers
{
    public interface IMapper<T, TDto>
    {
        TDto Map(T entity, ClaimsPrincipal? user = null);
        T Map(TDto dto, ClaimsPrincipal? user = null);
        void Map(TDto dto, T entity, ClaimsPrincipal? user = null);
        void Map(T entity, TDto dto, ClaimsPrincipal? user = null);
    }
}
