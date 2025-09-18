using System.Threading.Tasks;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models.ML;
using BankingManagmentApp.Services.Approval;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using BankingManagmentApp.Services;   // for ICreditScoringService + CreditScoreResult

namespace BankingManagmentApp.Tests.Services
{
    public class LoanApprovalEngineTests
    {
        private ApplicationDbContext GetDbContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            return new ApplicationDbContext(options);
        }

        private LoanApprovalPolicy GetPolicy() => new LoanApprovalPolicy
        {
            MaxAmountRisk1 = 10000,
            MaxAmountRisk2 = 5000,
            MaxAmountRisk3 = 2000,
            MinScoreRisk1 = 700,
            MinScoreRisk2 = 650,
            MinScoreRisk3 = 600,
            AnnualInterest = 0.12m,
            DefaultMonths = 12,
            MaxInstallmentToNetFlow = 0.3m
        };

        [Fact]
        public async Task DecideAsync_NoScore_ReturnsPendingReview()
        {
            var db = GetDbContext(nameof(DecideAsync_NoScore_ReturnsPendingReview));
            var scoring = new Mock<ICreditScoringService>();
            scoring.Setup(s => s.ComputeAsync(It.IsAny<string>(), It.IsAny<ApplicationFeatures>()))
                   .ReturnsAsync((CreditScoreResult)null);

            var engine = new LoanApprovalEngine(db, scoring.Object, GetPolicy());

            var decision = await engine.DecideAsync("u1", new ApplicationFeatures { RequestedAmount = 2000 });

            Assert.Equal(ApprovalOutcome.PendingReview, decision.Outcome);
            Assert.Null(decision.ApprovedAmount);
        }

        [Fact]
        public async Task DecideAsync_HighRiskOrLowScore_ReturnsAutoDeclined()
        {
            var db = GetDbContext(nameof(DecideAsync_HighRiskOrLowScore_ReturnsAutoDeclined));
            var scoring = new Mock<ICreditScoringService>();
            scoring.Setup(s => s.ComputeAsync(It.IsAny<string>(), It.IsAny<ApplicationFeatures>()))
                   .ReturnsAsync(new CreditScoreResult { RiskLevel = 4, Score = 500 });

            var engine = new LoanApprovalEngine(db, scoring.Object, GetPolicy());

            var decision = await engine.DecideAsync("u1", new ApplicationFeatures { RequestedAmount = 1000 });

            Assert.Equal(ApprovalOutcome.AutoDeclined, decision.Outcome);
        }

        [Fact]
        public async Task DecideAsync_RequestFarAboveRiskCap_ReturnsPendingReview()
        {
            var db = GetDbContext(nameof(DecideAsync_RequestFarAboveRiskCap_ReturnsPendingReview));
            db.CreditFeaturesView.Add(new CreditFeatures { UserId = "u1", AvgMonthlyInflow = 2000, AvgMonthlyOutflow = 500 });
            db.SaveChanges();

            var scoring = new Mock<ICreditScoringService>();
            scoring.Setup(s => s.ComputeAsync(It.IsAny<string>(), It.IsAny<ApplicationFeatures>()))
                   .ReturnsAsync(new CreditScoreResult { RiskLevel = 2, Score = 700 });

            var engine = new LoanApprovalEngine(db, scoring.Object, GetPolicy());

            var decision = await engine.DecideAsync("u1", new ApplicationFeatures { RequestedAmount = 10000 });

            Assert.Equal(ApprovalOutcome.PendingReview, decision.Outcome);
        }

        [Fact]
        public async Task DecideAsync_WithinPolicy_ReturnsAutoApproved()
        {
            var db = GetDbContext(nameof(DecideAsync_WithinPolicy_ReturnsAutoApproved));
            db.CreditFeaturesView.Add(new CreditFeatures { UserId = "u1", AvgMonthlyInflow = 2000, AvgMonthlyOutflow = 500 });
            db.SaveChanges();

            var scoring = new Mock<ICreditScoringService>();
            scoring.Setup(s => s.ComputeAsync(It.IsAny<string>(), It.IsAny<ApplicationFeatures>()))
                   .ReturnsAsync(new CreditScoreResult { RiskLevel = 1, Score = 750 });

            var engine = new LoanApprovalEngine(db, scoring.Object, GetPolicy());

            var decision = await engine.DecideAsync("u1", new ApplicationFeatures { RequestedAmount = 3000 });

            Assert.Equal(ApprovalOutcome.AutoApproved, decision.Outcome);
            Assert.Equal(3000, decision.ApprovedAmount);
        }

        [Fact]
        public async Task DecideAsync_TooExpensiveButAffordableAmount_ReturnsCappedApproval()
        {
            var db = GetDbContext(nameof(DecideAsync_TooExpensiveButAffordableAmount_ReturnsCappedApproval));
            db.CreditFeaturesView.Add(new CreditFeatures { UserId = "u1", AvgMonthlyInflow = 1000, AvgMonthlyOutflow = 100 });
            db.SaveChanges();

            var scoring = new Mock<ICreditScoringService>();
            scoring.Setup(s => s.ComputeAsync(It.IsAny<string>(), It.IsAny<ApplicationFeatures>()))
                   .ReturnsAsync(new CreditScoreResult { RiskLevel = 2, Score = 700 });

            var engine = new LoanApprovalEngine(db, scoring.Object, GetPolicy());

            var decision = await engine.DecideAsync("u1", new ApplicationFeatures { RequestedAmount = 5000 });

            Assert.Equal(ApprovalOutcome.AutoApproved, decision.Outcome);
            Assert.True(decision.ApprovedAmount < 5000);
        }

        [Fact]
        public async Task DecideAsync_BorderlineScore_ReturnsPendingReview()
        {
            var db = GetDbContext(nameof(DecideAsync_BorderlineScore_ReturnsPendingReview));
            db.CreditFeaturesView.Add(new CreditFeatures { UserId = "u1", AvgMonthlyInflow = 1500, AvgMonthlyOutflow = 200 });
            db.SaveChanges();

            var scoring = new Mock<ICreditScoringService>();
            scoring.Setup(s => s.ComputeAsync(It.IsAny<string>(), It.IsAny<ApplicationFeatures>()))
                   .ReturnsAsync(new CreditScoreResult { RiskLevel = 1, Score = 650 }); // below MinScoreRisk1

            var engine = new LoanApprovalEngine(db, scoring.Object, GetPolicy());

            var decision = await engine.DecideAsync("u1", new ApplicationFeatures { RequestedAmount = 2000 });

            Assert.Equal(ApprovalOutcome.PendingReview, decision.Outcome);
        }
    }
}
