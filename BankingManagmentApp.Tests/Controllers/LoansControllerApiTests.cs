using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BankingManagmentApp.Controllers;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services;
using BankingManagmentApp.Services.Approval;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BankingManagmentApp.Tests.Controllers
{
    // ---- Simple workflow fake
    internal sealed class LoansFakeWorkflow : ILoanWorkflow
    {
        public int LastProcessedLoanId { get; private set; }
        public Task ProcessNewApplicationAsync(Loans loan)
        {
            LastProcessedLoanId = loan.Id;
            return Task.CompletedTask;
        }
    }

    // ---- Email fake that matches the CURRENT IEmailService
    internal sealed class LoansFakeEmailService : IEmailService
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
                Kind: EmailKind.StatusUpdate,
                To: customerEmail,
                Subject: null,
                Body: null,
                LoanId: loanId,
                NewStatus: newStatus,
                AttachmentBytes: attachmentBytes, // controller може да подаде null
                AttachmentName: $"Loan_Contract{loanId}.pdf"
            ));
            return Task.CompletedTask;
        }

        public Task SendEmailAsync(string toEmail, string subject, string message)
        {
            Calls.Add(new Call(
                Kind: EmailKind.Plain,
                To: toEmail,
                Subject: subject,
                Body: message,
                LoanId: null,
                NewStatus: null,
                AttachmentBytes: null,
                AttachmentName: null
            ));
            return Task.CompletedTask;
        }

        public Task SendEmailWithAttachmentAsync(string toEmail, string subject, string message, byte[] attachmentBytes, string attachmentFileName)
        {
            Calls.Add(new Call(
                Kind: EmailKind.WithAttachment,
                To: toEmail,
                Subject: subject,
                Body: message,
                LoanId: null,
                NewStatus: null,
                AttachmentBytes: attachmentBytes,
                AttachmentName: attachmentFileName
            ));
            return Task.CompletedTask;
        }
    }

    public class LoansControllerApiTests
    {
        [Fact]
        public async Task Apply_NoUser_Returns_Challenge()
        {
            using var ctx = CtxHelper.NewInMemoryContext();

            var store = new FakeUserStore(); // no users added
            var userManager = UserManagerFactory.Create(store);
            var email = new LoansFakeEmailService();

            var sut = new LoansController(ctx, userManager, email, loanContractGenerator: null!);
            CtxHelper.AttachUser(sut, new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity()));

            var result = await sut.Apply(new Loans { Type = "Personal", Amount = 1000, Term = default }, new LoansFakeWorkflow());
            result.Should().BeOfType<ChallengeResult>();
        }

        [Fact]
        public async Task Apply_InvalidModel_Sets_Defaults_Saves_And_Calls_Workflow()
        {
            using var ctx = CtxHelper.NewInMemoryContext();

            var customer = new Customers { Id = "c1", Email = "c1@x.com" };
            ctx.Customers.Add(customer);
            await ctx.SaveChangesAsync();

            var store = new FakeUserStore(); store.Add(customer);
            var userManager = UserManagerFactory.Create(store);

            var email = new LoansFakeEmailService();
            var workflow = new LoansFakeWorkflow();

            var sut = new LoansController(ctx, userManager, email, loanContractGenerator: null!);
            CtxHelper.AttachUser(sut, CtxHelper.PrincipalFor("c1"));

            // Force INVALID model => controller fills defaults (per current code)
            sut.ModelState.AddModelError("x", "invalid");

            var loan = new Loans { Type = "Personal", Amount = 2500, Term = default };
            var res = await sut.Apply(loan, workflow) as RedirectToActionResult;

            res.Should().NotBeNull();
            res!.ControllerName.Should().Be("Profile");
            res.ActionName.Should().Be("Index");

            var saved = ctx.Loans.Single();
            saved.CustomerId.Should().Be("c1");
            saved.Status.Should().Be("Pending "); // note trailing space in controller
            saved.Term.Should().NotBe(default);
            workflow.LastProcessedLoanId.Should().Be(saved.Id);
        }

        [Fact]
        public async Task MyLoans_NonAdmin_GetsOnlyOwnLoans()
        {
            using var ctx = CtxHelper.NewInMemoryContext();

            ctx.Customers.AddRange(
                new Customers { Id = "u1", Email = "u1@x.com" },
                new Customers { Id = "u2", Email = "u2@x.com" }
            );
            ctx.Loans.AddRange(
                new Loans { CustomerId = "u1", Type = "Personal", Amount = 100, Date = DateTime.UtcNow, Status = "Pending" },
                new Loans { CustomerId = "u2", Type = "Personal", Amount = 200, Date = DateTime.UtcNow, Status = "Pending" }
            );
            await ctx.SaveChangesAsync();

            var store = new FakeUserStore(); store.Add(new Customers { Id = "u1", Email = "u1@x.com" });
            var userManager = UserManagerFactory.Create(store);
            var email = new LoansFakeEmailService();

            var sut = new LoansController(ctx, userManager, email, loanContractGenerator: null!);
            CtxHelper.AttachUser(sut, CtxHelper.PrincipalFor("u1", isAdmin: false));

            var result = await sut.MyLoans() as ViewResult;
            result.Should().NotBeNull();
            var model = Assert.IsAssignableFrom<List<Loans>>(result!.Model);
            model.Should().HaveCount(1);
            model.Single().CustomerId.Should().Be("u1");
        }

        [Fact]
        public async Task Details_OtherUsersLoan_NonAdmin_Forbid()
        {
            using var ctx = CtxHelper.NewInMemoryContext();

            var owner = new Customers { Id = "owner", Email = "o@x.com" };
            var stranger = new Customers { Id = "stranger", Email = "s@x.com" };
            ctx.Customers.AddRange(owner, stranger);
            ctx.Loans.Add(new Loans { CustomerId = "owner", Type = "Personal", Amount = 500, Date = DateTime.UtcNow, Status = "Pending" });
            await ctx.SaveChangesAsync();

            var store = new FakeUserStore(); store.Add(owner); store.Add(stranger);
            var userManager = UserManagerFactory.Create(store);

            var email = new LoansFakeEmailService();

            var sut = new LoansController(ctx, userManager, email, loanContractGenerator: null!);
            CtxHelper.AttachUser(sut, CtxHelper.PrincipalFor("stranger", isAdmin: false));

            var loanId = ctx.Loans.Select(l => l.Id).Single();
            var res = await sut.Details(loanId);
            res.Should().BeOfType<ForbidResult>();
        }

        [Fact]
        public async Task Edit_Post_InvalidModel_Admin_Declined_Sends_StatusEmail_No_Repayments_Redirects()
        {
            // Use Declined status to avoid PDF generation dependency.
            using var ctx = CtxHelper.NewInMemoryContext();

            var customer = new Customers { Id = "c1", Email = "c1@x.com", UserName = "c1" };
            ctx.Customers.Add(customer);
            var loan = new Loans
            {
                CustomerId = "c1",
                Type = "Personal",
                Amount = 2400m,
                Date = DateTime.UtcNow,
                Status = "Pending"
            };
            ctx.Loans.Add(loan);
            await ctx.SaveChangesAsync();

            // Controller uses _context.Update(postedLoan) — изчистваме тракването преди POST.
            ctx.ChangeTracker.Clear();

            var store = new FakeUserStore(); store.Add(customer);
            var userManager = UserManagerFactory.Create(store);
            var email = new LoansFakeEmailService();

            var sut = new LoansController(ctx, userManager, email, loanContractGenerator: null!);
            CtxHelper.AttachUser(sut, CtxHelper.PrincipalFor("c1", isAdmin: true));

            sut.ModelState.AddModelError("x", "invalid"); // enter update branch per current code

            var postDto = new Loans
            {
                Id = loan.Id,
                CustomerId = "will-be-overwritten",
                Type = loan.Type,
                Amount = loan.Amount,
                Status = "Declined"
            };

            var res = await sut.Edit(loan.Id, postDto) as RedirectToActionResult;
            res.Should().NotBeNull();
            res!.ActionName.Should().Be(nameof(LoansController.Index));

            // Expect ONE status update email, no attachment for Declined
            email.Calls.Should().HaveCount(1);
            var call = email.Calls.Single();
            call.Kind.Should().Be(LoansFakeEmailService.EmailKind.StatusUpdate);
            call.To.Should().Be("c1@x.com");
            call.LoanId.Should().Be(loan.Id);
            call.NewStatus.Should().Be("Declined");
            call.AttachmentBytes.Should().BeNull(); // controller passes null for 'Declined'

            // No repayments created for Declined
            ctx.LoanRepayments.Where(r => r.LoanId == loan.Id).Count().Should().Be(0);
        }

        [Fact]
        public async Task Create_Admin_ValidModel_Adds_And_Redirects()
        {
            using var ctx = CtxHelper.NewInMemoryContext();

            var admin = new Customers { Id = "admin", Email = "a@x.com", UserName = "admin" };
            ctx.Customers.Add(admin);
            await ctx.SaveChangesAsync();

            var store = new FakeUserStore(); store.Add(admin);
            var userManager = UserManagerFactory.Create(store);
            var email = new LoansFakeEmailService();

            var sut = new LoansController(ctx, userManager, email, loanContractGenerator: null!);
            CtxHelper.AttachUser(sut, CtxHelper.PrincipalFor("admin", isAdmin: true));

            var dto = new Loans
            {
                CustomerId = "admin",
                Type = "Personal",
                Amount = 1234m,
                Term = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
                Date = DateTime.UtcNow,
                Status = "Pending",
                ApprovedAmount = 0,
                // НЕ задаваме null за ApprovalDate, защото е non-nullable
                ApprovalDate = DateTime.UtcNow
            };

            var res = await sut.Create(dto) as RedirectToActionResult;
            res.Should().NotBeNull();
            res!.ActionName.Should().Be(nameof(LoansController.Index));

            ctx.Loans.Should().ContainSingle(l => l.CustomerId == "admin" && l.Amount == 1234m);
        }

        [Fact]
        public async Task DeleteConfirmed_Removes_Loan_Repayments_And_Assessments()
        {
            using var ctx = CtxHelper.NewInMemoryContext();

            var c = new Customers { Id = "u1", Email = "u1@x.com" };
            ctx.Customers.Add(c);
            var loan = new Loans
            {
                CustomerId = "u1",
                Type = "Personal",
                Amount = 1000m,
                Date = DateTime.UtcNow,
                Status = "Approved",
                ApprovedAmount = 1000m,
                ApprovalDate = DateTime.UtcNow,
                Term = DateOnly.FromDateTime(DateTime.Today.AddMonths(12))
            };
            ctx.Loans.Add(loan);
            await ctx.SaveChangesAsync();

            ctx.LoanRepayments.Add(new LoanRepayments
            {
                LoanId = loan.Id,
                DueDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(1)),
                AmountDue = 10,
                AmountPaid = 0,
                PaymentDate = DateOnly.FromDateTime(DateTime.Today),
                Status = "Pending"
            });
            ctx.CreditAssessments.Add(new CreditAssessments { LoanId = loan.Id });
            await ctx.SaveChangesAsync();

            var store = new FakeUserStore(); store.Add(c);
            var userManager = UserManagerFactory.Create(store);
            var email = new LoansFakeEmailService();

            var sut = new LoansController(ctx, userManager, email, loanContractGenerator: null!);
            CtxHelper.AttachUser(sut, CtxHelper.PrincipalFor("u1", isAdmin: true));

            var res = await sut.DeleteConfirmed(loan.Id) as RedirectToActionResult;
            res.Should().NotBeNull();
            res!.ActionName.Should().Be(nameof(LoansController.Index));

            ctx.Loans.Any(l => l.Id == loan.Id).Should().BeFalse();
            ctx.LoanRepayments.Any(r => r.LoanId == loan.Id).Should().BeFalse();
            ctx.CreditAssessments.Any(a => a.LoanId == loan.Id).Should().BeFalse();
        }
    }
}
