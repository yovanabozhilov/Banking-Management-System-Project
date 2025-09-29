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
    public class KnowledgeBaseServiceTests
    {
        private static ApplicationDbContext NewDb()
        {
            var opts = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(opts);
        }

        [Fact]
        public async Task SearchAsync_ReturnsMatches_ByKeywordOrAnswer()
        {
            using var db = NewDb();
            db.TemplateAnswer.AddRange(
                new TemplateAnswer { Keyword = "fees",    AnswerText = "No monthly fees." },
                new TemplateAnswer { Keyword = "limits",  AnswerText = "Transfer limits apply." },
                new TemplateAnswer { Keyword = "support", AnswerText = "Contact support 24/7." }
            );
            await db.SaveChangesAsync();

            var kb = new KnowledgeBaseService(db);

            var res1 = await kb.SearchAsync("fee");
            Assert.NotEmpty(res1);
            Assert.Contains(res1, r => r.AnswerText.Contains("No monthly fees"));

            var res2 = await kb.SearchAsync("support");
            Assert.Single(res2.Where(r => r.Keyword == "support"));
        }

        [Fact]
        public async Task SearchAsync_VeryShortQuery_ReturnsEmpty()
        {
            using var db = NewDb();
            var kb = new KnowledgeBaseService(db);

            var resEmpty = await kb.SearchAsync("a"); 
            Assert.Empty(resEmpty);
        }
    }
}
