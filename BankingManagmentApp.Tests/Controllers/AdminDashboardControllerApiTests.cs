using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BankingManagmentApp.Controllers;
using BankingManagmentApp.Models;
using BankingManagmentApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BankingManagmentApp.Tests.Controllers
{
    public class AdminDashboardControllerMoreTests
    {
        [Fact]
        public async Task Benchmarking_UsesCreditDebitDirection_WhenTypesPresent()
        {
            using var ctx = CtxHelper.NewInMemoryContext();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            ctx.Transactions.AddRange(
                new Transactions { TransactionType = "Credit", Amount = 120m, Date = today },
                new Transactions { TransactionType = "Credit", Amount = 30m,  Date = today },
                new Transactions { TransactionType = "Debit",  Amount = 50m,  Date = today },
                new Transactions { TransactionType = "Debit",  Amount = 20m,  Date = today }
            );
            await ctx.SaveChangesAsync();

            var sut = new AdminDashboardController(ctx);
            var result = await sut.Benchmarking() as ViewResult;

            Assert.NotNull(result);
            Assert.NotNull(result!.Model);

            Assert.IsType<string>(sut.ViewBag.BenchmarkMode);
            string mode = (string)sut.ViewBag.BenchmarkMode;
            Assert.Contains("Detected Credit/Debit", mode);

            var model = Assert.IsAssignableFrom<List<CategoryBenchmarkVm>>(result.Model);

            var credit = model.SingleOrDefault(m => string.Equals(m.Category, "Credit", StringComparison.OrdinalIgnoreCase));
            var debit  = model.SingleOrDefault(m => string.Equals(m.Category, "Debit",  StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(credit);
            Assert.NotNull(debit);

            Assert.Equal(150m, credit!.Income);
            Assert.Equal(0m,   credit.Expense);

            Assert.Equal(0m,   debit!.Income);
            Assert.Equal(70m,  debit.Expense);

            Assert.True(credit.IndustryAvgIncome  >= 0);
            Assert.True(credit.IndustryAvgExpense >= 0);
            Assert.True(debit.IndustryAvgIncome   >= 0);
            Assert.True(debit.IndustryAvgExpense  >= 0);
        }

        [Fact]
        public async Task Benchmarking_UsesSignedAmounts_WhenNoCreditDebitButNegativesExist()
        {
            using var ctx = CtxHelper.NewInMemoryContext();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            ctx.Transactions.AddRange(
                new Transactions { TransactionType = "Groceries", Amount = -200m, Date = today },
                new Transactions { TransactionType = "Salary",    Amount =  3000m, Date = today },
                new Transactions { TransactionType = "Rent",      Amount = -800m,  Date = today }
            );
            await ctx.SaveChangesAsync();

            var sut = new AdminDashboardController(ctx);
            var result = await sut.Benchmarking() as ViewResult;

            Assert.NotNull(result);
            Assert.IsType<string>(sut.ViewBag.BenchmarkMode);
            string mode = (string)sut.ViewBag.BenchmarkMode;
            Assert.Contains("signed amounts", mode, StringComparison.OrdinalIgnoreCase);

            var model = Assert.IsAssignableFrom<List<CategoryBenchmarkVm>>(result!.Model);

            var groceries = model.Single(x => x.Category == "Groceries");
            var salary    = model.Single(x => x.Category == "Salary");
            var rent      = model.Single(x => x.Category == "Rent");

            Assert.Equal(0m,    groceries.Income);
            Assert.Equal(200m,  groceries.Expense);

            Assert.Equal(3000m, salary.Income);
            Assert.Equal(0m,    salary.Expense);

            Assert.Equal(0m,    rent.Income);
            Assert.Equal(800m,  rent.Expense);
        }

        [Fact]
        public async Task Index_Computes_Totals_And_MonthOverMonth_Changes()
        {
            using var ctx = CtxHelper.NewInMemoryContext();

            ctx.Customers.AddRange(
                new Customers { UserName = "u1", CreateAt = DateTime.UtcNow.AddDays(-2) },
                new Customers { UserName = "u2", CreateAt = DateTime.UtcNow }
            );
            ctx.Loans.Add(new Loans
            {
                CustomerId = "u1",
                Amount = 1000m,
                Date = DateTime.UtcNow.AddDays(-10),
                Type = "Personal"
            });
            ctx.Accounts.AddRange(
                new Accounts { CustomerId = "u1", Balance = 100m, CreateAt = DateTime.UtcNow.AddDays(-1), IBAN = "IBAN1", AccountType = "Checking", Currency = "USD" },
                new Accounts { CustomerId = "u2", Balance = 50m,  CreateAt = DateTime.UtcNow,              IBAN = "IBAN2", AccountType = "Checking", Currency = "USD" }
            );

            var now = DateTime.UtcNow;
            var curMonth = new DateOnly(now.Year, now.Month, 1);
            var prevMonthDate = curMonth.AddMonths(-1);

            ctx.Transactions.AddRange(
                new Transactions { TransactionType = "Credit", Amount = 100m, Date = prevMonthDate },
                new Transactions { TransactionType = "Debit",  Amount =  40m, Date = prevMonthDate }
            );

            ctx.Transactions.AddRange(
                new Transactions { TransactionType = "Credit", Amount = 200m, Date = curMonth },
                new Transactions { TransactionType = "Debit",  Amount =  10m, Date = curMonth }
            );

            await ctx.SaveChangesAsync();

            var sut = new AdminDashboardController(ctx);
            var result = await sut.Index() as ViewResult;

            Assert.NotNull(result);

            Assert.Equal(2, (int)sut.ViewBag.TotalClients);
            Assert.Equal(1, (int)sut.ViewBag.TotalCredits);
            Assert.Equal(2, (int)sut.ViewBag.TotalCards);

            Assert.Equal(300m, (decimal)sut.ViewBag.TotalDeposits);
            Assert.Equal(50m,  (decimal)sut.ViewBag.TotalWithdrawals);

            float depChange = (float)sut.ViewBag.DepositsChange;
            float wdrChange = (float)sut.ViewBag.WithdrawalsChange;

            Assert.InRange(depChange, 99.9f, 100.1f);  
            Assert.InRange(wdrChange, -75.1f, -74.9f);  

            int currentMonth = DateTime.Now.Month;
            int currentYear  = DateTime.Now.Year;
            Assert.Equal(
                ctx.Customers.Count(c => c.CreateAt.Month == currentMonth && c.CreateAt.Year == currentYear),
                (int)sut.ViewBag.NewClientsThisMonth
            );
            Assert.Equal(
                ctx.Accounts.Count(a => a.CreateAt.Month == currentMonth && a.CreateAt.Year == currentYear),
                (int)sut.ViewBag.NewCardsMonthly
            );

            Assert.NotNull(sut.ViewBag.MonthlyData);
        }
    }
}
