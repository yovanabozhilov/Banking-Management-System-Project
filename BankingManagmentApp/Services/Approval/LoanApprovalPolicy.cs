using System;

namespace BankingManagmentApp.Services.Approval
{
    public class LoanApprovalPolicy
    {
        public int MinScoreRisk1  = 700;
        public int MinScoreRisk2  = 660;
        public int MinScoreRisk3  = 600;
        public decimal MaxAmountRisk1 = 30000m;
        public decimal MaxAmountRisk2 = 15000m;
        public decimal MaxAmountRisk3 = 8000m;
        public decimal MaxInstallmentToNetFlow = 0.40m;
        public int DefaultMonths = 12;
        public decimal AnnualInterest = 0.10m; 
    }
}
