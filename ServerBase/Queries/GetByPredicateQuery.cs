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
    public interface IGetByPredicateQuery<T, TDto, TContext>
    {
        Task<List<TDto>> Handle(Expression<Func<T, bool>> predicate, ClaimsPrincipal user, Guid? tenantId = null, Guid? subjectId = null, params Expression<Func<T, object>>[]? includes);
    }

    public class GetByPredicateQuery<T, TDto, TContext> : IGetByPredicateQuery<T, TDto, TContext>, IQuery
        where T : class, IEntity
        where TDto : IDto
        where TContext : IDbContext<TContext>
    {
        public IDbContext<TContext> _dbContext { get; }
        public IMapper<T, TDto> _mapper { get; }
        private readonly IAuthorizationService _authorizationService;

        public GetByPredicateQuery(IDbContext<TContext> dbContext, IMapper<T, TDto> mapper, IAuthorizationService authorizationService)
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _authorizationService = authorizationService;
        }

        public async Task<List<TDto>> Handle(Expression<Func<T, bool>> predicate, ClaimsPrincipal user, Guid? tenantId = null, Guid? subjectId = null, params Expression<Func<T, object>>[]? includes)
        {
            var query = QueryAccessHelper.ApplyScopeFilters(_dbContext.Set<T>().AsQueryable(), tenantId, subjectId)
                .Where(predicate)
                .AsNoTracking();

            query = QueryAccessHelper.ApplyIncludes(query, includes);

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
