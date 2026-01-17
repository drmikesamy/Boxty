using System.Security.Claims;
using Boxty.ServerBase.Auth.Constants;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Boxty.ServerBase.Interfaces;
using Boxty.ServerBase.Mappers;
using Boxty.SharedBase.DTOs;
using Boxty.SharedBase.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Boxty.ServerBase.Commands
{
    public interface IUpdateCommand<T, TDto, TContext>
    {
        Task<Guid> Handle(TDto dto, ClaimsPrincipal user);
    }

    public class UpdateCommand<T, TDto, TContext> : IUpdateCommand<T, TDto, TContext>, ICommand
        where T : class, IEntity
        where TDto : IDto
        where TContext : IDbContext<TContext>
    {
        private readonly IDbContext<TContext> _dbContext;
        private readonly IMapper<T, TDto> _mapper;
        private readonly IAuthorizationService _authorizationService;
        private readonly IValidator<TDto> _validator;
        public UpdateCommand(IDbContext<TContext> dbContext, IMapper<T, TDto> mapper, IAuthorizationService authorizationService, IValidator<TDto> validator)
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _authorizationService = authorizationService;
            _validator = validator;
        }

        public async Task<Guid> Handle(TDto dto, ClaimsPrincipal user)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            // 1. Validate DTO using FluentValidation
            var validationResult = await _validator.ValidateAsync(dto);

            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }
            var existingEntity = await _dbContext.Set<T>().FindAsync(dto.Id);
            if (existingEntity == null)
            {
                throw new KeyNotFoundException($"Entity with ID {dto.Id} not found.");
            }

            if (typeof(IDraftable).IsAssignableFrom(typeof(T)) && dto is IDraftable draftable && !draftable.IsDraft)
            {
                var finalisePermission = PermissionHelper.GeneratePermission<T>(PermissionOperations.Finalise);

                var finaliseAuthResult = await _authorizationService.AuthorizeAsync(user, null, $"Permission:{finalisePermission}");

                if (!finaliseAuthResult.Succeeded)
                {
                    throw new UnauthorizedAccessException("Authorization failed for finalise permission.");
                }
            }

            var authResult = await _authorizationService.AuthorizeAsync(user, existingEntity, "resource-access");
            if (!authResult.Succeeded)
            {
                throw new UnauthorizedAccessException("Authorization failed for resource-access policy.");
            }
            _mapper.Map(dto, existingEntity);

            await _dbContext.SaveChangesWithAuditAsync(user);
            return existingEntity.Id;
        }
    }
}
