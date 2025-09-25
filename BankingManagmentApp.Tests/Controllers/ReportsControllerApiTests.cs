using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Claims;
using BankingManagmentApp.Controllers;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services;
using BankingManagmentApp.ViewModels.Reports;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BankingManagmentApp.Tests.Controllers
{
    public class ReportsControllerApiTests
    {
        private ApplicationDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private ReportsController GetController(ApplicationDbContext context, string userEmail = "admin@example.com")
        {
            var emailMock = new Mock<IEmailService>();
            var controller = new ReportsController(context, emailMock.Object);

            var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, userEmail)
            }, "mock"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claims }
            };

            return controller;
        }

        [Fact]
        public async Task Index_Get_ShouldReturnViewWithEmptyResults()
        {
            var context = GetDbContext();
            var controller = GetController(context);

            var result = await controller.Index();

            result.Should().BeOfType<ViewResult>();
            var vm = (result as ViewResult)!.Model as ReportResultVm;
            vm.Should().NotBeNull();
            vm!.Rows.Should().BeEmpty();
            vm.Filters.Should().NotBeNull();
        }

        [Fact]
        public async Task Index_Post_ShouldReturnReportResults()
        {
            var context = GetDbContext();

            var acc = new Accounts { Id = 1, IBAN = "ACC1", Currency = "USD", AccountType = "Checking", CustomerId = "u1" };
            context.Accounts.Add(acc);

            context.Transactions.AddRange(
                new Transactions { Id = 1, AccountsId = acc.Id, Date = DateOnly.FromDateTime(DateTime.Today), Amount = 100, TransactionType = "deposit", Description = "salary", ReferenceNumber = 1, Accounts = acc },
                new Transactions { Id = 2, AccountsId = acc.Id, Date = DateOnly.FromDateTime(DateTime.Today), Amount = 50, TransactionType = "withdrawal", Description = "atm", ReferenceNumber = 2, Accounts = acc }
            );

            await context.SaveChangesAsync();

            var controller = GetController(context);

            var filters = new ReportFilterVm { AccountId = acc.Id, From = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), To = DateOnly.FromDateTime(DateTime.Today), GroupBy = ReportGroupBy.Monthly };
            var result = await controller.Index(filters);

            result.Should().BeOfType<ViewResult>();
            var vm = (result as ViewResult)!.Model as ReportResultVm;
            vm.Should().NotBeNull();
            vm!.Rows.Should().HaveCount(1);
            vm.Rows[0].TotalTransactions.Should().Be(2);
            vm.Rows[0].AmountByType["deposit"].Should().Be(100);
            vm.Rows[0].AmountByType["withdrawal"].Should().Be(50);
        }

        [Fact]
        public async Task ExportPdf_ShouldReturnFileAndSendEmail()
        {
            var context = GetDbContext();

            var acc = new Accounts { Id = 1, IBAN = "ACC1", Currency = "USD", AccountType = "Checking", CustomerId = "u1" };
            context.Accounts.Add(acc);
            context.Transactions.Add(new Transactions { Id = 1, AccountsId = acc.Id, Date = DateOnly.FromDateTime(DateTime.Today), Amount = 100, TransactionType = "deposit", Description = "salary", ReferenceNumber = 1, Accounts = acc });
            await context.SaveChangesAsync();

            var emailMock = new Mock<IEmailService>();
            var controller = new ReportsController(context, emailMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "admin@example.com") }, "mock"))
                }
            };

            var filters = new ReportFilterVm { AccountId = acc.Id, From = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), To = DateOnly.FromDateTime(DateTime.Today) };
            var result = await controller.ExportPdf(filters);

            result.Should().BeOfType<FileContentResult>();
            var file = result as FileContentResult;
            file!.ContentType.Should().Be("application/pdf");
            file.FileContents.Length.Should().BeGreaterThan(0);

            emailMock.Verify(e => e.SendEmailWithAttachmentAsync(
                "admin@example.com",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>()
            ), Times.Once);
        }

        [Fact]
        public async Task ExportExcel_ShouldReturnFileAndSendEmail()
        {
            var context = GetDbContext();

            var acc = new Accounts { Id = 1, IBAN = "ACC1", Currency = "USD", AccountType = "Checking", CustomerId = "u1" };
            context.Accounts.Add(acc);
            context.Transactions.Add(new Transactions { Id = 1, AccountsId = acc.Id, Date = DateOnly.FromDateTime(DateTime.Today), Amount = 100, TransactionType = "deposit", Description = "salary", ReferenceNumber = 1, Accounts = acc });
            await context.SaveChangesAsync();

            var emailMock = new Mock<IEmailService>();
            var controller = new ReportsController(context, emailMock.Object);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "admin@example.com") }, "mock"))
                }
            };

            var filters = new ReportFilterVm { AccountId = acc.Id, From = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)), To = DateOnly.FromDateTime(DateTime.Today) };
            var result = await controller.ExportExcel(filters);

            result.Should().BeOfType<FileContentResult>();
            var file = result as FileContentResult;
            file!.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            file.FileContents.Length.Should().BeGreaterThan(0);

            emailMock.Verify(e => e.SendEmailWithAttachmentAsync(
                "admin@example.com",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<string>()
            ), Times.Once);
        }
    }
}
