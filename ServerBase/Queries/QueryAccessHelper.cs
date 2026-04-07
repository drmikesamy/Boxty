using System.Linq.Expressions;
using System.Security.Claims;
using Boxty.ServerBase.Entities;
using Microsoft.EntityFrameworkCore;

namespace Boxty.ServerBase.Queries
{
    internal static class QueryAccessHelper
    {
        public static IQueryable<T> ApplyScopeFilters<T>(
            IQueryable<T> query,
            Guid? tenantId = null,
            Guid? subjectId = null)
            where T : class, IEntity
        {
            if (tenantId != null)
            {
                query = query.Where(entity => entity.TenantId == tenantId.Value);
            }

            if (subjectId != null)
            {
                query = query.Where(entity => entity.SubjectId == subjectId.Value);
            }

            return query;
        }

        public static IQueryable<T> ApplyIncludes<T>(
            IQueryable<T> query,
            params Expression<Func<T, object>>[]? includes)
            where T : class
        {
            if (includes == null || includes.Length == 0)
            {
                return query;
            }

            foreach (var include in includes)
            {
                query = query.Include(include);
            }

            return query;
        }
    }
}