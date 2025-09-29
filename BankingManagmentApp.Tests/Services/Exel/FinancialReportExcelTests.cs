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

            return new ReportResultVm
            {
                Filters = new ReportFilterVm
                {
                    From = new DateOnly(2025, 1, 1),
                    To = new DateOnly(2025, 2, 28),
                    GroupBy = ReportGroupBy.Monthly,
                    SelectedAccountLabel = "ACC-123"
                },
                Rows = rows,
                TotalsByType = new Dictionary<string, decimal>
                {
                    { "Deposit", 60m },
                    { "Withdrawal", 40m }
                }
            };
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
        public void Build_CreatesSummaryWorksheet_WithHeaderAndKpis()
        {
            var report = BuildSampleReport();

            var bytes = FinancialReportExcel.Build(report);

            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);

            var ws = wb.Worksheet("Summary");

            Assert.Equal("Financial Report", ws.Cell(1, 1).GetString());

            var foundTransactions = false;
            var foundCredits = false;
            var foundDebits = false;
            var foundNetFlow = false;

            foreach (var cell in ws.RangeUsed().Cells())
            {
                var s = cell.GetString();
                if (s == "Transactions") foundTransactions = true;
                if (s == "Credits")      foundCredits = true;
                if (s == "Debits")       foundDebits = true;
                if (s == "Net Flow")     foundNetFlow = true;
            }

            Assert.True(foundTransactions, "Missing 'Transactions' KPI label");
            Assert.True(foundCredits, "Missing 'Credits' KPI label");
            Assert.True(foundDebits, "Missing 'Debits' KPI label");
            Assert.True(foundNetFlow, "Missing 'Net Flow' KPI label");
        }

        [Fact]
        public void Build_CreatesResultsWorksheet_WithRowsAndTotals()
        {
            var report = BuildSampleReport();

            var bytes = FinancialReportExcel.Build(report);

            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);

            var ws = wb.Worksheet("Results");

            Assert.Equal("Year", ws.Cell(1, 1).GetString());
            Assert.Equal("Month", ws.Cell(1, 2).GetString()); 
            Assert.Equal("Transactions", ws.Cell(1, 3).GetString());
            Assert.Equal("Total Amount", ws.Cell(1, 4).GetString());
            Assert.Equal("Net Flow", ws.Cell(1, 5).GetString());
            Assert.Equal("By type", ws.Cell(1, 6).GetString());

            Assert.Equal(2025, ws.Cell(2, 1).GetValue<int>());
            Assert.Equal(1, ws.Cell(2, 2).GetValue<int>());
            Assert.Equal(5, ws.Cell(2, 3).GetValue<int>());
            Assert.Equal(100m, ws.Cell(2, 4).GetValue<decimal>());

            Assert.Equal(2025, ws.Cell(3, 1).GetValue<int>());
            Assert.Equal(2, ws.Cell(3, 2).GetValue<int>());
            Assert.Equal(3, ws.Cell(3, 3).GetValue<int>());
            Assert.Equal(200m, ws.Cell(3, 4).GetValue<decimal>());

            Assert.Equal("Total", ws.Cell(4, 1).GetString());
            Assert.Equal(8, ws.Cell(4, 3).GetValue<int>());        
            Assert.Equal(300m, ws.Cell(4, 4).GetValue<decimal>());  
        }

        [Fact]
        public void Build_CreatesByTypeWorksheet_WithCorrectData()
        {
            var report = BuildSampleReport();

            var bytes = FinancialReportExcel.Build(report);

            using var ms = new MemoryStream(bytes);
            using var wb = new XLWorkbook(ms);

            var ws = wb.Worksheet("By Type");

            Assert.Equal("Type", ws.Cell(1, 1).GetString());
            Assert.Equal("Amount", ws.Cell(1, 2).GetString());
            Assert.Equal("Percent", ws.Cell(1, 3).GetString());

            Assert.Equal("Deposit", ws.Cell(2, 1).GetString());
            Assert.Equal(60m, ws.Cell(2, 2).GetValue<decimal>());

            Assert.Equal("Withdrawal", ws.Cell(3, 1).GetString());
            Assert.Equal(40m, ws.Cell(3, 2).GetValue<decimal>());

        }
    }
}
