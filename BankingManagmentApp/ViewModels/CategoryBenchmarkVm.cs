namespace BankingManagmentApp.ViewModels
{
    public class CategoryBenchmarkVm
    {
        public string Category { get; set; } = string.Empty;

        // totals from your DB
        public decimal Income { get; set; }
        public decimal Expense { get; set; }

        // fake industry benchmarks (provided by server code)
        public decimal IndustryAvgIncome { get; set; }
        public decimal IndustryAvgExpense { get; set; }

        // convenience computed properties (safe with zero)
        public decimal IncomeVariancePercent
            => IndustryAvgIncome == 0 ? 0 : Math.Round((Income - IndustryAvgIncome) / IndustryAvgIncome * 100m, 2);

        public decimal ExpenseVariancePercent
            => IndustryAvgExpense == 0 ? 0 : Math.Round((Expense - IndustryAvgExpense) / IndustryAvgExpense * 100m, 2);
    }
}
