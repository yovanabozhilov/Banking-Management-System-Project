namespace BankingManagmentApp.Configuration
{
    public class CreditScoringOptions
    {
        public int  LookbackDays     { get; set; } = 90; 
        public int  MinTransactions  { get; set; } = 3;
        public int  MinActiveMonths  { get; set; } = 1;  
        public bool RequireBothFlows { get; set; } = true;
    }
}
