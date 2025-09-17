namespace BankingManagmentApp.Services
{
    public interface IEmailService
    {
        Task SendLoanStatusUpdateAsync(string customerEmail, int loanId, string newStatus, byte[] attachmentBytes);
    }
}
