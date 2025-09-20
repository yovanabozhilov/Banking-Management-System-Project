namespace BankingManagmentApp.Services
{
    public interface IEmailService
    {
        Task SendLoanStatusUpdateAsync(string customerEmail, int loanId, string newStatus, byte[] attachmentBytes);
        Task SendEmailAsync(string toEmail, string subject, string message);

        Task SendEmailWithAttachmentAsync(string toEmail, string subject, string message, byte[] attachmentBytes, string attachmentFileName);
    }
}
