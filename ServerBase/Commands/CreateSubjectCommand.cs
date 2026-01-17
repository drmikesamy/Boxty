using System.Security.Claims;
using Boxty.ServerBase.Config;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Boxty.ServerBase.Interfaces;
using Boxty.ServerBase.Mappers;
using Boxty.ServerBase.Models.Email;
using Boxty.ServerBase.Services;
using Boxty.SharedBase.DTOs;
using Boxty.SharedBase.Helpers;
using Boxty.SharedBase.Interfaces;
using FluentValidation;
using FS.Keycloak.RestApiClient.Model;
using Microsoft.Extensions.Options;

namespace Boxty.ServerBase.Commands
{
    public interface ICreateSubjectCommand<T, TDto, TContext>
    {
        Task<Guid> Handle(TDto dto, ClaimsPrincipal user);
    }

    public class CreateSubjectCommand<T, TDto, TContext> : ICreateSubjectCommand<T, TDto, TContext>, ICommand
        where T : class, IEntity, ISubjectEntity
        where TDto : IDto, IAuditDto, ISubject
        where TContext : IDbContext<TContext>
    {
        private IDbContext<TContext> _dbContext { get; }
        private IMapper<T, TDto> _mapper { get; }
        private readonly IKeycloakService _keycloakService;
        private readonly IValidator<TDto> _validator;
        private readonly IUserContextService _userContextService;
        private readonly ISendEmailCommand _sendEmailCommand;
        private readonly AppOptions _options;

        public CreateSubjectCommand(
            IDbContext<TContext> dbContext,
            IMapper<T, TDto> mapper,
            IKeycloakService keycloakService,
            IValidator<TDto> validator,
            IUserContextService userContextService,
            ISendEmailCommand sendEmailCommand,
            IOptions<AppOptions> options
        )
        {
            _dbContext = dbContext;
            _mapper = mapper;
            _keycloakService = keycloakService;
            _validator = validator;
            _userContextService = userContextService;
            _sendEmailCommand = sendEmailCommand;
            _options = options.Value;
        }

        public async Task<Guid> Handle(TDto dto, ClaimsPrincipal user)
        {
            try
            {
                var validationContext = await ValidateAndCacheAsync(dto, user);

                var newTemporaryPassword = PasswordHelper.GenerateTemporaryPassword();

                var userRep = new UserRepresentation
                {
                    FirstName = dto.FirstName,
                    LastName = dto.LastName,
                    Username = dto.Email,
                    Email = dto.Email,
                    Credentials = new List<CredentialRepresentation>
                    {
                        new CredentialRepresentation
                        {
                            UserLabel = "Password",
                            Type = "password",
                            Value = newTemporaryPassword,
                            Temporary = true
                        }
                    },
                    Enabled = true
                };

                await _keycloakService.PostUsersAsync(userRep);

                var existingUsers = await _keycloakService.GetUsersAsync(dto.Email, 1);
                var newId = existingUsers?.FirstOrDefault()?.Id;

                if (!string.IsNullOrEmpty(newId))
                {
                    await _keycloakService.PostOrganizationMemberAsync(dto.TenantId.ToString(), newId);
                    await _keycloakService.PostUserRoleMappingAsync(newId, new List<RoleRepresentation> { validationContext.ValidatedRole });
                }

                var newEntity = _mapper.Map(dto);
                newEntity.Id = newId != null ? Guid.Parse(newId) : Guid.NewGuid();

                _dbContext.Set<T>().Add(newEntity);
                await _dbContext.SaveChangesWithAuditAsync(user);

                dto.Id = newEntity.Id;

                await SendWelcomeEmailAsync(dto, newTemporaryPassword, user);

                return dto.Id;
            }
            catch (ValidationException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (ArgumentNullException ex)
            {
                throw new InvalidOperationException($"Invalid input: {ex.ParamName} cannot be null", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to create subject: {ex.Message}", ex);
            }
        }

        private async Task SendWelcomeEmailAsync(TDto dto, string newTemporaryPassword, ClaimsPrincipal user)
        {
            try
            {
                if (!_options.Email.EnableEmailSending)
                {
                    return;
                }

                var senderEmail = _options.Email.SenderAddress;
                var senderName = _options.Email.SenderName;

                var subject = "Boxty Account Created";
                var htmlContent = $@"
                    <html>
                    <body>
                        <p>Welcome to the Boxty Portal {dto.FirstName} {dto.LastName},</p>
                        <p>Your portal account has been successfully created and can be accessed through the following link: <a href=""https://boxty.com"">Boxty - Home</a></p>
                        <p>Please use the following credentials to log in:</p>
                        <p><strong>Email:</strong> {dto.Email}</p>
                        <p><strong>Temporary Password:</strong> {newTemporaryPassword}</p>
                        <p><strong>Role:</strong> {dto.RoleName ?? "Subject"}</p>
                        <div style=""text-align: center; margin: 30px 0;"">
                            <a href=""https://boxty.org"" style=""
                                display: inline-block;
                                background-color: #007bff;
                                color: white;
                                text-decoration: none;
                                padding: 12px 24px;
                                border-radius: 5px;
                                font-weight: bold;
                                font-size: 16px;
                                border: none;
                                cursor: pointer;
                            "">Go to Boxty</a>
                        </div>
                        <p>Please note you will be prompted to change your password when you first log in. Your portal login requires two factor authentication, please ensure you have downloaded an authenticator app such as Microsoft Authenticator to allow you to login. If you have any questions, please contact us at <a href=""mailto:admin@boxty.co.uk"">info@boxty.com</a></p>
                        <br/>
                        <p>Kind Regards,<br/>Boxty</p>
                    </body>
                    </html>";

                var plainTextContent = $@"
Welcome to the Boxty Portal {dto.FirstName} {dto.LastName},

Your portal account has been successfully created and can be accessed through the following link: Boxty - Home (https://boxty.com)

Please use the following credentials to log in:

Email: {dto.Email}
Temporary Password: {newTemporaryPassword}
Role: {dto.RoleName ?? "Subject"}

Please note you will be prompted to change your password when you first log in. Your portal login requires two factor authentication, please ensure you have downloaded an authenticator app such as Microsoft Authenticator to allow you to login. If you have any questions, please contact us at admin@boxty.co.uk or alternatively call 01147004362

Kind Regards,
Boxty";

                var emailRequest = new SendEmailRequest
                {
                    SenderAddress = senderEmail,
                    RecipientAddress = dto.Email,
                    Subject = subject,
                    HtmlContent = htmlContent,
                    PlainTextContent = plainTextContent,
                    IsHighPriority = false
                };

                await _sendEmailCommand.Handle(emailRequest, user);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Subject created successfully, but failed to send welcome email: {ex.Message}", ex);
            }
        }

        private async Task<ValidationContext> ValidateAndCacheAsync(TDto dto, ClaimsPrincipal user)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var validationErrors = new List<FluentValidation.Results.ValidationFailure>();
            var context = new ValidationContext();

            var authorizationErrors = ValidateRoleAssignmentAuthorization(dto, user);
            if (authorizationErrors.Any())
            {
                validationErrors.AddRange(authorizationErrors);
            }

            var validationResult = await _validator.ValidateAsync(dto);
            if (!validationResult.IsValid)
            {
                validationErrors.AddRange(validationResult.Errors);
            }

            try
            {
                var existingUsers = await _keycloakService.GetUsersAsync(dto.Email, 1);
                if (existingUsers?.Any() == true)
                {
                    validationErrors.Add(new FluentValidation.Results.ValidationFailure("Email", $"A user with email '{dto.Email}' already exists in Keycloak."));
                }
            }
            catch (Exception ex)
            {
                validationErrors.Add(new FluentValidation.Results.ValidationFailure("Email", $"Failed to verify email availability: {ex.Message}"));
            }

            if (dto.TenantId != Guid.Empty)
            {
                try
                {
                    context.ValidatedOrganization = await _keycloakService.GetOrganizationByIdAsync(dto.TenantId.ToString());
                }
                catch (Exception ex) when (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
                {
                    validationErrors.Add(new FluentValidation.Results.ValidationFailure("TenantId", $"Organization with ID '{dto.TenantId}' does not exist in Keycloak."));
                }
                catch (Exception ex)
                {
                    validationErrors.Add(new FluentValidation.Results.ValidationFailure("TenantId", $"Failed to verify organization '{dto.TenantId}' in Keycloak: {ex.Message}"));
                }
            }

            if (string.IsNullOrEmpty(dto.RoleName))
            {
                dto.RoleName = "subject";
            }
            try
            {
                context.ValidatedRole = await _keycloakService.GetRoleByNameAsync(dto.RoleName);
            }
            catch (Exception ex) when (ex.Message.Contains("404") || ex.Message.Contains("Not Found"))
            {
                validationErrors.Add(new FluentValidation.Results.ValidationFailure("RoleName", $"Role '{dto.RoleName}' does not exist in Keycloak."));
            }
            catch (Exception ex)
            {
                validationErrors.Add(new FluentValidation.Results.ValidationFailure("RoleName", $"Failed to verify role '{dto.RoleName}' in Keycloak: {ex.Message}"));
            }

            if (validationErrors.Any())
            {
                throw new ValidationException(validationErrors);
            }

            return context;
        }

        private List<FluentValidation.Results.ValidationFailure> ValidateRoleAssignmentAuthorization(TDto dto, ClaimsPrincipal user)
        {
            var authorizationErrors = new List<FluentValidation.Results.ValidationFailure>();

            if (user?.Identity?.IsAuthenticated != true)
            {
                authorizationErrors.Add(new FluentValidation.Results.ValidationFailure("Authorization", "User must be authenticated to create subjects."));
                return authorizationErrors;
            }

            var userRoles = _userContextService.GetRoles(user);
            if (userRoles == null || !userRoles.Any())
            {
                authorizationErrors.Add(new FluentValidation.Results.ValidationFailure("Authorization", "User has no assigned roles."));
                return authorizationErrors;
            }

            var roleHierarchy = new Dictionary<string, int>
            {
                ["subject"] = 1,
                ["tenantlimitedadministrator"] = 2,
                ["tenantadministrator"] = 3,
                ["administrator"] = 4
            };

            var userMaxAuthority = userRoles
                .Where(role => roleHierarchy.ContainsKey(role.ToLowerInvariant()))
                .Select(role => roleHierarchy[role.ToLowerInvariant()])
                .DefaultIfEmpty(0)
                .Max();

            var canCreateSubjects = userRoles.Any(role =>
                string.Equals(role, "administrator", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "tenantadministrator", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "tenantlimitedadministrator", StringComparison.OrdinalIgnoreCase));

            if (!canCreateSubjects)
            {
                var userRoleNames = string.Join(", ", userRoles);
                authorizationErrors.Add(new FluentValidation.Results.ValidationFailure("Authorization", $" You are not authorised to create subjects."));
                return authorizationErrors;
            }

            var requestedRole = dto.RoleName?.ToLowerInvariant() ?? "subject";

            if (!roleHierarchy.TryGetValue(requestedRole, out var roleAuthority))
            {
                if (!userRoles.Any(role => string.Equals(role, "administrator", StringComparison.OrdinalIgnoreCase)))
                {
                    authorizationErrors.Add(new FluentValidation.Results.ValidationFailure("RoleName", $"Only administrators can assign unknown or custom roles like '{dto.RoleName}'."));
                }
                return authorizationErrors;
            }

            if (roleAuthority > userMaxAuthority)
            {
                var userRoleNames = string.Join(", ", userRoles.Where(role => roleHierarchy.ContainsKey(role.ToLowerInvariant())));
                var message = $"User with role(s) '{userRoleNames}' cannot assign role '{dto.RoleName}'. Users can only assign roles at their authority level or below.";
                authorizationErrors.Add(new FluentValidation.Results.ValidationFailure("RoleName", message));
            }

            var isTenantAdmin = userRoles.Any(role => string.Equals(role, "tenantadministrator", StringComparison.OrdinalIgnoreCase));
            var isTenantLimitedAdmin = userRoles.Any(role => string.Equals(role, "tenantlimitedadministrator", StringComparison.OrdinalIgnoreCase));
            var isFullAdmin = userRoles.Any(role => string.Equals(role, "administrator", StringComparison.OrdinalIgnoreCase));

            if ((isTenantAdmin || isTenantLimitedAdmin) && !isFullAdmin)
            {
                if (string.Equals(requestedRole, "administrator", StringComparison.OrdinalIgnoreCase))
                {
                    var userType = isTenantAdmin ? "Tenant administrators" : "Tenant limited administrators";
                    authorizationErrors.Add(new FluentValidation.Results.ValidationFailure("RoleName", $"{userType} cannot assign administrator roles."));
                }

                if (isTenantLimitedAdmin && string.Equals(requestedRole, "tenantadministrator", StringComparison.OrdinalIgnoreCase))
                {
                    authorizationErrors.Add(new FluentValidation.Results.ValidationFailure("RoleName", "Tenant limited administrators cannot assign tenant administrator roles."));
                }

                var userTenantId = _userContextService.GetOrganizationId(user);
                if (!string.IsNullOrEmpty(userTenantId) && Guid.TryParse(userTenantId, out var userTenant))
                {
                    if (dto.TenantId != userTenant)
                    {
                        var userType = isTenantAdmin ? "Tenant administrators" : "Tenant limited administrators";
                        authorizationErrors.Add(new FluentValidation.Results.ValidationFailure("TenantId", $"{userType} can only create subjects within their own tenant."));
                    }
                }
            }

            return authorizationErrors;
        }

        private class ValidationContext
        {
            public OrganizationRepresentation? ValidatedOrganization { get; set; }
            public RoleRepresentation ValidatedRole { get; set; } = null!;
        }
    }
}
