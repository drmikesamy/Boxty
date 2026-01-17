using System.Security.Claims;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Boxty.ServerBase.Interfaces;
using Boxty.ServerBase.Services;
using Boxty.SharedBase.DTOs;
using Boxty.SharedBase.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Boxty.ServerBase.Commands
{
    public interface IDeleteSubjectCommand<T, TDto, TContext>
    {
        Task<bool> Handle(Guid id, ClaimsPrincipal user);
    }

    public class DeleteSubjectCommand<T, TDto, TContext> : IDeleteSubjectCommand<T, TDto, TContext>, ICommand
        where T : class, IEntity, ISubjectEntity
        where TDto : IDto, IAuditDto, ISubject
        where TContext : IDbContext<TContext>
    {
        private IDbContext<TContext> _dbContext { get; }
        private readonly IKeycloakService _keycloakService;
        private readonly IUserContextService _userContextService;
        private readonly IAuthorizationService _authorizationService;

        public DeleteSubjectCommand(
            IDbContext<TContext> dbContext,
            IKeycloakService keycloakService,
            IUserContextService userContextService,
            IAuthorizationService authorizationService
        )
        {
            _dbContext = dbContext;
            _keycloakService = keycloakService;
            _userContextService = userContextService;
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
            await _keycloakService.DeleteUserAsync(id.ToString());
            _dbContext.Set<T>().Remove(entity);
            await _dbContext.SaveChangesWithAuditAsync(user);
            return true;
        }
    }
}
