using System;
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

        public Dictionary<string, decimal> AmountByType { get; set; } = new();
    }

    public class ReportResultVm
    {
        public ReportFilterVm Filters { get; set; } = new();
        public List<ReportRow> Rows { get; set; } = new();

        public int GrandTotalTransactions => Rows.Sum(r => r.TotalTransactions);
        public decimal GrandTotalAmount => Rows.Sum(r => r.TotalAmount);
    }
}
