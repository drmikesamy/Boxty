using System.Security.Claims;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Boxty.ServerBase.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Boxty.ServerBase.Commands
{
    public interface IDeleteCommand
    {
        Task<bool> Handle(Guid id, ClaimsPrincipal user);
    }

    public interface IDeleteCommand<T, TContext> : IDeleteCommand
        where T : class, IEntity
        where TContext : IDbContext<TContext>
    {
    }

    public class DeleteCommand<T, TContext> : IDeleteCommand<T, TContext>, ICommand
        where T : class, IEntity
        where TContext : IDbContext<TContext>
    {
        private readonly IDbContext<TContext> _dbContext;
        private readonly IAuthorizationService _authorizationService;

        public DeleteCommand(IDbContext<TContext> dbContext, IAuthorizationService authorizationService)
        {
            _dbContext = dbContext;
            _authorizationService = authorizationService;
        }

        public async Task<bool> Handle(Guid id, ClaimsPrincipal user)
        {
            var entity = await _dbContext.Set<T>().SingleOrDefaultAsync(e => e.Id == id);

            if (entity == null)
            {
                return false;
            }

            var authResult = await _authorizationService.AuthorizeAsync(user, entity, "resource-access");
            if (!authResult.Succeeded)
            {
                throw new UnauthorizedAccessException("Authorization failed for resource-access policy.");
            }

            _dbContext.Set<T>().Remove(entity);
            await _dbContext.SaveChangesWithAuditAsync(user);
            return true;
        }
    }
}
