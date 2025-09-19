using System;
using System.Collections.Generic;
using System.IO;
using BankingManagmentApp.Services.Excel;
using BankingManagmentApp.ViewModels.Reports;
using ClosedXML.Excel;
using Xunit;

namespace BankingManagmentApp.Tests.Services.Excel
{
    public class FinancialReportExcelTests
    {
        private ReportResultVm BuildSampleReport()
        {
            var rows = new List<ReportRow>
            {
                new ReportRow
                {
                    Year = 2025,
                    Month = 1,
                    TotalTransactions = 5,
                    TotalAmount = 100m,
                    AmountByType = new Dictionary<string, decimal>
                    {
                        { "Deposit", 60m },
                        { "Withdrawal", 40m }
                    }
                },
                new ReportRow
                {
                    Year = 2025,
                    Month = 2,
                    TotalTransactions = 3,
                    TotalAmount = 200m,
                    AmountByType = new Dictionary<string, decimal>()
                }
            };

            // Only set Rows, the GrandTotals will be computed by ReportResultVm itself
            return new ReportResultVm { Rows = rows };
        }

        [Fact]
        public void Build_ReturnsNonEmptyByteArray()
        {
            var report = BuildSampleReport();

            var bytes = FinancialReportExcel.Build(report);

            Assert.NotNull(bytes);
            Assert.NotEmpty(bytes);
        }

        [Fact]
        public void Build_CreatesSummaryWorksheet_WithCorrectTotals()
        {
            var report = BuildSampleReport();

            var bytes = FinancialReportExcel.Build(report);

            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);

            var ws = wb.Worksheet("Summary");

            Assert.Equal("Period", ws.Cell(1, 1).GetString());
            Assert.Equal("2025-01", ws.Cell(2, 1).GetString());
            Assert.Equal(5, ws.Cell(2, 2).GetValue<int>());
            Assert.Equal(100m, ws.Cell(2, 3).GetValue<decimal>());

            Assert.Equal("TOTAL", ws.Cell(4, 1).GetString());
            Assert.Equal(8, ws.Cell(4, 2).GetValue<int>());   // 5+3
            Assert.Equal(300m, ws.Cell(4, 3).GetValue<decimal>()); // 100+200
        }

        [Fact]
        public void Build_CreatesByTypeWorksheet_WithCorrectData()
        {
            var report = BuildSampleReport();

            var bytes = FinancialReportExcel.Build(report);

            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);

            var ws = wb.Worksheet("ByType");

            // Headers
            Assert.Equal("Period", ws.Cell(1, 1).GetString());
            Assert.Equal("Transaction Type", ws.Cell(1, 2).GetString());
            Assert.Equal("Amount", ws.Cell(1, 3).GetString());

            // Deposit row
            Assert.Equal("2025-01", ws.Cell(2, 1).GetString());
            Assert.Equal("Deposit", ws.Cell(2, 2).GetString());
            Assert.Equal(60m, ws.Cell(2, 3).GetValue<decimal>());

            // Withdrawal row
            Assert.Equal("2025-01", ws.Cell(3, 1).GetString());
            Assert.Equal("Withdrawal", ws.Cell(3, 2).GetString());
            Assert.Equal(40m, ws.Cell(3, 3).GetValue<decimal>());

            // Empty branch → February
            Assert.Equal("2025-02", ws.Cell(4, 1).GetString());
            Assert.Equal("—", ws.Cell(4, 2).GetString());
            Assert.Equal(0m, ws.Cell(4, 3).GetValue<decimal>());
        }
    }
}
