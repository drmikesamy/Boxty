using System.Security.Claims;

namespace Boxty.ServerBase.Mappers
{
    public interface IMapper<T, TDto>
    {
        TDto Map(T entity, ClaimsPrincipal? user = null);
        T Map(TDto dto, ClaimsPrincipal? user = null);
        IEnumerable<TDto> Map(IEnumerable<T> entities, ClaimsPrincipal? user = null);
        IEnumerable<T> Map(IEnumerable<TDto> dtos, ClaimsPrincipal? user = null);
        void Map(TDto dto, T entity, ClaimsPrincipal? user = null);
        void Map(T entity, TDto dto, ClaimsPrincipal? user = null);
    }
}
