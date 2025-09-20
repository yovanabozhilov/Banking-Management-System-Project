using BankingManagmentApp.Models;
using BankingManagmentApp.Services;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System.Threading.Tasks;

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
        message.From.Add(new MailboxAddress("GlowPay", "noreply@bank.com"));
        message.To.Add(new MailboxAddress("Client", customerEmail));
        message.Subject = $"Your loan status was changed #{loanId}";

        var bodyBuilder = new BodyBuilder();
        bodyBuilder.TextBody = $"Dear Customer,\n\nYour loan status with ID {loanId} was changed to: {newStatus}.";

        // Прикачи файла, ако е подаден
        if (attachmentBytes != null && attachmentBytes.Length > 0)
        {
            bodyBuilder.Attachments.Add($"Loan_Contract{loanId}.pdf", attachmentBytes, ContentType.Parse("application/pdf"));
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

    public async Task SendEmailAsync(string toEmail, string subject, string message)
    {
        var smtpServer = _config["SmtpSettings:Server"];
        var smtpPort = int.Parse(_config["SmtpSettings:Port"]);
        var smtpUser = _config["SmtpSettings:Username"];
        var smtpPass = _config["SmtpSettings:Password"];

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress("GlowPay", "noreply@bank.com"));
        email.To.Add(new MailboxAddress("Client", toEmail));
        email.Subject = subject;

        var bodyBuilder = new BodyBuilder();
        bodyBuilder.HtmlBody = message;

        email.Body = bodyBuilder.ToMessageBody();

        using (var client = new SmtpClient())
        {
            await client.ConnectAsync(smtpServer, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUser, smtpPass);
            await client.SendAsync(email);
            await client.DisconnectAsync(true);
        }
    }
    public async Task SendEmailWithAttachmentAsync(string toEmail, string subject, string message, byte[] attachmentBytes, string attachmentFileName)
    {
        var smtpServer = _config["SmtpSettings:Server"];
        var smtpPort = int.Parse(_config["SmtpSettings:Port"]);
        var smtpUser = _config["SmtpSettings:Username"];
        var smtpPass = _config["SmtpSettings:Password"];

        var email = new MimeMessage();
        email.From.Add(new MailboxAddress("GlowPay", "noreply@bank.com"));
        email.To.Add(new MailboxAddress("Client", toEmail));
        //email.Subject = $"Your loan status was changed #{loanId}";
        email.Subject = "Financial Reports";

        var bodyBuilder = new BodyBuilder();
        //bodyBuilder.TextBody = $"Dear Customer,\n\nYour loan status with ID {loanId} was changed to: {newStatus}.";
        bodyBuilder.TextBody = "Dear admin, we are sending you an attachment with the reports";
        // Прикачи файла, ако е подаден
        if (attachmentBytes != null && attachmentBytes.Length > 0)
        {
            bodyBuilder.Attachments.Add(attachmentFileName, attachmentBytes);
            //bodyBuilder.Attachments.Add($"Loan_Contract{loanId}.pdf", attachmentBytes, ContentType.Parse("application/pdf"));
        }

        email.Body = bodyBuilder.ToMessageBody();

        using (var client = new SmtpClient())
        {
            // Use the variables read from configuration for both connection and authentication
            await client.ConnectAsync(smtpServer, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUser, smtpPass);
            await client.SendAsync(email);
            await client.DisconnectAsync(true);
        }
    }
}