using System.Security.Claims;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Boxty.ServerBase.Interfaces;
using Boxty.ServerBase.Mappers;
using Boxty.SharedBase.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;

namespace Boxty.ServerBase.Commands
{
    public interface IUploadCommand<T, TDto, TContext>
    {
        Task<Guid> Handle(TDto dto, ClaimsPrincipal user);
    }

    public class UploadCommand<T, TDto, TContext> : IUploadCommand<T, TDto, TContext>, ICommand
        where T : class, IEntity
        where TDto : IDto
        where TContext : IDbContext<TContext>
    {
        private IDbContext<TContext> _dbContext { get; }
        private IMapper<T, TDto> _mapper { get; }
        private readonly IAuthorizationService _authorizationService;
        private readonly IValidator<TDto> _validator;

        public UploadCommand(IDbContext<TContext> dbContext, IMapper<T, TDto> mapper, IAuthorizationService authorizationService, IValidator<TDto> validator)
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
            var newEntity = _mapper.Map(dto);
            var authResult = await _authorizationService.AuthorizeAsync(user, newEntity, "resource-access");
            if (!authResult.Succeeded)
            {
                throw new UnauthorizedAccessException("Authorization failed for resource-access policy.");
            }
            _dbContext.Set<T>().Add(newEntity);
            await _dbContext.SaveChangesWithAuditAsync(user);
            return newEntity.Id;
        }
    }
}
