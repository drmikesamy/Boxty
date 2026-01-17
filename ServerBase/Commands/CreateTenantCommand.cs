using System.Security.Claims;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Boxty.ServerBase.Helpers;
using Boxty.ServerBase.Interfaces;
using Boxty.ServerBase.Mappers;
using Boxty.ServerBase.Services;
using Boxty.SharedBase.DTOs;
using Boxty.SharedBase.Interfaces;
using FluentValidation;
using FS.Keycloak.RestApiClient.Model;
using Microsoft.AspNetCore.Authorization;

namespace Boxty.ServerBase.Commands
{
    public interface ICreateTenantCommand<T, TDto, TContext>
    {
        Task<Guid> Handle(TDto dto, ClaimsPrincipal user);
    }

    public class CreateTenantCommand<T, TDto, TContext> : ICreateTenantCommand<T, TDto, TContext>, ICommand
        where T : class, IEntity, ITenantEntity
        where TDto : IDto, ITenant
        where TContext : IDbContext<TContext>
    {
        private IDbContext<TContext> _dbContext { get; }
        private IMapper<T, TDto> _mapper { get; }
        private readonly IAuthorizationService _authorizationService;
        private readonly IKeycloakService _keycloakService;
        private readonly IValidator<TDto> _validator;

        public CreateTenantCommand(
            IDbContext<TContext> dbContext,
            IMapper<T, TDto> mapper,
            IAuthorizationService authorizationService,
            IKeycloakService keycloakService,
            IValidator<TDto> validator
        )
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _authorizationService = authorizationService;
            _keycloakService = keycloakService;
            _validator = validator;
        }

        public async Task<Guid> Handle(TDto dto, ClaimsPrincipal user)
        {
            try
            {
                // Perform all validations upfront and cache the results
                var tenantName = dto.Name.Replace(" ", "-").ToLowerInvariant();
                var validationContext = await ValidateAndCacheAsync(dto, tenantName);

                // All validations passed - proceed with creating the organization
                var orgBody = new OrganizationRepresentation
                {
                    Name = tenantName,
                    Domains = new List<OrganizationDomainRepresentation>
                    {
                        new OrganizationDomainRepresentation { Name = dto.Domain }
                    },
                    Enabled = true
                };

                await _keycloakService.PostOrganizationAsync(orgBody);

                // Get the newly created organization ID
                var newOrganizations = await _keycloakService.GetOrganizationsAsync(tenantName);
                var newId = newOrganizations?.FirstOrDefault()?.Id;

                // Save to database
                var newEntity = _mapper.Map(dto);
                newEntity.Id = newId != null ? Guid.Parse(newId) : Guid.NewGuid();

                _dbContext.Set<T>().Add(newEntity);
                await _dbContext.SaveChangesWithAuditAsync(user);

                dto.Id = newEntity.Id;
                return dto.Id;
            }
            catch (ValidationException)
            {
                // Re-throw validation exceptions as-is
                throw;
            }
            catch (InvalidOperationException)
            {
                // Re-throw business logic exceptions as-is
                throw;
            }
            catch (ArgumentNullException ex)
            {
                throw new InvalidOperationException($"Invalid input: {ex.ParamName} cannot be null", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create tenant: {ex.Message}", ex);
            }
        }

        private async Task<ValidationContext> ValidateAndCacheAsync(TDto dto, string tenantName)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));
            // 1. Validate DTO using FluentValidation
            var validationResult = await _validator.ValidateAsync(dto);

            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.Errors);
            }

            var tenantValidator = new TenantValidator<TDto>();
            var tenantValidationResult = await tenantValidator.ValidateAsync(dto);

            if (!tenantValidationResult.IsValid)
            {
                throw new ValidationException(tenantValidationResult.Errors);
            }

            var context = new ValidationContext();

            // 2. Check if organization name already exists in Keycloak
            var existingOrganizations = await _keycloakService.GetOrganizationsAsync(tenantName);

            if (existingOrganizations?.Any() == true)
            {
                throw new InvalidOperationException($"An organization with name '{dto.Name}' already exists in Keycloak.");
            }

            // 3. Validate domain format and uniqueness (if domain is provided)
            if (!string.IsNullOrEmpty(dto.Domain))
            {
                // Check if domain is already in use by another organization
                var allOrganizations = await _keycloakService.GetOrganizationsAsync(dto.Domain); // Get more orgs to check domains

                var domainExists = allOrganizations?.Any(org =>
                    org.Domains?.Any(domain =>
                        string.Equals(domain.Name, dto.Domain, StringComparison.OrdinalIgnoreCase)) == true) == true;

                if (domainExists)
                {
                    throw new InvalidOperationException($"Domain '{dto.Domain}' is already in use by another organization in Keycloak.");
                }
            }

            return context;
        }

        private class ValidationContext
        {
            // Currently no cached objects needed for tenant creation,
            // but this maintains consistency with the CreateSubjectCommand pattern
            // and allows for future enhancements
        }
    }
    public class TenantValidator<TDto> : AbstractValidator<TDto>
        where TDto : ITenant
    {
        public TenantValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name must not be empty.");

            RuleFor(x => x.Domain)
                .NotEmpty().WithMessage("Domain must not be empty.")
                .Matches(@"^(?:[a-zA-Z0-9-]+\.)+[a-zA-Z]{2,}$")
                .WithMessage("Domain must be a valid domain format.");
        }
    }
}
