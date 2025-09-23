// using BankingManagmentApp.Controllers;
// using BankingManagmentApp.Data;
// using BankingManagmentApp.Models;
// using BankingManagmentApp.Services;
// using FluentAssertions;
// using Microsoft.AspNetCore.Http;
// using Microsoft.AspNetCore.Identity;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.EntityFrameworkCore;
// using Moq;
// using System;
// using System.Collections.Generic;
// using System.Security.Claims;
// using System.Text;
// using System.Threading.Tasks;
// using Xunit;

// namespace BankingManagmentApp.Tests.Controllers
// {
//     public class ProfileControllerApiTests
//     {
//         private ApplicationDbContext GetDbContext()
//         {
//             var options = new DbContextOptionsBuilder<ApplicationDbContext>()
//                 .UseInMemoryDatabase(Guid.NewGuid().ToString())
//                 .Options;
//             return new ApplicationDbContext(options);
//         }

//         private static Mock<UserManager<Customers>> GetUserManagerMock()
//         {
//             var store = new Mock<IUserStore<Customers>>();
//             return new Mock<UserManager<Customers>>(store.Object, null, null, null, null, null, null, null, null);
//         }

//         private ProfileController GetController(
//             ApplicationDbContext context,
//             Customers user,
//             ICreditScoringService scoring)
//         {
//             var userMgr = GetUserManagerMock();
//             userMgr.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
//                    .ReturnsAsync(user);

//             var controller = new ProfileController(context, userMgr.Object, scoring);

//             var claims = new ClaimsPrincipal(new ClaimsIdentity(new[]
//             {
//                 new Claim(ClaimTypes.NameIdentifier, user.Id)
//             }, "mock"));

//             controller.ControllerContext = new ControllerContext
//             {
//                 HttpContext = new DefaultHttpContext { User = claims }
//             };

//             return controller;
//         }

//         [Fact]
//         public async Task Index_ShouldReturnViewWithProfileVm()
//         {
//             var context = GetDbContext();
//             var user = new Customers { Id = "u1", UserName = "test" };

//             // seed account + loan
//             var account = new Accounts
//             {
//                 Id = 1,
//                 CustomerId = user.Id,
//                 IBAN = "IBAN123",
//                 AccountType = "Savings",
//                 Currency = "EUR",
//                 Balance = 1000,
//                 CreateAt = DateTime.UtcNow
//             };
//             var loan = new Loans
//             {
//                 Id = 1,
//                 CustomerId = user.Id,
//                 Type = "Personal",
//                 Amount = 5000,
//                 ApprovedAmount = 5000,
//                 Term = DateOnly.FromDateTime(DateTime.Today.AddYears(1)),
//                 Status = "Approved",
//                 Date = DateTime.UtcNow
//             };
//             context.Accounts.Add(account);
//             context.Loans.Add(loan);
//             await context.SaveChangesAsync();

//             var scoringMock = new Mock<ICreditScoringService>();
//             scoringMock.Setup(s => s.ComputeAsync(user.Id))
//                        .ReturnsAsync(new CreditScoreResult { Score = 700, RiskLevel = "Medium", Notes = "ok" });

//             var controller = GetController(context, user, scoringMock.Object);

//             var result = await controller.Index();

//             result.Should().BeOfType<ViewResult>();
//             var vm = (result as ViewResult)!.Model as BankingManagmentApp.ViewModels.ProfileVm;
//             vm.Should().NotBeNull();
//             vm!.User.Id.Should().Be(user.Id);
//             vm.Accounts.Should().HaveCount(1);
//             vm.Loans.Should().HaveCount(1);
//             vm.Credit!.CreditScore.Should().Be(700);
//         }

//         [Fact]
//         public async Task ExportAccounts_ShouldReturnCsvFile()
//         {
//             var context = GetDbContext();
//             var user = new Customers { Id = "u2", UserName = "tester" };

//             context.Accounts.Add(new Accounts
//             {
//                 Id = 1,
//                 CustomerId = user.Id,
//                 IBAN = "ACC123",
//                 AccountType = "Checking",
//                 Currency = "USD",
//                 Balance = 123.45m,
//                 CreateAt = DateTime.UtcNow
//             });
//             await context.SaveChangesAsync();

//             var controller = GetController(context, user, Mock.Of<ICreditScoringService>());
//             var result = await controller.ExportAccounts(null);

//             result.Should().BeOfType<FileContentResult>();
//             var file = (FileContentResult)result;
//             file.ContentType.Should().Be("text/csv");
//             var content = Encoding.UTF8.GetString(file.FileContents);
//             content.Should().Contain("IBAN");
//             content.Should().Contain("ACC123");
//         }

//         [Fact]
//         public async Task ExportTransactions_ShouldForbid_WhenAccountNotOwned()
//         {
//             var context = GetDbContext();
//             var user = new Customers { Id = "u3", UserName = "baduser" };

//             // another user owns account
//             context.Accounts.Add(new Accounts { Id = 10, CustomerId = "other", IBAN = "X", AccountType = "Y", Currency = "USD" });
//             await context.SaveChangesAsync();

//             var controller = GetController(context, user, Mock.Of<ICreditScoringService>());
//             var result = await controller.ExportTransactions(10, null, null, null, null);

//             result.Should().BeOfType<ForbidResult>();
//         }

//         [Fact]
//         public async Task SearchTransactions_ShouldReturnFilteredTransactions()
//         {
//             var context = GetDbContext();
//             var user = new Customers { Id = "u4", UserName = "searcher" };

//             var acc = new Accounts
//             {
//                 Id = 20,
//                 CustomerId = user.Id,
//                 IBAN = "IB20",
//                 AccountType = "Savings",
//                 Currency = "EUR",
//                 Balance = 200,
//                 CreateAt = DateTime.UtcNow
//             };
//             context.Accounts.Add(acc);

//             // Transactions must point to the account acc
//             context.Transactions.AddRange(
//                 new Transactions
//                 {
//                     Id = 1,
//                     AccountsId = 2,
//                     Date = DateOnly.FromDateTime(DateTime.Today),
//                     Amount = 100,
//                     TransactionType = "deposit",
//                     Description = "salary",
//                     ReferenceNumber = 1001,
//                     Accounts = acc
//                 },
//                 new Transactions
//                 {
//                     Id = 2,
//                     AccountsId = 3,
//                     Date = DateOnly.FromDateTime(DateTime.Today),
//                     Amount = -50,
//                     TransactionType = "withdrawal",
//                     Description = "atm",
//                     ReferenceNumber = 1002,
//                     Accounts = acc
//                 }
//             );

//             await context.SaveChangesAsync();

//             var controller = GetController(context, user, Mock.Of<ICreditScoringService>());

//             var result = await controller.SearchTransactions(acc.Id, null, null, "salary", "deposit");

//             result.Should().BeOfType<ViewResult>();
//             var vm = (result as ViewResult)!.Model as BankingManagmentApp.ViewModels.ProfileVm;
//             vm.Should().NotBeNull();

//             // Assuming TransactionType is the list of transactions
//             vm!.TransactionType.Should().HaveCount(1);
//             vm.TransactionType[0].Description.Should().Be("salary");
//         }
//     }
// }
