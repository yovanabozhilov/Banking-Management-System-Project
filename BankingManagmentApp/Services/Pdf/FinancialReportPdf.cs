using BankingManagmentApp.ViewModels.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Linq;

namespace BankingManagmentApp.Services.Pdf
{
    public class FinancialReportPdf : IDocument
    {
        private readonly ReportResultVm _vm;
        public FinancialReportPdf(ReportResultVm vm) => _vm = vm;

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Margin(30);

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().Text("Financial Report").SemiBold().FontSize(18);

                        col.Item().Text(txt =>
                        {
                            txt.Span("Period: ").SemiBold();
                            txt.Span($"{_vm.Filters.From:yyyy-MM-dd} → {_vm.Filters.To:yyyy-MM-dd}");
                        });

                        col.Item().Text($"Group by: {_vm.Filters.GroupBy}");
                        col.Item().Text($"Account: {_vm.Filters.SelectedAccountLabel}");

                        if (!string.IsNullOrEmpty(_vm.SelectedCustomerName) || !string.IsNullOrEmpty(_vm.SelectedCustomerId))
                            col.Item().Text($"Client: {_vm.SelectedCustomerName} ({_vm.SelectedCustomerId})");
                    });
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    if (_vm.TotalsByType.Any())
                    {
                        col.Item().Element(e => e.PaddingBottom(4))
                                  .Text("Totals by Type").SemiBold().FontSize(14);

                        col.Item().Element(e => e.PaddingBottom(10)).Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(4);
                                c.RelativeColumn(3);
                                c.RelativeColumn(3);
                            });

                            t.Header(h =>
                            {
                                h.Cell().Element(CellHeader).Text("Type");
                                h.Cell().Element(CellHeader).AlignRight().Text("Amount");
                                h.Cell().Element(CellHeader).AlignRight().Text("Percent");
                            });

                            var total = _vm.TotalByTypeAll;
                            foreach (var kv in _vm.TotalsByType.OrderByDescending(x => x.Value))
                            {
                                var pct = total == 0 ? 0 : Math.Round((kv.Value / total) * 100m, 2);
                                t.Cell().Text(kv.Key);
                                t.Cell().AlignRight().Text(kv.Value.ToString("N2"));
                                t.Cell().AlignRight().Text($"{pct}%");
                            }
                        });
                    }

                    col.Item().Element(e => e.PaddingTop(6).PaddingBottom(4))
                              .Text("Results").SemiBold().FontSize(14);

                    col.Item().Table(t =>
                    {
                        var hasMonth = _vm.Filters.GroupBy == ReportGroupBy.Monthly;

                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); 
                            if (hasMonth) c.RelativeColumn(2); 
                            c.RelativeColumn(2); 
                            c.RelativeColumn(3); 
                            c.RelativeColumn(5); 
                            c.RelativeColumn(4); 
                            c.RelativeColumn(5); 
                        });

                        t.Header(h =>
                        {
                            h.Cell().Element(CellHeader).Text("Year");
                            if (hasMonth) h.Cell().Element(CellHeader).Text("Month");
                            h.Cell().Element(CellHeader).AlignRight().Text("Transactions");
                            h.Cell().Element(CellHeader).AlignRight().Text("Total Amount");
                            h.Cell().Element(CellHeader).Text("By type");
                            h.Cell().Element(CellHeader).Text("Client");
                            h.Cell().Element(CellHeader).Text("Client ID");
                        });

                        foreach (var r in _vm.Rows)
                        {
                            var custLabel = _vm.SelectedCustomerName
                                ?? (string.IsNullOrEmpty(_vm.Filters.CustomerName) && string.IsNullOrEmpty(_vm.Filters.CustomerId)
                                    ? "All / Multiple"
                                    : (_vm.Filters.CustomerName ?? "Filtered"));

                            var custId = _vm.SelectedCustomerId ?? (string.IsNullOrEmpty(_vm.Filters.CustomerId) ? "—" : _vm.Filters.CustomerId);

                            t.Cell().Text(r.Year.ToString());
                            if (hasMonth) t.Cell().Text(r.Month?.ToString() ?? "");
                            t.Cell().AlignRight().Text(r.TotalTransactions.ToString("N0"));
                            t.Cell().AlignRight().Text(r.TotalAmount.ToString("N2"));
                            t.Cell().Text(string.Join("  •  ", r.AmountByType.OrderBy(x => x.Key).Select(x => $"{x.Key}: {x.Value:N2}")));
                            t.Cell().Text(custLabel);
                            t.Cell().Text(custId);
                        }

                        t.Footer(f =>
                        {
                            f.Cell().Element(CellHeader).Text("Total");
                            if (hasMonth) f.Cell().Element(CellHeader).Text("");
                            f.Cell().Element(CellHeader).AlignRight().Text(_vm.GrandTotalTransactions.ToString("N0"));
                            f.Cell().Element(CellHeader).AlignRight().Text(_vm.GrandTotalAmount.ToString("N2"));
                            f.Cell().Element(CellHeader).Text("");
                            f.Cell().Element(CellHeader).Text("");
                            f.Cell().Element(CellHeader).Text("");
                        });
                    });
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("Generated ").SemiBold();
                    x.Span($"{DateTime.Now:yyyy-MM-dd HH:mm}");
                });
            });
        }

        static IContainer CellHeader(IContainer c)
            => c.DefaultTextStyle(x => x.SemiBold())
                .Background(Colors.Grey.Lighten3)
                .Padding(4);
    }
}
