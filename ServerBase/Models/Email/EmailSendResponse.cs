namespace Boxty.ServerBase.Models.Email
{
    public class EmailSendResponse
    {
        public string OperationId { get; set; } = string.Empty;
        public EmailSendStatus Status { get; set; } = EmailSendStatus.NotStarted;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
