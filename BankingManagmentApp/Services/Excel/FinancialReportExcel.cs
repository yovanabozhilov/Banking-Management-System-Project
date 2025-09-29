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

            var wsSummary = wb.AddWorksheet("Summary");
            int r = 1;

            wsSummary.Cell(r++, 1).Value = "Financial Report";
            wsSummary.Cell(r++, 1).Value = $"Period: {vm.Filters.From:yyyy-MM-dd} → {vm.Filters.To:yyyy-MM-dd}";
            wsSummary.Cell(r++, 1).Value = $"Group by: {vm.Filters.GroupBy}";
            wsSummary.Cell(r++, 1).Value = $"Account: {vm.Filters.SelectedAccountLabel}";
            if (!string.IsNullOrWhiteSpace(vm.SelectedCustomerName) || !string.IsNullOrWhiteSpace(vm.SelectedCustomerId))
                wsSummary.Cell(r++, 1).Value = $"Client: {vm.SelectedCustomerName} ({vm.SelectedCustomerId})";
            r++;

            var kpiStart = r;
            wsSummary.Cell(r, 1).Value = "Transactions";
            wsSummary.Cell(r++, 2).Value = vm.GrandTotalTransactions;

            wsSummary.Cell(r, 1).Value = "Credits";
            wsSummary.Cell(r++, 2).Value = vm.TotalCredits;

            wsSummary.Cell(r, 1).Value = "Debits";
            wsSummary.Cell(r++, 2).Value = vm.TotalDebits;

            wsSummary.Cell(r, 1).Value = "Net Flow";
            wsSummary.Cell(r++, 2).Value = vm.NetFlow;

            wsSummary.Range(kpiStart, 1, r - 1, 1).Style.Font.SetBold();
            wsSummary.Range(kpiStart, 2, r - 1, 2).Style.NumberFormat.Format = "#,##0.00";

            r += 1;

            if (vm.TotalsByType != null && vm.TotalsByType.Any())
            {
                wsSummary.Cell(r++, 1).Value = "Totals by Transaction Type";
                wsSummary.Cell(r, 1).Value = "Type";
                wsSummary.Cell(r, 2).Value = "Amount";
                wsSummary.Cell(r, 3).Value = "Percent";
                wsSummary.Range(r, 1, r, 3).Style.Font.SetBold();
                r++;

                var total = vm.TotalByTypeAll;
                foreach (var kv in vm.TotalsByType.OrderByDescending(x => x.Value))
                {
                    wsSummary.Cell(r, 1).Value = string.IsNullOrWhiteSpace(kv.Key) ? "Unknown" : kv.Key;
                    wsSummary.Cell(r, 2).Value = kv.Value;
                    wsSummary.Cell(r, 3).Value = total == 0 ? 0 : Math.Round((kv.Value / total) * 100m, 2);
                    r++;
                }

                wsSummary.Range(kpiStart, 2, r - 1, 2).Style.NumberFormat.Format = "#,##0.00";
                wsSummary.Range(kpiStart, 3, r - 1, 3).Style.NumberFormat.Format = "0.00%";
                r += 1;
            }

            if (vm.TopDescriptionsByType != null && vm.TopDescriptionsByType.Any())
            {
                wsSummary.Cell(r++, 1).Value = "Top References by Type";
                wsSummary.Cell(r, 1).Value = "Type";
                wsSummary.Cell(r, 2).Value = "Reference";
                wsSummary.Cell(r, 3).Value = "Amount";
                wsSummary.Range(r, 1, r, 3).Style.Font.SetBold();
                r++;

                foreach (var type in vm.TopDescriptionsByType.Keys.OrderBy(k => k))
                {
                    foreach (var d in vm.TopDescriptionsByType[type].OrderByDescending(x => x.Total))
                    {
                        wsSummary.Cell(r, 1).Value = string.IsNullOrWhiteSpace(type) ? "Unknown" : type;
                        wsSummary.Cell(r, 2).Value = string.IsNullOrWhiteSpace(d.Description) ? "—" : d.Description;
                        wsSummary.Cell(r, 3).Value = d.Total;
                        r++;
                    }
                }
                wsSummary.Range(kpiStart, 3, r - 1, 3).Style.NumberFormat.Format = "#,##0.00";
            }

            wsSummary.Columns().AdjustToContents();


            var ws = wb.AddWorksheet("Results");
            r = 1;

            int c = 1;
            ws.Cell(r, c++).Value = "Year";
            if (vm.Filters.GroupBy == ReportGroupBy.Monthly)
                ws.Cell(r, c++).Value = "Month";
            ws.Cell(r, c++).Value = "Transactions";
            ws.Cell(r, c++).Value = "Total Amount";
            ws.Cell(r, c++).Value = "Net Flow";
            ws.Cell(r, c++).Value = "By type";
            ws.Cell(r, c++).Value = "Client";
            ws.Cell(r, c++).Value = "Client ID";
            ws.Range(r, 1, r, c - 1).Style.Font.SetBold();
            r++;

            foreach (var row in vm.Rows.OrderBy(x => x.Year).ThenBy(x => x.Month ?? 0))
            {
                c = 1;
                ws.Cell(r, c++).Value = row.Year;
                if (vm.Filters.GroupBy == ReportGroupBy.Monthly)
                    ws.Cell(r, c++).Value = row.Month;

                ws.Cell(r, c++).Value = row.TotalTransactions;
                ws.Cell(r, c).Value = row.TotalAmount; ws.Cell(r, c++).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(r, c).Value = row.NetFlow;     ws.Cell(r, c++).Style.NumberFormat.Format = "#,##0.00";

                var byType = string.Join("  •  ",
                    (row.AmountByType ?? new())
                        .OrderBy(x => x.Key)
                        .Select(x => $"{(string.IsNullOrWhiteSpace(x.Key) ? "Unknown" : x.Key)}: {x.Value:N2}"));

                ws.Cell(r, c++).Value = byType;

                var clientLabel = vm.SelectedCustomerName
                    ?? (string.IsNullOrEmpty(vm.Filters.CustomerName) && string.IsNullOrEmpty(vm.Filters.CustomerId)
                        ? "All / Multiple"
                        : (vm.Filters.CustomerName ?? "Filtered"));

                ws.Cell(r, c++).Value = clientLabel;
                ws.Cell(r, c++).Value = vm.SelectedCustomerId ?? (string.IsNullOrEmpty(vm.Filters.CustomerId) ? "—" : vm.Filters.CustomerId);
                r++;
            }

            c = 1;
            ws.Cell(r, c++).Value = "Total";
            if (vm.Filters.GroupBy == ReportGroupBy.Monthly) c++;
            ws.Cell(r, c++).Value = vm.GrandTotalTransactions;
            ws.Cell(r, c).Value = vm.GrandTotalAmount; ws.Cell(r, c++).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(r, c).Value = vm.NetFlow;          ws.Cell(r, c++).Style.NumberFormat.Format = "#,##0.00";
            ws.Range(r, 1, r, c - 1).Style.Font.SetBold();

            ws.Columns().AdjustToContents();


            var ws2 = wb.AddWorksheet("By Type");
            int rr = 1;
            ws2.Cell(rr, 1).Value = "Type";
            ws2.Cell(rr, 2).Value = "Amount";
            ws2.Cell(rr, 3).Value = "Percent";
            ws2.Range(rr, 1, rr, 3).Style.Font.SetBold();
            rr++;

            var totalByType = vm.TotalByTypeAll;
            foreach (var kv in vm.TotalsByType.OrderByDescending(x => x.Value))
            {
                ws2.Cell(rr, 1).Value = string.IsNullOrWhiteSpace(kv.Key) ? "Unknown" : kv.Key;
                ws2.Cell(rr, 2).Value = kv.Value;
                ws2.Cell(rr, 3).Value = totalByType == 0 ? 0 : Math.Round((kv.Value / totalByType) * 100m, 2);
                rr++;
            }
            ws2.Range(2, 2, rr - 1, 2).Style.NumberFormat.Format = "#,##0.00";
            ws2.Range(2, 3, rr - 1, 3).Style.NumberFormat.Format = "0.00%";
            ws2.Columns().AdjustToContents();


            var ws3 = wb.AddWorksheet("References");
            int rx = 1;
            ws3.Cell(rx, 1).Value = "Type";
            ws3.Cell(rx, 2).Value = "Reference";
            ws3.Cell(rx, 3).Value = "Amount";
            ws3.Range(rx, 1, rx, 3).Style.Font.SetBold();
            rx++;

            foreach (var type in vm.TopDescriptionsByType.Keys.OrderBy(k => k))
            {
                foreach (var d in vm.TopDescriptionsByType[type].OrderByDescending(x => x.Total))
                {
                    ws3.Cell(rx, 1).Value = string.IsNullOrWhiteSpace(type) ? "Unknown" : type;
                    ws3.Cell(rx, 2).Value = string.IsNullOrWhiteSpace(d.Description) ? "—" : d.Description;
                    ws3.Cell(rx, 3).Value = d.Total;
                    rx++;
                }
            }
            ws3.Range(2, 3, rx - 1, 3).Style.NumberFormat.Format = "#,##0.00";
            ws3.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }
}
