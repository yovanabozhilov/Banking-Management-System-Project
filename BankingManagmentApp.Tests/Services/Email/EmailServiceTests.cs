using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using Xunit;

namespace BankingManagmentApp.Tests.Services.Email
{
    internal sealed class SpyEmailService : global::EmailService
    {
        public SpyEmailService(IConfiguration cfg) : base(cfg) { }

        protected override SmtpClient CreateClient() => new SmtpClient();

        public string? Host; public int Port; public SecureSocketOptions Options;
        public string? User; public string? Pass;
        public bool Connected; public bool Authed; public bool Sent; public bool Disconnected;
        public MimeMessage? LastMessage;

        protected override Task ConnectAsync(SmtpClient client, string host, int port, SecureSocketOptions options)
        {
            Host = host; Port = port; Options = options; Connected = true;
            return Task.CompletedTask;
        }

        protected override Task AuthenticateAsync(SmtpClient client, string user, string pass)
        {
            User = user; Pass = pass; Authed = true;
            return Task.CompletedTask;
        }

        protected override Task SendAsync(SmtpClient client, MimeMessage message)
        {
            LastMessage = message; Sent = true;
            return Task.CompletedTask;
        }

        protected override Task DisconnectAsync(SmtpClient client, bool quit)
        {
            Disconnected = true;
            return Task.CompletedTask;
        }
    }

    public class EmailServiceMinimalTests
    {
        private static IConfiguration Config() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SmtpSettings:Server"] = "smtp.test.local",
                    ["SmtpSettings:Port"] = "2525",
                    ["SmtpSettings:Username"] = "user",
                    ["SmtpSettings:Password"] = "pass"
                })
                .Build();

        [Fact]
        public async Task SendLoanStatusUpdate_AttachesPdf_WhenProvided()
        {
            var svc = new SpyEmailService(Config());
            var pdf = new byte[] { 1, 2, 3 };

            await svc.SendLoanStatusUpdateAsync("client@example.com", 42, "Approved", pdf);

            Assert.True(svc.Connected);
            Assert.Equal("smtp.test.local", svc.Host);
            Assert.Equal(2525, svc.Port);
            Assert.Equal(SecureSocketOptions.StartTls, svc.Options);

            Assert.True(svc.Authed);
            Assert.Equal("user", svc.User);
            Assert.Equal("pass", svc.Pass);

            Assert.True(svc.Sent);
            Assert.NotNull(svc.LastMessage);
            Assert.Equal("Your loan status was changed #42", svc.LastMessage!.Subject);
            Assert.Contains(svc.LastMessage.To.Mailboxes, m => m.Address == "client@example.com");

            var attachments = svc.LastMessage.Attachments.ToList();
            Assert.Single(attachments);
            var att = Assert.IsType<MimePart>(attachments[0]);
            Assert.Equal("application/pdf", att.ContentType.MimeType);
            Assert.Equal("Loan_Contract42.pdf", att.FileName);

            Assert.True(svc.Disconnected);
        }

        [Fact]
        public async Task SendLoanStatusUpdate_NoAttachment_WhenNull()
        {
            var svc = new SpyEmailService(Config());

            await svc.SendLoanStatusUpdateAsync("client@example.com", 7, "Rejected", null!);

            Assert.NotNull(svc.LastMessage);
            Assert.Empty(svc.LastMessage!.Attachments);
        }

        [Fact]
        public async Task SendEmailAsync_UsesHtmlBody()
        {
            var svc = new SpyEmailService(Config());

            await svc.SendEmailAsync("x@y.z", "Hello", "<b>Hi</b>");

            Assert.Equal("Hello", svc.LastMessage!.Subject);
            Assert.Contains("<b>Hi</b>", svc.LastMessage!.HtmlBody);
        }

        [Fact]
        public async Task SendEmailWithAttachment_UsesOriginalFixedSubjectAndMessage()
        {
            var svc = new SpyEmailService(Config());

            var bytes = new byte[] { 9, 9, 9 };
            await svc.SendEmailWithAttachmentAsync("boss@bank.com", "Monthly Reports", "See attached", bytes, "reports.xlsx");

            Assert.Equal("Financial Reports", svc.LastMessage!.Subject);
            Assert.Contains("Dear admin, we are sending you an attachment with the reports", svc.LastMessage!.TextBody);

            var att = Assert.IsType<MimePart>(svc.LastMessage.Attachments.Single());
            Assert.Equal("reports.xlsx", att.FileName);
        }
    }
}
