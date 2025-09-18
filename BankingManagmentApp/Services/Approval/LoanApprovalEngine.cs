using System;
using System.Linq;
using System.Threading.Tasks;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models.ML;
using Microsoft.EntityFrameworkCore;

using BankingManagmentApp.Services; // <-- for ICreditScoringService + CreditScoreResult


namespace BankingManagmentApp.Services.Approval
{
    public interface ILoanApprovalEngine
    {
        Task<ApprovalDecision> DecideAsync(string userId, ApplicationFeatures app);
    }

    public class LoanApprovalEngine : ILoanApprovalEngine
    {
        private readonly ApplicationDbContext _db;
        private readonly LoanApprovalPolicy _policy;
        private readonly ICreditScoringService _scoring;

        public LoanApprovalEngine(ApplicationDbContext db, ICreditScoringService scoring, LoanApprovalPolicy policy)
        {
            _db = db;
            _scoring = scoring;
            _policy = policy;
        }

        public async Task<ApprovalDecision> DecideAsync(string userId, ApplicationFeatures app)
        {
            var score = await _scoring.ComputeAsync(userId, app);
            if (score is null)
                return new(ApprovalOutcome.PendingReview, null, 0, 0, "No credit features for user.");

            var f = await _db.CreditFeaturesView.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == userId);
            var netFlow = (f?.AvgMonthlyInflow ?? 0) - (f?.AvgMonthlyOutflow ?? 0);

            if (score.RiskLevel >= 4 || score.Score < 600)
                return new(ApprovalOutcome.AutoDeclined, null, score.RiskLevel, score.Score, "High risk / low score.");

            decimal riskCap = score.RiskLevel switch
            {
                1 => _policy.MaxAmountRisk1,
                2 => _policy.MaxAmountRisk2,
                _ => _policy.MaxAmountRisk3
            };

            if (app.RequestedAmount > riskCap)
            {
                if (app.RequestedAmount > riskCap * 1.25m)
                    return new(ApprovalOutcome.PendingReview, null, score.RiskLevel, score.Score, "Requested >> risk cap.");
            }

            int months = _policy.DefaultMonths;
            if (app.TermMonths > 0) months = app.TermMonths;

            var monthlyRate = _policy.AnnualInterest / 12m;
            decimal MonthlyPayment(decimal principal) =>
                monthlyRate == 0 ? Math.Round(principal / months, 2)
                : Math.Round(principal * (monthlyRate * (decimal)Math.Pow(1 + (double)monthlyRate, months)) /
                              (decimal)(Math.Pow(1 + (double)monthlyRate, months) - 1), 2);

            var reqInstallment = MonthlyPayment(Math.Min(app.RequestedAmount, riskCap));
            var maxInstallment = Math.Max(0, netFlow) * _policy.MaxInstallmentToNetFlow;

            if (maxInstallment <= 0)
                return new(ApprovalOutcome.PendingReview, null, score.RiskLevel, score.Score, "Unknown/negative net monthly flow.");

            if (reqInstallment > maxInstallment)
            {
                decimal lo = 100, hi = Math.Min(riskCap, app.RequestedAmount);
                for (int i = 0; i < 20; i++)
                {
                    var mid = (lo + hi) / 2m;
                    if (MonthlyPayment(mid) <= maxInstallment) lo = mid; else hi = mid;
                }
                var affordable = Math.Floor(lo / 10m) * 10m;

                if (affordable < 500m)
                    return new(ApprovalOutcome.PendingReview, null, score.RiskLevel, score.Score, "Insufficient affordability.");

                return new(ApprovalOutcome.AutoApproved, affordable, score.RiskLevel, score.Score, "Capped by affordability.");
            }

            bool scoreOk = score.RiskLevel switch
            {
                1 => score.Score >= _policy.MinScoreRisk1,
                2 => score.Score >= _policy.MinScoreRisk2,
                _ => score.Score >= _policy.MinScoreRisk3
            };
            if (!scoreOk)
                return new(ApprovalOutcome.PendingReview, null, score.RiskLevel, score.Score, "Score borderline.");

            var approved = Math.Min(app.RequestedAmount, riskCap);
            return new(ApprovalOutcome.AutoApproved, approved, score.RiskLevel, score.Score, "Within policy.");
        }
    }
}
