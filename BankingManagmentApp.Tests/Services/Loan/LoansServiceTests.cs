using System;
using System.Linq;
using System.Threading.Tasks;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Moq;
using BankingManagmentApp.Services.Approval;

namespace BankingManagmentApp.Tests.Services
{
    public class LoansServiceTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()) // isolated DB per test
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task ApplyAsync_ShouldCreateLoanWithPendingStatus()
        {
            using var ctx = CreateContext();
            ctx.Customers.Add(new Customers { Id = "cust1", UserName = "john" });
            await ctx.SaveChangesAsync();

            var service = new LoansService(ctx);
            var term = DateOnly.FromDateTime(DateTime.Today.AddMonths(6));

            var loan = await service.ApplyAsync("cust1", "Personal", 500m, term);

            Assert.NotNull(loan);
            Assert.Equal("cust1", loan.CustomerId);
            Assert.Equal("Personal", loan.Type);
            Assert.Equal(500m, loan.Amount);
            Assert.Equal("Pending", loan.Status);

            var dbLoan = ctx.Loans.Single();
            Assert.Equal(loan.Id, dbLoan.Id);
        }

        [Fact]
        public async Task GetCustomerLoansAsync_ShouldReturnLoansOnlyForCustomer()
        {
            using var ctx = CreateContext();

            ctx.Loans.AddRange(
                new Loans { CustomerId = "cust1", Type = "Personal", Amount = 100 },
                new Loans { CustomerId = "cust2", Type = "Business", Amount = 200 }
            );
            await ctx.SaveChangesAsync();

            var service = new LoansService(ctx);

            var loans = await service.GetCustomerLoansAsync("cust1");

            Assert.Single(loans);
            Assert.Equal("cust1", loans[0].CustomerId);
        }

        [Fact]
        public async Task SyncRepaymentStatusesAsync_ShouldUpdateStatusesCorrectly()
        {
            using var ctx = CreateContext();
            var loan = new Loans { CustomerId = "cust1", Type = "Personal", Amount = 1000 };
            ctx.Loans.Add(loan);
            await ctx.SaveChangesAsync();

            var today = DateOnly.FromDateTime(DateTime.Today);

            ctx.LoanRepayments.AddRange(
                // Overdue
                new LoanRepayments { LoanId = loan.Id, DueDate = today.AddDays(-5), AmountDue = 100, AmountPaid = 0, Status = "" },
                // Due today
                new LoanRepayments { LoanId = loan.Id, DueDate = today, AmountDue = 200, AmountPaid = 0, Status = "" },
                // Scheduled
                new LoanRepayments { LoanId = loan.Id, DueDate = today.AddDays(5), AmountDue = 300, AmountPaid = 0, Status = "" },
                // Paid
                new LoanRepayments { LoanId = loan.Id, DueDate = today, AmountDue = 400, AmountPaid = 400, Status = "" }
            );
            await ctx.SaveChangesAsync();

            var service = new LoansService(ctx);

            var changed = await service.SyncRepaymentStatusesAsync("cust1", today);

            Assert.Equal(4, changed);

            var reps = ctx.LoanRepayments.OrderBy(r => r.AmountDue).ToList();
            Assert.Equal("Overdue", reps[0].Status);
            Assert.Equal("Due", reps[1].Status);
            Assert.Equal("Scheduled", reps[2].Status);
            Assert.Equal("Paid", reps[3].Status);
            Assert.NotNull(reps[3].PaymentDate); // should set PaymentDate when Paid
        }

        [Fact]
        public async Task SyncRepaymentStatusesAsync_ShouldOnlyAffectSpecifiedCustomer()
        {
            using var ctx = CreateContext();

            var loan1 = new Loans { CustomerId = "cust1", Type = "Personal", Amount = 100 };
            var loan2 = new Loans { CustomerId = "cust2", Type = "Personal", Amount = 100 };
            ctx.Loans.AddRange(loan1, loan2);
            await ctx.SaveChangesAsync();

            var today = DateOnly.FromDateTime(DateTime.Today);

            ctx.LoanRepayments.AddRange(
                new LoanRepayments { LoanId = loan1.Id, DueDate = today.AddDays(-1), AmountDue = 50, AmountPaid = 0, Status = "" },
                new LoanRepayments { LoanId = loan2.Id, DueDate = today.AddDays(-1), AmountDue = 50, AmountPaid = 0, Status = "" }
            );
            await ctx.SaveChangesAsync();

            var service = new LoansService(ctx);

            var changed = await service.SyncRepaymentStatusesAsync("cust1", today);

            Assert.Equal(1, changed);

            var rep1 = ctx.LoanRepayments.First(r => r.LoanId == loan1.Id);
            var rep2 = ctx.LoanRepayments.First(r => r.LoanId == loan2.Id);

            Assert.Equal("Overdue", rep1.Status);
            Assert.Equal("", rep2.Status); // untouched
        }
    }
}
