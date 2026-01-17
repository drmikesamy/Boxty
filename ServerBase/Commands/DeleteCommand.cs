using System.Security.Claims;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace Boxty.ServerBase.Commands
{
    public interface IDeleteCommand
    {
        Task<bool> Handle(Guid id, ClaimsPrincipal user);
    }
    public class DeleteCommand<T, TContext> : IDeleteCommand, ICommand
        where T : class
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
            var entity = await _dbContext.Set<T>().FindAsync(id);
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
