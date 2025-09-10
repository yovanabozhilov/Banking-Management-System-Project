using System;
using System.Threading.Tasks;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;            
using Microsoft.EntityFrameworkCore;

namespace BankingManagmentApp.Services.Approval
{
    public interface ILoanWorkflow
    {
        Task ProcessNewApplicationAsync(Loans loan);
    }

    public class LoanWorkflow : ILoanWorkflow
    {
        private readonly ApplicationDbContext _db;
        private readonly ILoanApprovalEngine _engine;
        private readonly LoanApprovalPolicy _policy;

        public LoanWorkflow(ApplicationDbContext db, ILoanApprovalEngine engine, LoanApprovalPolicy policy)
        {
            _db = db;
            _engine = engine;
            _policy = policy;
        }

        public async Task ProcessNewApplicationAsync(Loans loan)
        {
            if (loan == null) throw new ArgumentNullException(nameof(loan));
            if (string.IsNullOrWhiteSpace(loan.CustomerId))
                throw new InvalidOperationException("Loan.CustomerId is required before workflow.");

            using var tx = await _db.Database.BeginTransactionAsync();

            var app = new ApplicationFeatures
            {
                RequestedAmount = loan.ApprovedAmount > 0 ? loan.ApprovedAmount : loan.Amount,
                TermMonths      = CalculateTermMonths(loan),
                Product         = MapProduct(loan.Type)
            };

            var decision = await _engine.DecideAsync(loan.CustomerId, app);

            _db.CreditAssessments.Add(new CreditAssessments
            {
                LoanId = loan.Id,
                CreditScore = decision.Score,
                RiskLevel = decision.RiskLevel,
                Notes = decision.Reason
            });

            switch (decision.Outcome)
            {
                case ApprovalOutcome.AutoApproved:
                    loan.Status = "AutoApproved";
                    loan.ApprovedAmount = decision.ApprovedAmount ?? loan.Amount;
                    loan.ApprovalDate = DateTime.UtcNow;

                    if (loan.Term == default)
                        loan.Term = DateOnly.FromDateTime(DateTime.Today.AddMonths(app.TermMonths > 0 ? app.TermMonths
                                                                                                    : _policy.DefaultMonths));

                    var months = app.TermMonths > 0 ? app.TermMonths : _policy.DefaultMonths;
                    await GenerateRepaymentPlanAsync(loan, months, _policy.AnnualInterest);
                    break;

                case ApprovalOutcome.AutoDeclined:
                    loan.Status = "AutoDeclined";
                    loan.ApprovedAmount = 0;
                    loan.ApprovalDate = DateTime.UtcNow;
                    break;

                default:
                    loan.Status = "PendingReview";
                    break;
            }

            _db.Loans.Update(loan);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }

        private async Task GenerateRepaymentPlanAsync(Loans loan, int months, decimal annualInterest)
        {
            if (months <= 0) months = 12;

            var principal = loan.ApprovedAmount > 0 ? loan.ApprovedAmount : loan.Amount;
            if (principal <= 0) principal = loan.Amount;

            var mRate = annualInterest / 12m;

            decimal monthly =
                mRate == 0
                    ? Math.Round(principal / months, 2)
                    : Math.Round(
                        principal * (mRate * (decimal)Math.Pow(1 + (double)mRate, months)) /
                        (decimal)(Math.Pow(1 + (double)mRate, months) - 1),
                        2);

            var firstDue = DateOnly.FromDateTime(DateTime.Today).AddMonths(1);

            for (int i = 0; i < months; i++)
            {
                _db.LoanRepayments.Add(new LoanRepayments
                {
                    LoanId = loan.Id,
                    DueDate = firstDue.AddMonths(i),
                    AmountDue = monthly,
                    AmountPaid = 0,
                    PaymentDate = null,
                    Status = "Scheduled"
                });
            }

            await _db.SaveChangesAsync();
        }

        private int CalculateTermMonths(Loans loan)
        {
            if (loan.Term == default)
                return _policy.DefaultMonths;

            var days = (loan.Term.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days;
            var months = days / 30;
            if (months < 1) months = 1;
            return months;
        }

        private static ProductType MapProduct(string? type)
        {
            var t = type?.Trim().ToLowerInvariant() ?? string.Empty;
            return t switch
            {
                "mortgage"   => ProductType.Mortgage,
                "auto"       => ProductType.Auto,
                "creditcard" => ProductType.CreditCard,
                _            => ProductType.Personal
            };
        }
    }
}
