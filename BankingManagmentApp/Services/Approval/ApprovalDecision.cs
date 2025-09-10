namespace BankingManagmentApp.Services.Approval
{
    public enum ApprovalOutcome
    {
        AutoApproved = 0,
        PendingReview = 1,
        AutoDeclined = 2
    }

    public sealed class ApprovalDecision
    {
        public ApprovalOutcome Outcome { get; init; }
        public decimal? ApprovedAmount { get; init; } 
        public int RiskLevel { get; init; }          
        public int Score { get; init; }               
        public string Reason { get; init; } = string.Empty;

        public ApprovalDecision(
            ApprovalOutcome outcome,
            decimal? approvedAmount,
            int riskLevel,
            int score,
            string reason)
        {
            Outcome = outcome;
            ApprovedAmount = approvedAmount;
            RiskLevel = riskLevel;
            Score = score;
            Reason = reason;
        }
    }
}
