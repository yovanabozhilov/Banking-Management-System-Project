using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BankingManagmentApp.Services.Pdf;
using BankingManagmentApp.ViewModels.Reports;
using FluentAssertions;
using QuestPDF.Fluent;
using UglyToad.PdfPig;
using Xunit;

namespace BankingManagmentApp.Tests.Reporting.Pdf
{
    public class FinancialReportPdfTests
    {
        private static string Normalize(string s) =>
            new string(s.Where(ch => !char.IsWhiteSpace(ch)).ToArray());

        private static ReportResultVm SampleMonthlyVm() => new()
        {
            Filters = new ReportFilterVm
            {
                From = new DateOnly(2025, 1, 1),
                To = new DateOnly(2025, 1, 31),
                SelectedAccountLabel = "ACC-123",
                GroupBy = ReportGroupBy.Monthly
            },
            Rows = new List<ReportRow>
            {
                new ReportRow
                {
                    Year = 2025,
                    Month = 1,
                    TotalTransactions = 3,
                    TotalAmount = 1234.56m,
                    AmountByType = new Dictionary<string, decimal>
                    {
                        ["Deposit"] = 1000.50m,
                        ["Withdraw"] = 234.06m
                    }
                }
            }
        };

        private static ReportResultVm SampleYearlyVm() => new()
        {
            Filters = new ReportFilterVm
            {
                From = new DateOnly(2025, 1, 1),
                To = new DateOnly(2025, 12, 31),
                SelectedAccountLabel = "ACC-123",
                GroupBy = ReportGroupBy.Yearly
            },
            Rows = new List<ReportRow>
            {
                new ReportRow
                {
                    Year = 2025,
                    Month = null,
                    TotalTransactions = 10,
                    TotalAmount = 2500.00m,
                    AmountByType = new Dictionary<string, decimal>
                    {
                        ["Deposit"] = 2000.00m,
                        ["Withdraw"] = 500.00m
                    }
                }
            }
        };

        private static string ExtractAllText(byte[] pdfBytes)
        {
            using var ms = new MemoryStream(pdfBytes);
            using var doc = PdfDocument.Open(ms);
            var sb = new StringBuilder();
            foreach (var page in doc.GetPages())
                sb.AppendLine(page.Text);
            return sb.ToString();
        }

        [Fact]
        public void GeneratePdf_Monthly_ShouldContainHeader_And_TableData()
        {
            var vm = SampleMonthlyVm();
            var doc = new FinancialReportPdf(vm);

            var pdfBytes = doc.GeneratePdf();
            pdfBytes.Should().NotBeNullOrEmpty();

            var text = ExtractAllText(pdfBytes);
            var norm = Normalize(text);

            norm.Should().Contain("GlowPayFinancialReport");
            norm.Should().Contain("Period:2025-01-01–2025-01-31");
            norm.Should().Contain("Account:ACC-123");
            norm.Should().Contain("Groupby:Monthly");

            // Таблични хедъри (нормализирани)
            norm.Should().Contain("Year");
            norm.Should().Contain("Month");
            norm.Should().Contain("Totaltransactions");
            norm.Should().Contain("Totalamount");
            norm.Should().Contain("Bytype");

            // Данни
            norm.Should().Contain("2025");
            norm.Should().Contain("1");
            norm.Should().Contain("3");
            norm.Should().Contain("1234.56");

            // По тип (PDF екстрактът често маха интервали)
            norm.Should().Contain("Deposit:1000.50");
            norm.Should().Contain("Withdraw:234.06");
        }

        [Fact]
        public void GeneratePdf_Yearly_ShouldOmit_Month_Column()
        {
            var vm = SampleYearlyVm();
            var doc = new FinancialReportPdf(vm);

            var pdfBytes = doc.GeneratePdf();
            var norm = Normalize(ExtractAllText(pdfBytes));

            norm.Should().Contain("Groupby:Yearly");
            norm.Should().Contain("Year");
            norm.Should().NotContain("Month");
        }

        [Fact]
        public void GeneratePdf_ShouldUseInvariantCulture_ForAmounts()
        {
            var vm = SampleMonthlyVm();
            var doc = new FinancialReportPdf(vm);

            var norm = Normalize(ExtractAllText(doc.GeneratePdf()));

            norm.Should().Contain("1000.50");
            norm.Should().Contain("234.06");
            norm.Should().Contain("1234.56");
            norm.Should().NotContain("1000,50");
            norm.Should().NotContain("234,06");
        }
    }
}
