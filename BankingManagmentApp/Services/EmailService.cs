using BankingManagmentApp.Models;
using BankingManagmentApp.Services;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using MimeKit;
using System.Threading.Tasks;
using MailKit.Security;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    // ==== ДОБАВЕНО САМО ЗА ТЕСТОВЕ (без да променя поведението) ====
    protected virtual SmtpClient CreateClient() => new SmtpClient();

    protected virtual Task ConnectAsync(SmtpClient client, string host, int port, SecureSocketOptions options)
        => client.ConnectAsync(host, port, options);

    protected virtual Task AuthenticateAsync(SmtpClient client, string user, string pass)
        => client.AuthenticateAsync(user, pass);

    protected virtual Task SendAsync(SmtpClient client, MimeMessage message)
        => client.SendAsync(message);

    protected virtual Task DisconnectAsync(SmtpClient client, bool quit)
        => client.DisconnectAsync(quit);
    // ================================================================

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

        using (var client = CreateClient())
        {
            await ConnectAsync(client, smtpServer, smtpPort, SecureSocketOptions.StartTls);
            await AuthenticateAsync(client, smtpUser, smtpPass);
            await SendAsync(client, message);
            await DisconnectAsync(client, true);
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

        using (var client = CreateClient())
        {
            await ConnectAsync(client, smtpServer, smtpPort, SecureSocketOptions.StartTls);
            await AuthenticateAsync(client, smtpUser, smtpPass);
            await SendAsync(client, email);
            await DisconnectAsync(client, true);
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
        // Оригинално поведение: игнорира параметрите и ползва фиксирани стойности
        email.Subject = "Financial Reports";

        var bodyBuilder = new BodyBuilder();
        bodyBuilder.TextBody = "Dear admin, we are sending you an attachment with the reports";

        // Прикачи файла, ако е подаден
        if (attachmentBytes != null && attachmentBytes.Length > 0)
        {
            bodyBuilder.Attachments.Add(attachmentFileName, attachmentBytes);
        }

        email.Body = bodyBuilder.ToMessageBody();

        using (var client = CreateClient())
        {
            // Use the variables read from configuration for both connection and authentication
            await ConnectAsync(client, smtpServer, smtpPort, SecureSocketOptions.StartTls);
            await AuthenticateAsync(client, smtpUser, smtpPass);
            await SendAsync(client, email);
            await DisconnectAsync(client, true);
        }
    }
}
