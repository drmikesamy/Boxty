using System.Linq.Expressions;
using System.Security.Claims;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Boxty.ServerBase.Interfaces;
using Boxty.ServerBase.Mappers;
using Boxty.SharedBase.DTOs;
using Boxty.SharedBase.Interfaces;
using Boxty.SharedBase.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Boxty.ServerBase.Queries
{
    public interface IGetPagedQuery<T, TDto, TContext>
    {
        Task<PagedResult<TDto>> Handle(int page, int pageSize, ClaimsPrincipal user, FetchFilter? filter = null, Guid? tenantId = null, Guid? subjectId = null, params Expression<Func<T, object>>[]? includes);
    }

    public class GetPagedQuery<T, TDto, TContext> : IGetPagedQuery<T, TDto, TContext>, IQuery
        where T : class, IEntity
        where TDto : IDto
        where TContext : IDbContext<TContext>
    {
        public IDbContext<TContext> _dbContext { get; }
        public IMapper<T, TDto> _mapper { get; }
        private readonly IAuthorizationService _authorizationService;

        public GetPagedQuery(IDbContext<TContext> dbContext, IMapper<T, TDto> mapper, IAuthorizationService authorizationService)
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _authorizationService = authorizationService;
        }
        public async Task<PagedResult<TDto>> Handle(int page, int pageSize, ClaimsPrincipal user, FetchFilter? filter = null, Guid? tenantId = null, Guid? subjectId = null, params Expression<Func<T, object>>[]? includes)
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

            // Apply basic FetchFilter conditions that can be done at the entity level
            if (filter != null)
            {
                if (filter.IsActive.HasValue)
                {
                    query = query.Where(e => e.IsActive == filter.IsActive.Value);
                }

                if (filter.StartTime.HasValue)
                {
                    query = query.Where(e => e.CreatedDate >= filter.StartTime.Value);
                }

                if (filter.EndTime.HasValue)
                {
                    query = query.Where(e => e.CreatedDate <= filter.EndTime.Value);
                }
            }

            // Get total count before pagination
            var totalCount = await query.CountAsync();

            query = query.AsNoTracking();
            if (includes != null && includes.Length > 0)
            {
                foreach (var include in includes)
                {
                    query = query.Include(include);
                }
            }
            var entities = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var authorizedEntities = new List<TDto>();
            foreach (var entity in entities)
            {
                var authResult = await _authorizationService.AuthorizeAsync(user, entity, "resource-access");
                if (authResult.Succeeded)
                {
                    authorizedEntities.Add(_mapper.Map(entity, user));
                }
            }

            // Apply SearchTerm filter on the DTOs after mapping (only if TDto implements IAutoCrud)
            if (filter != null && !string.IsNullOrWhiteSpace(filter.SearchTerm) && typeof(IAutoCrud).IsAssignableFrom(typeof(TDto)))
            {
                authorizedEntities = authorizedEntities
                    .Where(dto => (dto as IAutoCrud)?.DisplayName?.Contains(filter.SearchTerm, StringComparison.OrdinalIgnoreCase) == true)
                    .ToList();
            }

            return new PagedResult<TDto>
            {
                Items = authorizedEntities,
                TotalCount = totalCount
            };
        }
    }
}
