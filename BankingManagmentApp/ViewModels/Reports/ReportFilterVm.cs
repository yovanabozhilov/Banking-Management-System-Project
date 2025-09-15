using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BankingManagmentApp.ViewModels.Reports
{
    public enum ReportGroupBy
    {
        Monthly = 0,
        Yearly = 1
    }

    public class ReportFilterVm
    {
        public DateOnly? From { get; set; }
        public DateOnly? To { get; set; }
        public int? AccountId { get; set; }
        public ReportGroupBy GroupBy { get; set; } = ReportGroupBy.Monthly;
        public IEnumerable<SelectListItem>? AccountsSelect { get; set; }
        public string SelectedAccountLabel { get; set; } = "All accounts";
    }
}
