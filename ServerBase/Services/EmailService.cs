using Boxty.ServerBase.Config;
using Boxty.ServerBase.Models.Email;
using Boxty.ServerBase.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Boxty.ServerBase.Services
{
    public class EmailService : IEmailService
    {
        private readonly EmailOptions _emailOptions;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<AppOptions> appOptions, ILogger<EmailService> logger)
        {
            _logger = logger;
            _emailOptions = appOptions.Value.Email;

            if (string.IsNullOrEmpty(_emailOptions.SmtpHost))
            {
                throw new InvalidOperationException("SMTP host is not configured");
            }
        }

        public async Task<EmailSendResponse> SendEmailAsync(
            string senderAddress,
            string recipientAddress,
            string subject,
            string htmlContent,
            string? plainTextContent = null,
            bool isHighPriority = false,
            Dictionary<string, string>? customHeaders = null,
            List<string>? ccRecipients = null,
            List<string>? bccRecipients = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Sending email from {SenderAddress} to {RecipientAddress} with subject: {Subject}",
                    senderAddress, recipientAddress, subject);

                // Create the email message
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(_emailOptions.SenderName ?? senderAddress, senderAddress));
                message.To.Add(new MailboxAddress(recipientAddress, recipientAddress));
                message.Subject = subject;

                // Add CC recipients if provided
                if (ccRecipients?.Any() == true)
                {
                    foreach (var ccRecipient in ccRecipients)
                    {
                        message.Cc.Add(new MailboxAddress(ccRecipient, ccRecipient));
                    }
                }

                // Add BCC recipients if provided
                if (bccRecipients?.Any() == true)
                {
                    foreach (var bccRecipient in bccRecipients)
                    {
                        message.Bcc.Add(new MailboxAddress(bccRecipient, bccRecipient));
                    }
                }

                // Set priority if high priority is requested
                if (isHighPriority)
                {
                    message.Priority = MessagePriority.Urgent;
                }

                // Add custom headers if provided
                if (customHeaders?.Any() == true)
                {
                    foreach (var header in customHeaders)
                    {
                        message.Headers.Add(header.Key, header.Value);
                    }
                }

                // Create the body
                var bodyBuilder = new BodyBuilder();
                if (!string.IsNullOrEmpty(htmlContent))
                {
                    bodyBuilder.HtmlBody = htmlContent;
                }
                if (!string.IsNullOrEmpty(plainTextContent))
                {
                    bodyBuilder.TextBody = plainTextContent;
                }
                message.Body = bodyBuilder.ToMessageBody();

                // Send the email using SMTP
                using var client = new SmtpClient();

                // Determine the appropriate SSL/TLS option based on port
                // Port 465: SSL/TLS (implicit)
                // Port 587: STARTTLS (explicit)
                // Port 25: None or STARTTLS
                var secureSocketOptions = _emailOptions.SmtpPort == 465
                    ? SecureSocketOptions.SslOnConnect
                    : SecureSocketOptions.StartTls;

                // Connect to SMTP server
                await client.ConnectAsync(_emailOptions.SmtpHost, _emailOptions.SmtpPort, secureSocketOptions, cancellationToken);

                // Authenticate
                await client.AuthenticateAsync(_emailOptions.SmtpUsername, _emailOptions.SmtpPassword, cancellationToken);

                // Send the message
                await client.SendAsync(message, cancellationToken);

                // Disconnect
                await client.DisconnectAsync(true, cancellationToken);

                var operationId = Guid.NewGuid().ToString();

                _logger.LogInformation("Email sent successfully via SMTP. Operation ID: {OperationId}",
                    operationId);

                return new EmailSendResponse
                {
                    OperationId = operationId,
                    Status = EmailSendStatus.Succeeded,
                    Success = true,
                    Message = "Email sent successfully via SMTP"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email from {SenderAddress} to {RecipientAddress}",
                    senderAddress, recipientAddress);
                throw new InvalidOperationException($"Failed to send email: {ex.Message}", ex);
            }
        }

        public Task<EmailSendStatus> GetEmailStatusAsync(string operationId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Checking email status for operation ID: {OperationId}", operationId);

                // For SMTP, emails are sent immediately, so we return Succeeded
                _logger.LogInformation("Email status check for operation {OperationId} - SMTP sends immediately", operationId);

                return Task.FromResult(EmailSendStatus.Succeeded);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get email status for operation ID: {OperationId}", operationId);
                throw new InvalidOperationException($"Failed to get email status: {ex.Message}", ex);
            }
        }
    }
}
