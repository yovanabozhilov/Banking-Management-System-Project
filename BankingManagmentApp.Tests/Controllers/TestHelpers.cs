using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services;
using BankingManagmentApp.Services.Approval;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BankingManagmentApp.Tests.Controllers
{
    internal static class CtxHelper
    {
        public static ApplicationDbContext NewInMemoryContext(string? name = null)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
                .EnableSensitiveDataLogging()
                .Options;

            var ctx = new ApplicationDbContext(options);
            ctx.Database.EnsureCreated();
            return ctx;
        }

        public static ClaimsPrincipal PrincipalFor(string userId, bool isAdmin = false)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, userId)
            };
            if (isAdmin) claims.Add(new Claim(ClaimTypes.Role, "Admin"));

            var identity = new ClaimsIdentity(claims, "TestAuth");
            return new ClaimsPrincipal(identity);
        }

        public static void AttachUser(Controller controller, ClaimsPrincipal principal)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }
    }

    // Minimal in-memory user store for tests
    internal sealed class FakeUserStore : IUserStore<Customers>
    {
        private readonly Dictionary<string, Customers> _byId = new();

        public void Add(Customers user) => _byId[user.Id] = user;

        public Task<Customers?> FindByIdAsync(string userId, CancellationToken ct) =>
            Task.FromResult(_byId.TryGetValue(userId, out var u) ? u : null);

        // Unused members implemented minimally:
        public Task<string> GetUserIdAsync(Customers user, CancellationToken ct) => Task.FromResult(user.Id);
        public Task<string?> GetUserNameAsync(Customers user, CancellationToken ct) => Task.FromResult(user.UserName);
        public Task SetUserNameAsync(Customers user, string? userName, CancellationToken ct) { user.UserName = userName; return Task.CompletedTask; }
        public Task<string?> GetNormalizedUserNameAsync(Customers user, CancellationToken ct) => Task.FromResult(user.UserName);
        public Task SetNormalizedUserNameAsync(Customers user, string? normalizedName, CancellationToken ct) => Task.CompletedTask;
        public Task<IdentityResult> CreateAsync(Customers user, CancellationToken ct) { _byId[user.Id] = user; return Task.FromResult(IdentityResult.Success); }
        public Task<IdentityResult> UpdateAsync(Customers user, CancellationToken ct) { _byId[user.Id] = user; return Task.FromResult(IdentityResult.Success); }
        public Task<IdentityResult> DeleteAsync(Customers user, CancellationToken ct) { _byId.Remove(user.Id); return Task.FromResult(IdentityResult.Success); }
        public Task<Customers?> FindByNameAsync(string normalizedUserName, CancellationToken ct) =>
            Task.FromResult(_byId.Values.FirstOrDefault(u => u.UserName == normalizedUserName));
        public void Dispose() { }
    }

    internal static class UserManagerFactory
    {
        public static UserManager<Customers> Create(FakeUserStore store)
        {
            return new UserManager<Customers>(
                store,
                null, // IOptions<IdentityOptions>
                new PasswordHasher<Customers>(),
                Array.Empty<IUserValidator<Customers>>(),
                Array.Empty<IPasswordValidator<Customers>>(),
                null, null, null, null);
        }
    }

    // ===== Test doubles for Loans =====

    internal sealed class LoanWorkflowFake : ILoanWorkflow
    {
        public int LastProcessedLoanId { get; private set; }
        public Task ProcessNewApplicationAsync(Loans loan)
        {
            LastProcessedLoanId = loan.Id;
            return Task.CompletedTask;
        }
    }

    internal sealed class EmailServiceSpy : IEmailService
    {
        public enum EmailKind { StatusUpdate, Plain, WithAttachment }

        public sealed record Call(
            EmailKind Kind,
            string To,
            string? Subject,
            string? Body,
            int? LoanId,
            string? NewStatus,
            byte[]? AttachmentBytes,
            string? AttachmentName
        );

        public List<Call> Calls { get; } = new();

        public Task SendLoanStatusUpdateAsync(string customerEmail, int loanId, string newStatus, byte[] attachmentBytes)
        {
            Calls.Add(new Call(
                EmailKind.StatusUpdate,
                customerEmail,
                null,
                null,
                loanId,
                newStatus,
                attachmentBytes, // може да е null на runtime
                $"Loan_Contract{loanId}.pdf"
            ));
            return Task.CompletedTask;
        }

        public Task SendEmailAsync(string toEmail, string subject, string message)
        {
            Calls.Add(new Call(
                EmailKind.Plain,
                toEmail,
                subject,
                message,
                null,
                null,
                null,
                null
            ));
            return Task.CompletedTask;
        }

        public Task SendEmailWithAttachmentAsync(string toEmail, string subject, string message, byte[] attachmentBytes, string attachmentFileName)
        {
            Calls.Add(new Call(
                EmailKind.WithAttachment,
                toEmail,
                subject,
                message,
                null,
                null,
                attachmentBytes,
                attachmentFileName
            ));
            return Task.CompletedTask;
        }
    }
}
