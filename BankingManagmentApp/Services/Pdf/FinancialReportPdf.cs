using System;
using System.Globalization;
using System.Linq;
using BankingManagmentApp.ViewModels.Reports;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace BankingManagmentApp.Services.Pdf
{
    public class FinancialReportPdf : IDocument
    {
        private readonly ReportResultVm _vm;

        public FinancialReportPdf(ReportResultVm vm)
        {
            _vm = vm;
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
        public DocumentSettings GetSettings() => DocumentSettings.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Margin(30);

                page.Header().Column(col =>
                {
                    col.Spacing(4);

                    col.Item()
                        .AlignCenter()
                        .Text("GlowPay Financial Report")
                        .SemiBold().FontSize(18);

                    col.Item().Height(14);
                });

                page.Content().Element(ComposeContent);

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(s => s.FontSize(9));
                    t.Span("Generated ");
                    t.Span(DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                    t.Span("   •   Page ");
                    t.CurrentPageNumber();
                    t.Span(" of ");
                    t.TotalPages();
                });
            });
        }

        private void ComposeContent(IContainer container)
        {
            container.Column(col =>
            {
                col.Spacing(6);

                col.Item().Text($"Period: {_vm.Filters.From:yyyy-MM-dd} – {_vm.Filters.To:yyyy-MM-dd}");
                col.Item().Text($"Account: {_vm.Filters.SelectedAccountLabel}");
                col.Item().Text($"Group by: {_vm.Filters.GroupBy}");

                col.Item().Height(12);

                col.Item().Table(table =>
                {
                    bool monthly = _vm.Filters.GroupBy == ReportGroupBy.Monthly;

                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1);
                        if (monthly) columns.RelativeColumn(1);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(4);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(CellHeader).Text("Year");
                        if (monthly) header.Cell().Element(CellHeader).Text("Month");
                        header.Cell().Element(CellHeader).Text("Total transactions");
                        header.Cell().Element(CellHeader).Text("Total amount");
                        header.Cell().Element(CellHeader).Text("By type");
                    });

                    foreach (var r in _vm.Rows)
                    {
                        table.Cell().Element(Cell).Text(r.Year.ToString());
                        if (monthly) table.Cell().Element(Cell).Text(r.Month?.ToString() ?? "-");
                        table.Cell().Element(Cell).Text(r.TotalTransactions.ToString());
                        table.Cell().Element(Cell).Text(r.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture));
                        table.Cell().Element(Cell).Text(FormatTypes(r));
                    }
                });
            });
        }

        private static string FormatTypes(ReportRow r)
        {
            if (r.AmountByType == null || r.AmountByType.Count == 0) return "-";
            return string.Join(", ", r.AmountByType.Select(kv =>
                $"{kv.Key}: {kv.Value.ToString("0.00", CultureInfo.InvariantCulture)}"));
        }

        private static IContainer Cell(IContainer container) =>
            container.PaddingVertical(5).BorderBottom(1);

        private static IContainer CellHeader(IContainer container) =>
            container.PaddingVertical(5).BorderBottom(1).DefaultTextStyle(x => x.SemiBold());
    }
}
