using BankingManagmentApp.Controllers;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services.Forecasting;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using Xunit;

namespace BankingManagmentApp.Tests.Controllers
{
    public class ForecastingControllerApiTests
    {
        private static ApplicationDbContext NewCtx()
        {
            var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .EnableSensitiveDataLogging()
                .Options;
            return new ApplicationDbContext(opts);
        }

        [Fact]
        public void Index_ShouldReturnViewResult_WithExpectedValues()
        {
            using var ctx = NewCtx();
            var now = DateTime.Now;

            var u1 = new Customers { Id = "u1", Email = "u1@x.com", UserName = "u1", IsActive = true,  CreateAt = now };
            var u2 = new Customers { Id = "u2", Email = "u2@x.com", UserName = "u2", IsActive = false, CreateAt = now.AddDays(-5) };
            ctx.Users.AddRange(u1, u2);

            var acc1 = new Accounts
            {
                Id = 1, CustomerId = u1.Id, Customer = u1, IBAN = "BG11XXXX1234567890",
                AccountType = "User", Currency = "BGN", Balance = 100m, Status = "Active", CreateAt = now
            };
            var acc2 = new Accounts
            {
                Id = 2, CustomerId = u2.Id, Customer = u2, IBAN = "BG22XXXX0987654321",
                AccountType = "User", Currency = "BGN", Balance = 50m, Status = "Inactive", CreateAt = now
            };
            ctx.Accounts.AddRange(acc1, acc2);

            ctx.Transactions.AddRange(
                new Transactions { Id = 1, AccountsId = acc1.Id, Accounts = acc1, Amount = 10m, TransactionType = "Credit", Date = DateOnly.FromDateTime(now),               Description = "t1", ReferenceNumber = 11111 },
                new Transactions { Id = 2, AccountsId = acc1.Id, Accounts = acc1, Amount = 20m, TransactionType = "Debit",  Date = DateOnly.FromDateTime(now.AddDays(-1)), Description = "t2", ReferenceNumber = 22222 },
                new Transactions { Id = 3, AccountsId = acc2.Id, Accounts = acc2, Amount = 5m,  TransactionType = "Credit", Date = DateOnly.FromDateTime(now.AddMonths(-1)),Description = "t3", ReferenceNumber = 33333 }
            );

            var loan = new Loans
            {
                Id = 10, CustomerId = u1.Id, Customer = u1,
                Type = "Personal", Amount = 1000m, ApprovedAmount = 1000m,
                Status = "Approved", ApprovalDate = now, Date = now,
                Term = DateOnly.FromDateTime(now.AddMonths(12))
            };
            ctx.Loans.Add(loan);

            ctx.LoanRepayments.AddRange(
                new LoanRepayments { Id = 100, LoanId = loan.Id, Loan = loan, DueDate = DateOnly.FromDateTime(now.AddDays(-5)),  AmountDue = 100m, AmountPaid = 0m,   Status = "Pending" },
                new LoanRepayments { Id = 101, LoanId = loan.Id, Loan = loan, DueDate = DateOnly.FromDateTime(now.AddDays(10)), AmountDue = 100m, AmountPaid = 100m, Status = "Paid" }
            );

            ctx.SaveChanges();

            var svc = new ForecastService(ctx);
            var controller = new ForecastingController(svc);

            var result = controller.Index() as ViewResult;

            result.Should().NotBeNull();
            var vd = result!.ViewData;

            var ymNow  = DateOnly.FromDateTime(now).ToString("yyyy-MM");
            var ymPrev = DateOnly.FromDateTime(now.AddMonths(-1)).ToString("yyyy-MM");
            var txVol = vd["TransactionVolume"].Should().BeAssignableTo<Dictionary<string, int>>().Subject;
            txVol.Should().ContainKey(ymNow);
            txVol.Should().ContainKey(ymPrev);
            txVol[ymNow].Should().Be(2);   
            txVol[ymPrev].Should().Be(1); 

            var expectedAvg = (10m + 20m + 5m) / 3m;
            vd["AvgTransactionValue"].Should().BeOfType<decimal>().Which.Should().Be(expectedAvg);

            var cashFlows = vd["CashFlows"].Should().BeAssignableTo<Dictionary<string, decimal>>().Subject;
            cashFlows[ymNow].Should().Be(30m);
            cashFlows[ymPrev].Should().Be(5m);

            vd["TransactionAnomalies"].Should().BeAssignableTo<List<Transactions>>().Which.Should().BeEmpty();

            vd["CardExpenses"].Should().BeOfType<decimal>().Which.Should().Be(0m);
            vd["ActiveCards"].Should().BeOfType<int>().Which.Should().Be(0);

            vd["CardDefaultRisk"].Should().BeOfType<double>().Which.Should().Be(0d);

            vd["OverdueLoansRate"].Should().BeOfType<double>().Which.Should().Be(0.5d);

            vd["NewLoans"].Should().BeOfType<int>().Which.Should().Be(1);

            vd["RepaymentTrend"].Should().BeOfType<string>().Which.Should().Be("Delay increasing");

            vd["NewCustomers"].Should().BeOfType<int>().Which.Should().Be(2);

            vd["ChurnRate"].Should().BeOfType<double>().Which.Should().Be(0.5d);
        }
    }
}
