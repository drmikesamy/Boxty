namespace Boxty.ServerBase.Models.Email
{
    /// <summary>
    /// Represents the status of an email send operation.
    /// </summary>
    public enum EmailSendStatus
    {
        /// <summary>
        /// The email send operation has not started yet.
        /// </summary>
        NotStarted = 0,

        /// <summary>
        /// The email is currently being sent.
        /// </summary>
        Running = 1,

        /// <summary>
        /// The email was sent successfully.
        /// </summary>
        Succeeded = 2,

        /// <summary>
        /// The email send operation failed.
        /// </summary>
        Failed = 3,

        /// <summary>
        /// The email send operation was canceled.
        /// </summary>
        Canceled = 4
    }
}
