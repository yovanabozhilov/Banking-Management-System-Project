using BankingManagmentApp.Services;
using MailKit.Net.Smtp;
using MimeKit;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendLoanStatusUpdateAsync(string customerEmail, int loanId, string newStatus, byte[] attachmentBytes)
    {
        var smtpServer = _config["SmtpSettings:Server"];
        var smtpPort = int.Parse(_config["SmtpSettings:Port"]);
        var smtpUser = _config["SmtpSettings:Username"];
        var smtpPass = _config["SmtpSettings:Password"];

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("Банка", "noreply@bank.com"));
        message.To.Add(new MailboxAddress("Клиент", customerEmail));
        message.Subject = $"Промяна на статус на заем #{loanId}";

        var bodyBuilder = new BodyBuilder();
        bodyBuilder.TextBody = $"Здравейте,\n\nСтатусът на вашия заем с ID {loanId} беше обновен на: {newStatus}.";

        // Прикачи файла, ако е подаден
        if (attachmentBytes != null && attachmentBytes.Length > 0)
        {
            bodyBuilder.Attachments.Add($"Договор_Заем_{loanId}.pdf", attachmentBytes, ContentType.Parse("application/pdf"));
        }

        message.Body = bodyBuilder.ToMessageBody();

        using (var client = new SmtpClient())
        {
            // Use the variables read from configuration for both connection and authentication
            await client.ConnectAsync(smtpServer, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUser, smtpPass);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}