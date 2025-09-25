using BankingManagmentApp.Controllers;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using System.Linq;

namespace BankingManagmentApp.Tests.Controllers
{
    public class TransactionsControllerApiTests
    {
        private ApplicationDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .EnableSensitiveDataLogging()
                .Options;
            return new ApplicationDbContext(options);
        }

        private static Mock<UserManager<Customers>> GetUserManagerMock()
        {
            var store = new Mock<IUserStore<Customers>>();
            return new Mock<UserManager<Customers>>(store.Object, null, null, null, null, null, null, null, null);
        }

        private TransactionsController GetController(ApplicationDbContext context, Customers user)
        {
            var userMgr = GetUserManagerMock();
            userMgr.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);

            var controller = new TransactionsController(context, userMgr.Object);

            var http = new DefaultHttpContext();
            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            }, "mock"));

            controller.ControllerContext = new ControllerContext { HttpContext = http };

            controller.TempData = new TempDataDictionary(http, Mock.Of<ITempDataProvider>());

            return controller;
        }

        [Fact]
        public async Task Index_ReturnsAllTransactions()
        {
            var context = GetDbContext();
            var user = new Customers { Id = "u1", UserName = "test" };
            context.Customers.Add(user);
            await context.SaveChangesAsync();

            var acc = new Accounts
            {
                Id = 1,
                CustomerId = user.Id,
                IBAN = "IB123",
                AccountType = "Checking",
                Currency = "USD",
                Balance = 1000,
                Customer = user
            };
            context.Accounts.Add(acc);

            context.Transactions.Add(new Transactions
            {
                Id = 1,
                AccountsId = acc.Id,
                Amount = 100,
                TransactionType = "Credit",
                Date = DateOnly.FromDateTime(DateTime.Today),
                ReferenceNumber = 12345,
                Accounts = acc
            });
            await context.SaveChangesAsync();

            var controller = GetController(context, user);

            var result = await controller.Index();

            result.Should().BeOfType<ViewResult>();
            var model = (result as ViewResult)!.Model as List<Transactions>;
            model.Should().HaveCount(1);
        }

        [Fact]
        public async Task Details_ReturnsTransaction_WhenExists()
        {
            var context = GetDbContext();
            var user = new Customers { Id = "u2" };
            context.Customers.Add(user);
            await context.SaveChangesAsync();

            var acc = new Accounts
            {
                Id = 2,
                CustomerId = user.Id,
                IBAN = "IB124",
                AccountType = "Savings",
                Currency = "EUR",
                Balance = 500,
                Customer = user
            };
            context.Accounts.Add(acc);

            var t = new Transactions
            {
                Id = 1,
                AccountsId = acc.Id,
                Amount = 50,
                TransactionType = "Debit",
                Date = DateOnly.FromDateTime(DateTime.Today),
                ReferenceNumber = 11111,
                Accounts = acc
            };
            context.Transactions.Add(t);
            await context.SaveChangesAsync();

            var controller = GetController(context, user);
            var result = await controller.Details(1);

            result.Should().BeOfType<ViewResult>();
            var model = (result as ViewResult)!.Model as Transactions;
            model!.Id.Should().Be(1);
        }

        [Fact]
        public async Task Create_Post_AddsTransaction()
        {
            var context = GetDbContext();
            var user = new Customers { Id = "u3" };
            context.Customers.Add(user);
            await context.SaveChangesAsync();

            var acc = new Accounts
            {
                Id = 3,
                CustomerId = user.Id,
                Balance = 500,
                IBAN = "IB300",
                AccountType = "Checking",
                Currency = "USD",
                Customer = user
            };
            context.Accounts.Add(acc);
            await context.SaveChangesAsync();

            var controller = GetController(context, user);
            var newTransaction = new Transactions
            {
                AccountsId = acc.Id,
                Amount = 100,
                TransactionType = "Credit",
                Description = "Deposit",
                Date = DateOnly.FromDateTime(DateTime.Today),
                ReferenceNumber = 123456
            };

            controller.ModelState.AddModelError("x", "invalid"); 

            var result = await controller.Create(newTransaction);

            result.Should().BeOfType<RedirectToActionResult>();
            context.Transactions.Should().HaveCount(1);
            context.Accounts.First().Balance.Should().Be(600);
        }

        [Fact]
        public async Task Edit_Post_UpdatesTransaction()
        {
            var context = GetDbContext();
            var user = new Customers { Id = "u4" };
            context.Customers.Add(user);
            await context.SaveChangesAsync();

            var acc = new Accounts
            {
                Id = 4,
                CustomerId = user.Id,
                IBAN = "IB126",
                AccountType = "Savings",
                Currency = "GBP",
                Balance = 1000,
                Customer = user
            };
            context.Accounts.Add(acc);

            var t = new Transactions
            {
                Id = 1,
                AccountsId = acc.Id,
                Amount = 200,
                TransactionType = "Credit",
                Date = DateOnly.FromDateTime(DateTime.Today),
                ReferenceNumber = 22222,
                Accounts = acc
            };
            context.Transactions.Add(t);
            await context.SaveChangesAsync();

            var controller = GetController(context, user);

            t.Amount = 300;
            var result = await controller.Edit(1, t);

            result.Should().BeOfType<RedirectToActionResult>();
            context.Transactions.First().Amount.Should().Be(300);
        }

        [Fact]
        public async Task DeleteConfirmed_RemovesTransaction()
        {
            var context = GetDbContext();
            var user = new Customers { Id = "u5" };
            context.Customers.Add(user);
            await context.SaveChangesAsync();

            var acc = new Accounts
            {
                Id = 5,
                CustomerId = user.Id,
                IBAN = "IB127",
                AccountType = "Checking",
                Currency = "USD",
                Balance = 1000,
                Customer = user
            };
            context.Accounts.Add(acc);

            var t = new Transactions
            {
                Id = 1,
                AccountsId = acc.Id,
                Amount = 50,
                TransactionType = "Debit",
                Date = DateOnly.FromDateTime(DateTime.Today),
                ReferenceNumber = 33333,
                Accounts = acc
            };
            context.Transactions.Add(t);
            await context.SaveChangesAsync();

            var controller = GetController(context, user);
            var result = await controller.DeleteConfirmed(1);

            result.Should().BeOfType<RedirectToActionResult>();
            context.Transactions.Should().BeEmpty();
        }

        [Fact]
        public async Task Pay_ReducesBalanceAndMarksRepaymentPaid()
        {
            var context = GetDbContext();
            var user = new Customers { Id = "u6" };
            context.Customers.Add(user);
            await context.SaveChangesAsync();

            var acc = new Accounts
            {
                Id = 6,
                CustomerId = user.Id,
                Balance = 500,
                IBAN = "IB600",
                AccountType = "Checking",
                Currency = "USD",
                Customer = user 
            };
            context.Accounts.Add(acc);

            var loan = new Loans
            {
                Id = 1,
                CustomerId = user.Id,
                Amount = 1000,
                ApprovedAmount = 1000,
                Status = "Approved",
                Type = "Personal",
                Customer = user
            };
            context.Loans.Add(loan);

            var repayment = new LoanRepayments
            {
                Id = 1,
                LoanId = loan.Id,
                AmountDue = 200,
                Status = "Pending"
            };
            context.LoanRepayments.Add(repayment);
            await context.SaveChangesAsync();

            var controller = GetController(context, user);

            var result = await controller.Pay(1);

            result.Should().BeOfType<RedirectToActionResult>();
            context.LoanRepayments.First().Status.Should().Be("Paid");
            context.Accounts.First().Balance.Should().Be(300);
        }
    }
}
