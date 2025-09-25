using System;
using System.Linq;
using System.Threading.Tasks;
using BankingManagmentApp.Controllers;
using BankingManagmentApp.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BankingManagmentApp.Tests.Controllers
{
    public class AdminDashboardControllerApiTests
    {
        [Fact]
        public async Task SentimentAnalysis_Classifies_And_Sets_Counters()
        {
            using var ctx = CtxHelper.NewInMemoryContext();

            ctx.Feedbacks.AddRange(
                new Feedback { Comment = "I love the app, it is excellent!" },
                new Feedback { Comment = "This is the worst service, awful experience." },
                new Feedback { Comment = "OK" }
            );
            await ctx.SaveChangesAsync();

            var sut = new AdminDashboardController(ctx);
            var res = await sut.SentimentAnalysis() as ViewResult;
            res.Should().NotBeNull();

            ((int)sut.ViewBag.PositiveCount).Should().Be(1);
            ((int)sut.ViewBag.NegativeCount).Should().Be(1);
            ((int)sut.ViewBag.NeutralCount).Should().Be(1);
        }

        [Fact]
        public async Task CategoryComparison_Splits_Credit_Debit()
        {
            using var ctx = CtxHelper.NewInMemoryContext();

            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            ctx.Transactions.AddRange(
                new Transactions { TransactionType = "Credit", Amount = 100, Date = today },
                new Transactions { TransactionType = "Credit", Amount = 50,  Date = today },
                new Transactions { TransactionType = "Debit",  Amount = 30,  Date = today }
            );
            await ctx.SaveChangesAsync();

            var sut = new AdminDashboardController(ctx);
            var res = await sut.CategoryComparison() as ViewResult;
            res.Should().NotBeNull();

            var list = (System.Collections.Generic.List<BankingManagmentApp.ViewModels.CategoryComparisonVm>)res!.Model!;
            list.Single(x => x.Category == "Credit").Income.Should().Be(150);
            list.Single(x => x.Category == "Debit").Expense.Should().Be(30);
        }
    }
}
