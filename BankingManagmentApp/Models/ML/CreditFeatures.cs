namespace BankingManagmentApp.Models.ML
{
    // Мапва 1:1 към dbo.vw_CreditFeatures
    public class CreditFeatures
    {
        public string  UserId            { get; set; } = string.Empty;
        public decimal TotalBalance      { get; set; }
        public int     NumAccounts       { get; set; }
        public int     NumLoans          { get; set; }
        public double? LoanAgeDaysAvg    { get; set; }
        public double? OnTimeRatio       { get; set; }
        public double? OverdueRatio      { get; set; }
        public decimal AvgMonthlyInflow  { get; set; }
        public decimal AvgMonthlyOutflow { get; set; }
    }
}
