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

        public DocumentMetadata GetMetadata() => new DocumentMetadata
        {
            Title = "Financial Report",
            Author = "BankingManagementApp",
            Subject = "Aggregated transactions report"
        };

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));  

                page.Header().Column(col =>
                {
                    col.Item().Text("Financial Report").FontSize(14).SemiBold();

                    col.Item().Text(t =>
                    {
                        t.Span("Period: ").SemiBold();
                        t.Span($"{_vm.Filters.From:yyyy-MM-dd} → {_vm.Filters.To:yyyy-MM-dd}");
                    });

                    col.Item().Text(t =>
                    {
                        t.Span("Group by: ").SemiBold();
                        t.Span(_vm.Filters.GroupBy.ToString());
                    });

                    col.Item().Text(t =>
                    {
                        t.Span("Account: ").SemiBold();
                        t.Span(_vm.Filters.SelectedAccountLabel ?? "All accounts");
                    });

                    if (!string.IsNullOrWhiteSpace(_vm.SelectedCustomerName) ||
                        !string.IsNullOrWhiteSpace(_vm.SelectedCustomerId))
                    {
                        col.Item().Text(t =>
                        {
                            t.Span("Client: ").SemiBold();
                            t.Span($"{_vm.SelectedCustomerName} ({_vm.SelectedCustomerId})");
                        });
                    }

                    col.Item().PaddingTop(8).Height(1).Background(Colors.Grey.Lighten2);
                });

                page.Content().PaddingTop(10).Column(col =>
                {
                    if (_vm.TotalsByType != null && _vm.TotalsByType.Any())
                    {
                        col.Item().PaddingBottom(6)
                                  .Text("Totals by Transaction Type").FontSize(12).SemiBold();

                        col.Item().PaddingBottom(10).Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(6); 
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
                            int i = 0;
                            foreach (var kv in _vm.TotalsByType.OrderByDescending(x => x.Value))
                            {
                                var bg = (i++ % 2 == 0) ? Colors.White : Colors.Grey.Lighten5;
                                var type = string.IsNullOrWhiteSpace(kv.Key) ? "Unknown" : kv.Key;
                                var pct = total == 0 ? 0 : Math.Round((kv.Value / total) * 100m, 2);

                                t.Cell().Element(c => CellBody(c, bg)).Text(type);
                                t.Cell().Element(c => CellBody(c, bg)).AlignRight().Text(kv.Value.ToString("N2"));
                                t.Cell().Element(c => CellBody(c, bg)).AlignRight().Text($"{pct:N2}%");
                            }
                        });
                    }

                    if (_vm.TopDescriptionsByType != null && _vm.TopDescriptionsByType.Any())
                    {
                        col.Item().PaddingBottom(6)
                                  .Text("Top References by Type").FontSize(12).SemiBold();

                        col.Item().PaddingBottom(10).Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(3);  
                                c.RelativeColumn(6); 
                                c.RelativeColumn(3);  
                            });

                            t.Header(h =>
                            {
                                h.Cell().Element(CellHeader).Text("Type");
                                h.Cell().Element(CellHeader).Text("Reference");
                                h.Cell().Element(CellHeader).AlignRight().Text("Amount");
                            });

                            int i = 0;
                            foreach (var typeEntry in _vm.TopDescriptionsByType.OrderBy(k => k.Key))
                            {
                                var type = string.IsNullOrWhiteSpace(typeEntry.Key) ? "Unknown" : typeEntry.Key;

                                foreach (var d in typeEntry.Value.OrderByDescending(x => x.Total))
                                {
                                    var bg = (i++ % 2 == 0) ? Colors.White : Colors.Grey.Lighten5;
                                    var desc = string.IsNullOrWhiteSpace(d.Description) ? "—" : d.Description;

                                    t.Cell().Element(c => CellBody(c, bg)).Text(type);
                                    t.Cell().Element(c => CellBody(c, bg)).Text(desc);
                                    t.Cell().Element(c => CellBody(c, bg)).AlignRight().Text(d.Total.ToString("N2"));
                                }
                            }
                        });
                    }

                    col.Item().PaddingBottom(6)
                              .Text("Detailed Results").FontSize(12).SemiBold();

                    col.Item().Table(t =>
                    {
                        var hasMonth = _vm.Filters.GroupBy == ReportGroupBy.Monthly;

                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(1.4f); 
                            if (hasMonth) c.RelativeColumn(1.4f); 
                            c.RelativeColumn(1.6f);
                            c.RelativeColumn(2.0f); 
                            c.RelativeColumn(2.0f); 
                            c.RelativeColumn(4.0f); 
                            c.RelativeColumn(2.5f); 
                            c.RelativeColumn(3.0f); 
                        });

                        t.Header(h =>
                        {
                            h.Cell().Element(CellHeader).Text("Year");
                            if (hasMonth) h.Cell().Element(CellHeader).Text("Month");
                            h.Cell().Element(CellHeader).AlignRight().Text("Transactions");
                            h.Cell().Element(CellHeader).AlignRight().Text("Total Amount");
                            h.Cell().Element(CellHeader).AlignRight().Text("Net Flow");
                            h.Cell().Element(CellHeader).Text("By Type");
                            h.Cell().Element(CellHeader).Text("Client");
                            h.Cell().Element(CellHeader).Text("Client ID");
                        });

                        int i = 0;
                        foreach (var r in _vm.Rows.OrderBy(x => x.Year).ThenBy(x => x.Month ?? 0))
                        {
                            var bg = (i++ % 2 == 0) ? Colors.White : Colors.Grey.Lighten5;

                            var custLabel = _vm.SelectedCustomerName
                                ?? (string.IsNullOrEmpty(_vm.Filters.CustomerName) && string.IsNullOrEmpty(_vm.Filters.CustomerId)
                                    ? "All / Multiple"
                                    : (_vm.Filters.CustomerName ?? "Filtered"));

                            var custId = _vm.SelectedCustomerId
                                ?? (string.IsNullOrEmpty(_vm.Filters.CustomerId) ? "—" : _vm.Filters.CustomerId);

                            t.Cell().Element(c => CellBody(c, bg)).Text(r.Year.ToString());
                            if (hasMonth) t.Cell().Element(c => CellBody(c, bg)).Text(r.Month?.ToString() ?? "");
                            t.Cell().Element(c => CellBody(c, bg)).AlignRight().Text(r.TotalTransactions.ToString("N0"));
                            t.Cell().Element(c => CellBody(c, bg)).AlignRight().Text(r.TotalAmount.ToString("N2"));
                            t.Cell().Element(c => CellBody(c, bg)).AlignRight().Text(r.NetFlow.ToString("N2"));

                            var byType = (r.AmountByType ?? new())
                                .OrderBy(x => x.Key)
                                .Select(x => $"{(string.IsNullOrWhiteSpace(x.Key) ? "Unknown" : x.Key)}: {x.Value:N2}");
                            t.Cell().Element(c => CellBody(c, bg)).Text(string.Join("  •  ", byType));

                            t.Cell().Element(c => CellBody(c, bg)).Text(custLabel);
                            t.Cell().Element(c => CellBody(c, bg)).Text(custId);
                        }

                        t.Footer(f =>
                        {
                            f.Cell().Element(CellHeader).Text("Total");
                            if (hasMonth) f.Cell().Element(CellHeader).Text("");
                            f.Cell().Element(CellHeader).AlignRight().Text(_vm.GrandTotalTransactions.ToString("N0"));
                            f.Cell().Element(CellHeader).AlignRight().Text(_vm.GrandTotalAmount.ToString("N2"));
                            f.Cell().Element(CellHeader).AlignRight().Text(_vm.NetFlow.ToString("N2"));
                            f.Cell().Element(CellHeader).Text("");
                            f.Cell().Element(CellHeader).Text("");
                            f.Cell().Element(CellHeader).Text("");
                        });
                    });
                });

                page.Footer().Row(r =>
                {
                    r.RelativeItem().Text($"Generated {DateTime.Now:yyyy-MM-dd HH:mm}")
                        .FontSize(9).FontColor(Colors.Grey.Darken2);
                    r.ConstantItem(120).AlignRight().Text(x =>
                    {
                        x.Span("Page ").FontSize(9).FontColor(Colors.Grey.Darken2);
                        x.CurrentPageNumber().FontSize(9).FontColor(Colors.Grey.Darken2);
                        x.Span(" / ").FontSize(9).FontColor(Colors.Grey.Darken2);
                        x.TotalPages().FontSize(9).FontColor(Colors.Grey.Darken2);
                    });
                });
            });
        }

        static IContainer CellHeader(IContainer c) =>
            c.DefaultTextStyle(x => x.SemiBold())
             .Background(Colors.Grey.Lighten3)
             .Padding(4);

        static IContainer CellBody(IContainer c, string bg) =>
            c.Background(bg)
             .PaddingVertical(2)
             .PaddingHorizontal(3);
    }
}
