namespace Boxty.ServerBase.Models.Email
{
    public class SendEmailRequest
    {
        public string SenderAddress { get; set; } = string.Empty;
        public string RecipientAddress { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string HtmlContent { get; set; } = string.Empty;
        public string? PlainTextContent { get; set; }
        public bool IsHighPriority { get; set; } = false;
        public Dictionary<string, string>? CustomHeaders { get; set; }
        public List<string>? CcRecipients { get; set; }
        public List<string>? BccRecipients { get; set; }
    }
}
