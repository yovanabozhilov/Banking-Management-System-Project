namespace BankingManagmentApp.ViewModels
{
    public class CategoryBenchmarkVm
    {
        public string Category { get; set; } = string.Empty;
        public decimal Income { get; set; }
        public decimal Expense { get; set; }
        public decimal IndustryAvgIncome { get; set; }
        public decimal IndustryAvgExpense { get; set; }
        public decimal IncomeVariancePercent
            => IndustryAvgIncome == 0 ? 0 : Math.Round((Income - IndustryAvgIncome) / IndustryAvgIncome * 100m, 2);

        public decimal ExpenseVariancePercent
            => IndustryAvgExpense == 0 ? 0 : Math.Round((Expense - IndustryAvgExpense) / IndustryAvgExpense * 100m, 2);
    }
}
