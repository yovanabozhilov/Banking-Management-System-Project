using System.Threading.Tasks;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models.ML;
using BankingManagmentApp.Services.Approval;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace BankingManagmentApp.Tests.Services
{
    public class LoanApprovalEngineTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(System.Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task DecideAsync_ShouldReturnPendingReview_WhenNoScore()
        {
            using var ctx = CreateContext();

            var scoring = new Mock<ICreditScoringService>();
            scoring.Setup(s => s.ComputeAsync(It.IsAny<string>(), It.IsAny<ApplicationFeatures>()))
                   .ReturnsAsync((CreditScoreResult?)null);

            var engine = new LoanApprovalEngine(ctx, scoring.Object, new LoanApprovalPolicy());

            var features = new ApplicationFeatures { RequestedAmount = 1000, TermMonths = 12 };
            var decision = await engine.DecideAsync("u1", features);

            Assert.Equal(ApprovalOutcome.PendingReview, decision.Outcome);
            Assert.Null(decision.ApprovedAmount);
        }

        [Fact]
        public async Task DecideAsync_ShouldAutoDecline_WhenHighRiskOrLowScore()
        {
            using var ctx = CreateContext();

            var scoring = new Mock<ICreditScoringService>();
            scoring.Setup(s => s.ComputeAsync("u1", It.IsAny<ApplicationFeatures>()))
                   .ReturnsAsync(new CreditScoreResult { RiskLevel = 4, Score = 550 });

            var engine = new LoanApprovalEngine(ctx, scoring.Object, new LoanApprovalPolicy());

            var features = new ApplicationFeatures { RequestedAmount = 1000, TermMonths = 12 };
            var decision = await engine.DecideAsync("u1", features);

            Assert.Equal(ApprovalOutcome.AutoDeclined, decision.Outcome);
            Assert.Null(decision.ApprovedAmount);
        }

        [Fact]
        public async Task DecideAsync_ShouldAutoApprove_WhenWithinPolicy()
        {
            using var ctx = CreateContext();

            // add net inflow/outflow data
            ctx.CreditFeaturesView.Add(new CreditFeaturesView
            {
                UserId = "u1",
                AvgMonthlyInflow = 2000,
                AvgMonthlyOutflow = 1000
            });
            await ctx.SaveChangesAsync();

            var scoring = new Mock<ICreditScoringService>();
            scoring.Setup(s => s.ComputeAsync("u1", It.IsAny<ApplicationFeatures>()))
                   .ReturnsAsync(new CreditScoreResult { RiskLevel = 1, Score = 720 });

            var policy = new LoanApprovalPolicy
            {
                AnnualInterest = 0.12m,
                MaxAmountRisk1 = 5000,
                MinScoreRisk1 = 650
            };

            var engine = new LoanApprovalEngine(ctx, scoring.Object, policy);

            var features = new ApplicationFeatures { RequestedAmount = 3000, TermMonths = 12 };
            var decision = await engine.DecideAsync("u1", features);

            Assert.Equal(ApprovalOutcome.AutoApproved, decision.Outcome);
            Assert.Equal(3000, decision.ApprovedAmount);
        }

        [Fact]
        public async Task DecideAsync_ShouldCapByAffordability_WhenPaymentTooHigh()
        {
            using var ctx = CreateContext();

            ctx.CreditFeaturesView.Add(new CreditFeaturesView
            {
                UserId = "u1",
                AvgMonthlyInflow = 1000,
                AvgMonthlyOutflow = 900  // net flow = 100
            });
            await ctx.SaveChangesAsync();

            var scoring = new Mock<ICreditScoringService>();
            scoring.Setup(s => s.ComputeAsync("u1", It.IsAny<ApplicationFeatures>()))
                   .ReturnsAsync(new CreditScoreResult { RiskLevel = 1, Score = 700 });

            var policy = new LoanApprovalPolicy
            {
                AnnualInterest = 0.1m,
                MaxAmountRisk1 = 5000,
                MinScoreRisk1 = 650,
                MaxInstallmentToNetFlow = 1.0m // can only spend netFlow = 100
            };

            var engine = new LoanApprovalEngine(ctx, scoring.Object, policy);

            var features = new ApplicationFeatures { RequestedAmount = 2000, TermMonths = 12 };
            var decision = await engine.DecideAsync("u1", features);

            Assert.Equal(ApprovalOutcome.AutoApproved, decision.Outcome);
            Assert.NotNull(decision.ApprovedAmount);
            Assert.True(decision.ApprovedAmount < 2000); // capped lower
        }
    }
}
