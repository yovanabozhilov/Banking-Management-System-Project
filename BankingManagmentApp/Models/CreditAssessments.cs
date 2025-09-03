namespace BankingManagmentApp.Models
{
    public class CreditAssessments
    {
        public int Id { get; set; }
        public int LoanId { get; set; }          
        public int CreditScore { get; set; }
        public int RiskLevel { get; set; }
        public string Notes { get; set; } = string.Empty;
        public Loans Loans { get; set; }
    }

}
