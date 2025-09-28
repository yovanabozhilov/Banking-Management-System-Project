using System;
using System.IO;
using System.Linq;
using BankingManagmentApp.ViewModels.Reports;
using ClosedXML.Excel;

namespace BankingManagmentApp.Services.Excel
{
    public static class FinancialReportExcel
    {
        public static byte[] Build(ReportResultVm vm)
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Report");

            int r = 1;
            ws.Cell(r++, 1).Value = "Financial Report";
            ws.Cell(r++, 1).Value = $"Period: {vm.Filters.From:yyyy-MM-dd} → {vm.Filters.To:yyyy-MM-dd}";
            ws.Cell(r++, 1).Value = $"Group by: {vm.Filters.GroupBy}";
            ws.Cell(r++, 1).Value = $"Account: {vm.Filters.SelectedAccountLabel}";
            if (!string.IsNullOrEmpty(vm.SelectedCustomerName) || !string.IsNullOrEmpty(vm.SelectedCustomerId))
                ws.Cell(r++, 1).Value = $"Customer: {vm.SelectedCustomerName} ({vm.SelectedCustomerId})";
            r++;

            int c = 1;
            ws.Cell(r, c++).Value = "Year";
            if (vm.Filters.GroupBy == ReportGroupBy.Monthly)
                ws.Cell(r, c++).Value = "Month";
            ws.Cell(r, c++).Value = "Transactions";
            ws.Cell(r, c++).Value = "Total Amount";
            ws.Cell(r, c++).Value = "By type";
            ws.Cell(r, c++).Value = "Client";
            ws.Cell(r, c++).Value = "Client ID";
            ws.Range(r, 1, r, c - 1).Style.Font.SetBold();
            r++;

            foreach (var row in vm.Rows)
            {
                c = 1;
                ws.Cell(r, c++).Value = row.Year;
                if (vm.Filters.GroupBy == ReportGroupBy.Monthly)
                    ws.Cell(r, c++).Value = row.Month;
                ws.Cell(r, c++).Value = row.TotalTransactions;
                ws.Cell(r, c++).Value = row.TotalAmount;
                ws.Cell(r, c++).Value = string.Join("  •  ", row.AmountByType.OrderBy(x => x.Key).Select(x => $"{x.Key}: {x.Value:N2}"));
                ws.Cell(r, c++).Value = vm.SelectedCustomerName ?? (string.IsNullOrEmpty(vm.Filters.CustomerName) && string.IsNullOrEmpty(vm.Filters.CustomerId) ? "All / Multiple" : (vm.Filters.CustomerName ?? "Filtered"));
                ws.Cell(r, c++).Value = vm.SelectedCustomerId ?? (string.IsNullOrEmpty(vm.Filters.CustomerId) ? "—" : vm.Filters.CustomerId);
                r++;
            }

            c = 1;
            ws.Cell(r, c++).Value = "Total";
            if (vm.Filters.GroupBy == ReportGroupBy.Monthly) c++;
            ws.Cell(r, c++).Value = vm.GrandTotalTransactions;
            ws.Cell(r, c++).Value = vm.GrandTotalAmount;
            ws.Range(r, 1, r, 7).Style.Font.SetBold();

            var ws2 = wb.AddWorksheet("By Type");
            int rr = 1;
            ws2.Cell(rr, 1).Value = "Type";
            ws2.Cell(rr, 2).Value = "Amount";
            ws2.Cell(rr, 3).Value = "Percent";
            ws2.Range(rr,1,rr,3).Style.Font.SetBold();
            rr++;

            var total = vm.TotalByTypeAll;
            foreach (var kv in vm.TotalsByType.OrderByDescending(x => x.Value))
            {
                ws2.Cell(rr, 1).Value = kv.Key;
                ws2.Cell(rr, 2).Value = kv.Value;
                ws2.Cell(rr, 3).Value = total == 0 ? 0 : Math.Round((kv.Value / total) * 100m, 2);
                rr++;
            }

            var ws3 = wb.AddWorksheet("Descriptions");
            int rx = 1;
            ws3.Cell(rx, 1).Value = "Type";
            ws3.Cell(rx, 2).Value = "Description";
            ws3.Cell(rx, 3).Value = "Amount";
            ws3.Range(rx,1,rx,3).Style.Font.SetBold();
            rx++;

            foreach (var type in vm.TopDescriptionsByType.Keys.OrderBy(k => k))
            {
                foreach (var d in vm.TopDescriptionsByType[type])
                {
                    ws3.Cell(rx, 1).Value = type;
                    ws3.Cell(rx, 2).Value = d.Description;
                    ws3.Cell(rx, 3).Value = d.Total;
                    rx++;
                }
            }

            ws.Columns().AdjustToContents();
            ws2.Columns().AdjustToContents();
            ws3.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }
}
