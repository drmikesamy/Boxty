using System.Linq.Expressions;
using System.Security.Claims;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Boxty.ServerBase.Interfaces;
using Boxty.ServerBase.Mappers;
using Boxty.SharedBase.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Boxty.ServerBase.Queries
{
    public interface IGetAllQuery<T, TDto, TContext>
    {
        Task<IEnumerable<TDto>> Handle(ClaimsPrincipal user, Guid? tenantId = null, Guid? subjectId = null, params Expression<Func<T, object>>[]? includes);
    }

    public class GetAllQuery<T, TDto, TContext> : IGetAllQuery<T, TDto, TContext>, IQuery
        where T : class, IEntity
        where TDto : IDto
        where TContext : IDbContext<TContext>
    {
        public IDbContext<TContext> _dbContext { get; }
        public IMapper<T, TDto> _mapper { get; }
        private readonly IAuthorizationService _authorizationService;

        public GetAllQuery(IDbContext<TContext> dbContext, IMapper<T, TDto> mapper, IAuthorizationService authorizationService)
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _authorizationService = authorizationService;
        }
        public async Task<IEnumerable<TDto>> Handle(ClaimsPrincipal user, Guid? tenantId = null, Guid? subjectId = null, params Expression<Func<T, object>>[]? includes)
        {
            var query = _dbContext.Set<T>().AsQueryable();

            if (tenantId != null)
            {
                query = query.Where(e => e.TenantId == tenantId);
            }
            if (subjectId != null)
            {
                query = query.Where(e => e.SubjectId == subjectId);
            }

            query = query.AsNoTracking();

            if (includes != null && includes.Length > 0)
            {
                foreach (var include in includes)
                {
                    query = query.Include(include);
                }
            }
            var entities = await query.ToListAsync();
            var authorizedEntities = new List<TDto>();
            foreach (var entity in entities)
            {
                var authResult = await _authorizationService.AuthorizeAsync(user, entity, "resource-access");
                if (authResult.Succeeded)
                {
                    authorizedEntities.Add(_mapper.Map(entity, user));
                }
            }
            return authorizedEntities;
        }
    }
}
