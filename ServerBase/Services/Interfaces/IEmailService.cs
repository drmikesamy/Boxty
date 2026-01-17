using Boxty.ServerBase.Models.Email;

namespace Boxty.ServerBase.Services.Interfaces
{
    public interface IEmailService
    {
        Task<EmailSendResponse> SendEmailAsync(
            string senderAddress,
            string recipientAddress,
            string subject,
            string htmlContent,
            string? plainTextContent = null,
            bool isHighPriority = false,
            Dictionary<string, string>? customHeaders = null,
            List<string>? ccRecipients = null,
            List<string>? bccRecipients = null,
            CancellationToken cancellationToken = default);

        Task<EmailSendStatus> GetEmailStatusAsync(string operationId, CancellationToken cancellationToken = default);
    }
}
