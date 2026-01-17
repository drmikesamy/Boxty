using System.Security.Claims;
using Boxty.ServerBase.Auth.Constants;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Boxty.ServerBase.Helpers;
using Boxty.ServerBase.Interfaces;
using Boxty.ServerBase.Mappers;
using Boxty.SharedBase.DTOs;
using Boxty.SharedBase.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
namespace Boxty.ServerBase.Commands
{
    public interface ICreateCommand<T, TDto, TContext>
    {
        Task<Guid> Handle(TDto dto, ClaimsPrincipal user, bool skipAuth = false);
    }

    public class CreateCommand<T, TDto, TContext> : ICreateCommand<T, TDto, TContext>, ICommand
        where T : class, IEntity
        where TDto : IDto
        where TContext : IDbContext<TContext>
    {
        private readonly IDbContext<TContext> _dbContext;
        private readonly IMapper<T, TDto> _mapper;
        private readonly IAuthorizationService _authorizationService;
        private readonly IValidator<TDto> _validator;

        public CreateCommand(
            IDbContext<TContext> dbContext,
            IMapper<T, TDto> mapper,
            IAuthorizationService authorizationService,
            IValidator<TDto> validator
        )
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _authorizationService = authorizationService;
            _validator = validator;
        }

        public async Task<Guid> Handle(TDto dto, ClaimsPrincipal user, bool skipAuth = false)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            // 1. Validate DTO using FluentValidation
            var validationResult = await _validator.ValidateAsync(dto);

            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            if (typeof(IDraftable).IsAssignableFrom(typeof(T)) && dto is IDraftable draftable && !draftable.IsDraft)
            {
                var finalisePermission = PermissionHelper.GeneratePermission<T>(PermissionOperations.Finalise);

                var finaliseAuthResult = await _authorizationService.AuthorizeAsync(user, null, $"Permission:{finalisePermission}");

                if (!finaliseAuthResult.Succeeded && skipAuth == false)
                {
                    throw new UnauthorizedAccessException("Authorization failed for finalise permission.");
                }
            }

            var newEntity = _mapper.Map(dto);

            _dbContext.Set<T>().Add(newEntity);

            await _dbContext.SaveChangesWithAuditAsync(user);
            return newEntity.Id;
        }
    }
}
