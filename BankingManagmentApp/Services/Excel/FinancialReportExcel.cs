using System;
using System.Linq;
using System.IO;
using ClosedXML.Excel;
using BankingManagmentApp.ViewModels.Reports;

namespace BankingManagmentApp.Services.Excel
{
    public static class FinancialReportExcel
    {
        public static byte[] Build(ReportResultVm result)
        {
            using var wb = new XLWorkbook();

            var summary = wb.Worksheets.Add("Summary");
            summary.Cell(1, 1).Value = "Period";
            summary.Cell(1, 2).Value = "Transactions";
            summary.Cell(1, 3).Value = "Total Amount";

            int row = 2;
            foreach (var r in result.Rows.OrderBy(x => x.Year).ThenBy(x => x.Month ?? 0))
            {
                var period = r.Month.HasValue ? $"{r.Year}-{r.Month.Value:00}" : r.Year.ToString();
                summary.Cell(row, 1).Value = period;
                summary.Cell(row, 2).Value = r.TotalTransactions;
                summary.Cell(row, 3).Value = r.TotalAmount;
                row++;
            }
            summary.Cell(row, 1).Value = "TOTAL";
            summary.Cell(row, 2).Value = result.GrandTotalTransactions;
            summary.Cell(row, 3).Value = result.GrandTotalAmount;
            summary.Columns().AdjustToContents();

            var byType = wb.Worksheets.Add("ByType");
            byType.Cell(1, 1).Value = "Period";
            byType.Cell(1, 2).Value = "Transaction Type";
            byType.Cell(1, 3).Value = "Amount";
            int r2 = 2;
            foreach (var rr in result.Rows.OrderBy(x => x.Year).ThenBy(x => x.Month ?? 0))
            {
                var period = rr.Month.HasValue ? $"{rr.Year}-{rr.Month.Value:00}" : rr.Year.ToString();
                if (rr.AmountByType.Count == 0)
                {
                    byType.Cell(r2, 1).Value = period;
                    byType.Cell(r2, 2).Value = "—";
                    byType.Cell(r2, 3).Value = 0m;
                    r2++;
                }
                else
                {
                    foreach (var kv in rr.AmountByType.OrderBy(k => k.Key))
                    {
                        byType.Cell(r2, 1).Value = period;
                        byType.Cell(r2, 2).Value = kv.Key ?? "Unknown";
                        byType.Cell(r2, 3).Value = kv.Value;
                        r2++;
                    }
                }
            }
            byType.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }
    }
}
