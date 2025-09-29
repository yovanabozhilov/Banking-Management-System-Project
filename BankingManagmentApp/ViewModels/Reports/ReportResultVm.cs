using System.Collections.Generic;
using System.Linq;

namespace BankingManagmentApp.ViewModels.Reports
{
    public class ReportRow
    {
        public int Year { get; set; }
        public int? Month { get; set; }

        public int TotalTransactions { get; set; }
        public decimal TotalAmount { get; set; }             
        public decimal NetFlow { get; set; }                

        public Dictionary<string, decimal> AmountByType { get; set; } = new();
    }

    public class DescriptionAggVm
    {
        public string Description { get; set; } = string.Empty;
        public decimal Total { get; set; }
    }

    public class ReportResultVm
    {
        public ReportFilterVm Filters { get; set; } = new();
        public List<ReportRow> Rows { get; set; } = new();

        public Dictionary<string, decimal> TotalsByType { get; set; } = new();
        public Dictionary<string, List<DescriptionAggVm>> TopDescriptionsByType { get; set; } = new();

        public string? SelectedCustomerId { get; set; }
        public string? SelectedCustomerName { get; set; }

        public int GrandTotalTransactions => Rows.Sum(r => r.TotalTransactions);
        public decimal GrandTotalAmount => Rows.Sum(r => r.TotalAmount);

        public decimal TotalCredits { get; set; }
        public decimal TotalDebits { get; set; }
        public decimal NetFlow => TotalCredits - TotalDebits;

        public decimal TotalByTypeAll => TotalsByType.Values.Sum();

        public decimal PercentForType(string? type)
        {
            var key = string.IsNullOrWhiteSpace(type) ? "Unknown" : type!;
            var total = TotalByTypeAll;
            if (total <= 0 || !TotalsByType.TryGetValue(key, out var v)) return 0m;
            return System.Math.Round((v / total) * 100m, 2);
        }
    }
}
