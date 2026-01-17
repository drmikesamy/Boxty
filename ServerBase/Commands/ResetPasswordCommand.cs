using System.Security.Claims;
using Boxty.ServerBase.Config;
using Boxty.ServerBase.Database;
using Boxty.ServerBase.Entities;
using Boxty.ServerBase.Interfaces;
using Boxty.ServerBase.Models.Email;
using Boxty.ServerBase.Services;
using Boxty.SharedBase.DTOs;
using Boxty.SharedBase.Helpers;
using Boxty.SharedBase.Interfaces;
using FluentValidation;
using FS.Keycloak.RestApiClient.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Boxty.ServerBase.Commands
{
    public interface IResetPasswordCommand<T, TDto, TContext>
    {
        Task<TDto> Handle(Guid id, ClaimsPrincipal user);
    }

    public class ResetPasswordCommand<T, TDto, TContext> : IResetPasswordCommand<T, TDto, TContext>, ICommand
        where T : class, IEntity, ISubjectEntity
        where TDto : IDto, IAuditDto, ISubject
        where TContext : IDbContext<TContext>
    {
        private IDbContext<TContext> _dbContext { get; }
        private readonly IKeycloakService _keycloakService;
        private readonly IUserContextService _userContextService;
        private readonly ISendEmailCommand _sendEmailCommand;
        private readonly AppOptions _options;

        public ResetPasswordCommand(
            IDbContext<TContext> dbContext,
            IKeycloakService keycloakService,
            IUserContextService userContextService,
            ISendEmailCommand sendEmailCommand,
            IOptions<AppOptions> options
        )
        {
            _dbContext = dbContext;
            _keycloakService = keycloakService;
            _userContextService = userContextService;
            _sendEmailCommand = sendEmailCommand;
            _options = options.Value;
        }

        public async Task<TDto> Handle(Guid id, ClaimsPrincipal user)
        {
            try
            {
                // Get the entity from database first
                var entity = await _dbContext.Set<T>()
                    .FirstOrDefaultAsync(e => e.Id == id);

                if (entity == null)
                {
                    throw new InvalidOperationException($"Subject with ID '{id}' not found.");
                }

                // Validate authorization with the target entity
                ValidatePasswordResetAuthorization(user, entity);

                // Generate new temporary password
                var newTemporaryPassword = PasswordHelper.GenerateTemporaryPassword();

                // Update password in Keycloak
                var credentialRepresentation = new CredentialRepresentation
                {
                    UserLabel = "Password",
                    Type = "password",
                    Value = newTemporaryPassword,
                    Temporary = true
                };

                await _keycloakService.ResetUserPasswordAsync(id.ToString(), credentialRepresentation);

                // Get user details from Keycloak for email
                var keycloakUser = await _keycloakService.GetUserByIdAsync(id.ToString());

                if (keycloakUser == null)
                {
                    throw new InvalidOperationException($"User with ID '{id}' not found in Keycloak.");
                }

                // Create DTO for email sending
                var dto = new
                {
                    Id = id,
                    FirstName = entity.FirstName ?? "",
                    LastName = entity.LastName ?? "",
                    Email = entity.Email ?? "",
                    RoleName = entity.RoleName
                };

                // Send password reset email
                await SendPasswordResetEmailAsync(dto, newTemporaryPassword, user);

                // Return a basic DTO response (you may need to adjust this based on your actual TDto structure)
                var result = Activator.CreateInstance<TDto>();
                result.Id = id;
                if (result is ISubject subjectResult)
                {
                    subjectResult.FirstName = dto.FirstName;
                    subjectResult.LastName = dto.LastName;
                    subjectResult.Email = dto.Email;
                    subjectResult.RoleName = dto.RoleName;
                }

                return result;
            }
            catch (ValidationException)
            {
                // Re-throw validation exceptions as-is
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                // Re-throw authorization exceptions as-is
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
                throw new InvalidOperationException($"Failed to reset password: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Sends a password reset email to the subject with their new temporary credentials.
        /// This reuses the same email template as the welcome email from CreateSubjectCommand.
        /// </summary>
        /// <param name="dto">The subject data containing email and temporary password</param>
        /// <param name="user">The current user's claims principal</param>
        private async Task SendPasswordResetEmailAsync(dynamic dto, string newTemporaryPassword, ClaimsPrincipal user)
        {
            try
            {
                // Check if email sending is enabled
                if (!_options.Email.EnableEmailSending)
                {
                    return; // Email sending is disabled, skip silently
                }

                // Get the sender email from configuration
                var senderEmail = _options.Email.SenderAddress;
                var senderName = _options.Email.SenderName;

                // Create email content - reusing the same template as CreateSubjectCommand
                var subject = "Composed Health Portal - Password Reset Notification";
                var htmlContent = $@"
                    <html>
                    <body>
                        <p>Dear {dto.FirstName} {dto.LastName},</p>
                        <p>Your password has been reset. Please use the following credentials to log in:</p>
                        <p><strong>Email:</strong> {dto.Email}</p>
                        <p><strong>Temporary Password:</strong> {newTemporaryPassword}</p>
                        <p><strong>Role:</strong> {dto.RoleName ?? "Subject"}</p>
                        <div style=""text-align: center; margin: 30px 0;"">
                            <a href=""https://composedhealth.com"" style=""
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
                            "">Go to Composed Health</a>
                        </div>
                        <p>Please note you will be prompted to change your password when you log in and two factor authentication is required. If you have any questions, please contact us at <a href=""mailto:admin@composedhealth.co.uk"">info@composedhealth.com</a>.</p>
                        <br/>
                        <p>Kind Regards,<br/>Composed Health</p>
                    </body>
                    </html>";

                var plainTextContent = $@"
Dear {dto.FirstName} {dto.LastName},

Your password has been reset. Please use the following credentials to log in:

Email: {dto.Email}
Temporary Password: {newTemporaryPassword}
Role: {dto.RoleName ?? "Subject"}

Please note you will be prompted to change your password when you log in and two factor authentication is required. If you have any questions, please contact us at admin@composedhealth.co.uk or alternatively call 01147004362.

Kind Regards,
Composed Health

Telephone - 01147004362
Email - admin@composedhealth.co.uk

This is an automated email notification from the Composed Health Portal. Please do not reply to this email.";

                // Create the email request
                var emailRequest = new SendEmailRequest
                {
                    SenderAddress = senderEmail,
                    RecipientAddress = dto.Email,
                    Subject = subject,
                    HtmlContent = htmlContent,
                    PlainTextContent = plainTextContent,
                    IsHighPriority = false
                };

                // Send the email
                await _sendEmailCommand.Handle(emailRequest, user);
            }
            catch (Exception ex)
            {
                // Log the error but don't fail the password reset
                // This allows the password reset to succeed even if email sending fails
                throw new InvalidOperationException($"Password reset successfully, but failed to send notification email: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Validates that the current user has the authority to reset passwords for the target user.
        /// Role hierarchy: administrator (4) > tenantadministrator (3) > tenantlimitedadministrator (2) > subject (1)
        /// Users can only reset passwords for users at their authority level or below.
        /// Tenant limited administrators can only reset subject passwords.
        /// </summary>
        /// <param name="user">The current user's claims principal</param>
        /// <param name="targetEntity">The target user entity whose password is being reset</param>
        /// <exception cref="UnauthorizedAccessException">Thrown when the user lacks permission to reset passwords</exception>
        private void ValidatePasswordResetAuthorization(ClaimsPrincipal user, T targetEntity)
        {
            if (user?.Identity?.IsAuthenticated != true)
            {
                throw new UnauthorizedAccessException("User must be authenticated to reset passwords.");
            }

            // Get current user's roles
            var userRoles = _userContextService.GetRoles(user);
            if (userRoles == null || !userRoles.Any())
            {
                throw new UnauthorizedAccessException("User has no assigned roles.");
            }

            // Define role hierarchy
            var roleHierarchy = new Dictionary<string, int>
            {
                ["subject"] = 1,
                ["tenantlimitedadministrator"] = 2,
                ["tenantadministrator"] = 3,
                ["administrator"] = 4
            };

            // Get user's maximum authority level
            var userMaxAuthority = userRoles
                .Where(role => roleHierarchy.ContainsKey(role.ToLowerInvariant()))
                .Select(role => roleHierarchy[role.ToLowerInvariant()])
                .DefaultIfEmpty(0)
                .Max();

            // Check if user has permission to reset passwords at all
            var canResetPasswords = userRoles.Any(role =>
                string.Equals(role, "administrator", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "tenantadministrator", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "tenantlimitedadministrator", StringComparison.OrdinalIgnoreCase));

            if (!canResetPasswords)
            {
                throw new UnauthorizedAccessException("User does not have permission to reset passwords. Requires 'administrator', 'tenantadministrator', or 'tenantlimitedadministrator' role.");
            }

            // Get target user's role authority level
            var targetRole = targetEntity.RoleName?.ToLowerInvariant() ?? "subject";
            var targetAuthority = roleHierarchy.ContainsKey(targetRole) ? roleHierarchy[targetRole] : 1;

            // Check role hierarchy - users can only reset passwords for users at their level or below
            if (targetAuthority > userMaxAuthority)
            {
                var userRoleNames = string.Join(", ", userRoles.Where(role => roleHierarchy.ContainsKey(role.ToLowerInvariant())));
                throw new UnauthorizedAccessException($"User with role(s) '{userRoleNames}' does not have permission to reset passwords for users with role '{targetEntity.RoleName}'. Users can only reset passwords for roles at their authority level or below.");
            }

            // Special restriction for tenant limited administrators
            var isTenantLimitedAdmin = userRoles.Any(role => string.Equals(role, "tenantlimitedadministrator", StringComparison.OrdinalIgnoreCase));
            var isFullAdmin = userRoles.Any(role => string.Equals(role, "administrator", StringComparison.OrdinalIgnoreCase));
            
            if (isTenantLimitedAdmin && !isFullAdmin)
            {
                // Tenant limited administrators can only reset subject passwords
                if (!string.Equals(targetRole, "subject", StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException($"Tenant limited administrators can only reset passwords for subjects, not for users with role '{targetEntity.RoleName}'.");
                }
            }
        }
    }
}
