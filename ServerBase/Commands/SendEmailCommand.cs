using System.Security.Claims;
using Boxty.ServerBase.Interfaces;
using Boxty.ServerBase.Models.Email;
using Boxty.ServerBase.Services.Interfaces;

using FluentValidation;
using Microsoft.Extensions.Logging;

namespace Boxty.ServerBase.Commands
{
    public interface ISendEmailCommand
    {
        Task<EmailSendResponse> Handle(SendEmailRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default);
        Task<EmailSendStatus> GetEmailStatusAsync(string operationId, CancellationToken cancellationToken = default);
    }

    public class SendEmailCommand : ISendEmailCommand, ICommand
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<SendEmailCommand> _logger;

        public SendEmailCommand(
            IEmailService emailService,
            ILogger<SendEmailCommand> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<EmailSendResponse> Handle(SendEmailRequest request, ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Processing email send request from user {UserId} to {RecipientAddress}",
                    user.Identity?.Name, request.RecipientAddress);

                // Send the email using the email service
                var result = await _emailService.SendEmailAsync(
                    request.SenderAddress,
                    request.RecipientAddress,
                    request.Subject,
                    request.HtmlContent,
                    request.PlainTextContent,
                    request.IsHighPriority,
                    request.CustomHeaders,
                    request.CcRecipients,
                    request.BccRecipients,
                    cancellationToken);

                _logger.LogInformation("Email sent successfully by user {UserId}. Operation ID: {OperationId}",
                    user.Identity?.Name, result.OperationId);

                return result;
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
                _logger.LogError(ex, "Invalid input provided for email sending");
                throw new InvalidOperationException($"Invalid input: {ex.ParamName} cannot be null", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred while sending email");
                throw new InvalidOperationException($"Failed to send email: {ex.Message}", ex);
            }
        }

        public async Task<EmailSendStatus> GetEmailStatusAsync(string operationId, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(operationId))
                {
                    throw new ArgumentNullException(nameof(operationId), "Operation ID cannot be null or empty");
                }

                _logger.LogInformation("Retrieving email status for operation ID: {OperationId}", operationId);

                var status = await _emailService.GetEmailStatusAsync(operationId, cancellationToken);

                _logger.LogInformation("Email status retrieved for operation {OperationId}: {Status}", operationId, status);

                return status;
            }
            catch (ArgumentNullException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve email status for operation ID: {OperationId}", operationId);
                throw new InvalidOperationException($"Failed to retrieve email status: {ex.Message}", ex);
            }
        }
    }
}
