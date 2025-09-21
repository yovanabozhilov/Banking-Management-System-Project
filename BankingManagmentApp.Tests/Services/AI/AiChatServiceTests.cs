using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Xunit;

namespace BankingManagmentApp.Tests.Services.AI
{
    /// <summary>
    /// Fake IChatClient – не бива да се вика в тези тестове (те покриват само tool-пътя).
    /// Ако бъде извикан, хвърля изключение.
    /// </summary>
    internal sealed class ThrowingChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("IChatClient should not be called in tool-path tests.");

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("IChatClient should not be called in tool-path tests.");
#pragma warning disable CS0162
            yield break;
#pragma warning restore CS0162
        }

        public object? GetService(Type serviceType, object? settings) => null;

        // изискван от IDisposable
        public void Dispose() { }
    }

    public class AiChatServiceTests
    {
        private static ApplicationDbContext NewDb()
        {
            var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(opts);
        }

        private static (AiChatService svc, ApplicationDbContext db, string userId) BuildService()
        {
            var db = NewDb();
            var userId = "u1";

            var customer = new Customers
            {
                Id = userId,
                FirstName = "Ivan",
                LastName = "Petrov",
                Email = "ivan@example.com",
                UserName = "ivan@example.com"
            };
            db.Users.Add(customer);

            var acc1 = new Accounts { CustomerId = userId, IBAN = "BG11AAAA11111111111111", AccountType = "Checking", Currency = "BGN", Balance = 100m, Customer = customer };
            var acc2 = new Accounts { CustomerId = userId, IBAN = "BG22BBBB22222222222222", AccountType = "Savings",  Currency = "BGN", Balance =  50m, Customer = customer };
            db.Accounts.AddRange(acc1, acc2);

            db.Transactions.AddRange(
                new Transactions { TransactionType = "Credit", Amount = 25m, Description = "Top-up", Accounts = acc1 },
                new Transactions { TransactionType = "Debit",  Amount = 10m, Description = "POS",    Accounts = acc1 },
                new Transactions { TransactionType = "Credit", Amount =  5m, Description = "Interest", Accounts = acc2 }
            );

            db.Loans.Add(new Loans { CustomerId = userId, Type = "Personal", Status = "Approved", Customer = customer });

            db.TemplateAnswer.AddRange(
                new TemplateAnswer { Keyword = "fees",  AnswerText = "No monthly fees for standard account." },
                new TemplateAnswer { Keyword = "login", AnswerText = "Please log in to see your data." }
            );

            db.SaveChanges();

            var tools = new ChatTools(db);
            var kb    = new KnowledgeBaseService(db);
            var chat  = new ThrowingChatClient();
            var svc   = new AiChatService(chat, kb, tools);

            return (svc, db, userId);
        }

        [Fact]
        public async Task UnauthenticatedPersonalQuery_ReturnsLoginPrompt()
        {
            var (svc, _, _) = BuildService();
            var text = await svc.SendAsync("what's my balance?", userId: null, userFirstName: "Ivan");
            Assert.Contains("Please log in", text, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task BalanceIntent_SumsAllAccounts_ForAuthenticatedUser()
        {
            var (svc, _, userId) = BuildService();
            var txt = await svc.SendAsync("How much is my balance?", userId, "Ivan");
            Assert.Contains("Your total balance across accounts is", txt);
            Assert.Contains("150.00", txt); // 100 + 50
        }

        [Fact]
        public async Task RecentTransactions_DefaultCount_ProducesFormattedLines()
        {
            var (svc, _, userId) = BuildService();
            var txt = await svc.SendAsync("show my recent transactions", userId, "Ivan");

            Assert.Contains("Here are your last", txt);
            Assert.Contains("#", txt);
            Assert.Contains("|", txt);
            Assert.Contains("POS", txt);
            Assert.Contains("BG11AAAA", txt);
        }

        [Fact]
        public async Task RecentTransactions_Last2_RespectsCount()
        {
            var (svc, db, userId) = BuildService();

            var acc = db.Accounts.First(a => a.CustomerId == userId);
            db.Transactions.AddRange(
                new Transactions { TransactionType = "Debit",  Amount = 3m, Description = "Coffee",   Accounts = acc },
                new Transactions { TransactionType = "Credit", Amount = 8m, Description = "Cashback", Accounts = acc }
            );
            await db.SaveChangesAsync();

            var txt = await svc.SendAsync("show my last 2 transactions", userId, "Ivan");
            Assert.Contains("last 2", txt);
        }

        [Fact]
        public async Task LoanStatus_ReturnsLatestLoan()
        {
            var (svc, db, userId) = BuildService();

            db.Loans.Add(new Loans { CustomerId = userId, Type = "Mortgage", Status = "Pending" });
            await db.SaveChangesAsync();

            var txt = await svc.SendAsync("what is my loan status?", userId, "Ivan");
            Assert.Contains("Loan status:", txt);
            Assert.Contains("Mortgage", txt);
            Assert.Contains("Pending", txt);
        }
    }
}
