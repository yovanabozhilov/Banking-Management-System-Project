using System;
using System.Linq;
using System.Threading.Tasks;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BankingManagmentApp.Tests.Services.AI
{
    public class ChatToolsTests
    {
        private static ApplicationDbContext NewDb()
        {
            var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(opts);
        }

        [Fact]
        public async Task GetBalanceAsync_SumsOnlyUsersAccounts()
        {
            using var db = NewDb();
            var u1 = new Customers { Id = "u1", Email = "a@a.a", UserName = "a@a.a" };
            var u2 = new Customers { Id = "u2", Email = "b@b.b", UserName = "b@b.b" };
            db.Users.AddRange(u1, u2);

            db.Accounts.AddRange(
                new Accounts { CustomerId = "u1", Balance = 5m, Currency = "BGN", IBAN = "BG1", AccountType = "C", Customer = u1 },
                new Accounts { CustomerId = "u1", Balance = 7m, Currency = "BGN", IBAN = "BG2", AccountType = "C", Customer = u1 },
                new Accounts { CustomerId = "u2", Balance = 9m, Currency = "BGN", IBAN = "BG3", AccountType = "C", Customer = u2 }
            );
            await db.SaveChangesAsync();

            var tools = new ChatTools(db);
            var sum = await tools.GetBalanceAsync("u1");
            Assert.Equal(12m, sum);
        }

        [Fact]
        public async Task GetRecentTransactionsAsync_ReturnsProjectionWithIban()
        {
            using var db = NewDb();
            var u = new Customers { Id = "u1", Email = "x@y.z", UserName = "x@y.z" };
            db.Users.Add(u);
            var acc = new Accounts { CustomerId = "u1", IBAN = "BG11AAAA...", Currency = "BGN", AccountType = "C", Balance = 0m, Customer = u };
            db.Accounts.Add(acc);
            db.Transactions.AddRange(
                new Transactions { TransactionType = "Credit", Amount = 1m, Description = "A", Accounts = acc },
                new Transactions { TransactionType = "Debit",  Amount = 2m, Description = "B", Accounts = acc }
            );
            await db.SaveChangesAsync();

            var tools = new ChatTools(db);
            var list = await tools.GetRecentTransactionsAsync("u1", 2);
            Assert.Equal(2, list.Count);

            var first = list[0];
            var iban = first.GetType().GetProperty("AccountIban")!.GetValue(first)?.ToString();
            Assert.Contains("BG11AAAA", iban);
        }

        [Fact]
        public async Task GetLoanStatusAsync_NoLoans_ReturnsMessage()
        {
            using var db = NewDb();
            var tools = new ChatTools(db);
            var status = await tools.GetLoanStatusAsync("missing");
            Assert.Equal("No active applications found.", status);
        }

        [Fact]
        public async Task GetLoanStatusAsync_ReturnsLatestByIdDesc()
        {
            using var db = NewDb();
            var u = new Customers { Id = "u1", Email = "x@y.z", UserName = "x@y.z" };
            db.Users.Add(u);
            db.Loans.Add(new Loans { CustomerId = "u1", Type = "Personal", Status = "Approved", Customer = u });
            await db.SaveChangesAsync();

            db.Loans.Add(new Loans { CustomerId = "u1", Type = "Mortgage", Status = "Pending", Customer = u });
            await db.SaveChangesAsync();

            var tools = new ChatTools(db);
            var status = await tools.GetLoanStatusAsync("u1");
            Assert.Contains("Mortgage", status);
            Assert.Contains("Pending", status);
        }
    }
}
