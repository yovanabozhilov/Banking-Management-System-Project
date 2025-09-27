using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BankingManagmentApp.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace BankingManagmentApp.Services.Pdf
{
    public class TransactionsStatementPdf : IDocument
    {
        private readonly Customers _user;
        private readonly Accounts? _account; 
        private readonly IReadOnlyList<Transactions> _items;
        private readonly DateOnly? _from, _to;
        private readonly CultureInfo _ci;

        public TransactionsStatementPdf(Customers user, Accounts? account,
                                        IReadOnlyList<Transactions> items,
                                        DateOnly? from, DateOnly? to,
                                        CultureInfo? ci = null)
        {
            _user = user;
            _account = account;
            _items = items;
            _from = from;
            _to = to;
            _ci = ci ?? CultureInfo.GetCultureInfo("bg-BG");
        }

        public DocumentMetadata GetMetadata() => new()
        {
            Title = "Account Transactions Statement",
            Author = "Banking Management System",
            Subject = "Official transactions statement",
            Keywords = "banking, statement, transactions, account"
        };

        public void Compose(IDocumentContainer container)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));
                page.Header().Element(Header);
                page.Content().Element(Content);
                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Page ").FontSize(9);
                    x.CurrentPageNumber().FontSize(9);
                    x.Span(" / ").FontSize(9);
                    x.TotalPages().FontSize(9);
                });
            });
        }

        void Header(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Banking Management System")
                                .SemiBold().FontSize(16);
                        c.Item().Text("Official Transactions Statement")
                                .FontSize(12).FontColor(Colors.Grey.Darken2);
                    });

                    row.ConstantItem(220).Column(c =>
                    {
                        c.Item().Text(txt =>
                        {
                            txt.Span("Date: ").SemiBold();
                            txt.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm", _ci));
                        });
                        c.Item().Text(txt =>
                        {
                            txt.Span("Customer: ").SemiBold();
                            txt.Span($"{_user.FirstName} {_user.LastName}");
                        });
                        if (_account is not null)
                        {
                            c.Item().Text(txt =>
                            {
                                txt.Span("IBAN: ").SemiBold();
                                txt.Span(_account.IBAN);
                            });
                            c.Item().Text(txt =>
                            {
                                txt.Span("Currency: ").SemiBold();
                                txt.Span(_account.Currency);
                            });
                        }
                        if (_from.HasValue || _to.HasValue)
                        {
                            c.Item().Text(txt =>
                            {
                                txt.Span("Period: ").SemiBold();
                                var f = _from?.ToString("dd.MM.yyyy", _ci) ?? "начало";
                                var t = _to?.ToString("dd.MM.yyyy", _ci) ?? "край";
                                txt.Span($"{f} – {t}");
                            });
                        }
                    });
                });
                col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            });
        }

        void Content(IContainer container)
        {
            container.PaddingTop(10).Column(col =>
            {
                col.Item().Row(r =>
                {
                    var credits = _items
                        .Where(i => !string.Equals(i.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase))
                        .Sum(i => i.Amount);
                    var debits = _items
                        .Where(i => string.Equals(i.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase))
                        .Sum(i => i.Amount);

                    r.RelativeItem().Background(Colors.Grey.Lighten4).Padding(6).Text(t =>
                    {
                        t.Span("Total transactions: ").SemiBold();
                        t.Span(_items.Count.ToString(_ci));
                    });
                    r.RelativeItem().Background(Colors.Grey.Lighten4).Padding(6).Text(t =>
                    {
                        t.Span("Credits (+): ").SemiBold();
                        t.Span(credits.ToString("N2", _ci));
                    });
                    r.RelativeItem().Background(Colors.Grey.Lighten4).Padding(6).Text(t =>
                    {
                        t.Span("Debits (-): ").SemiBold();
                        t.Span(debits.ToString("N2", _ci));
                    });
                    r.RelativeItem().Background(Colors.Grey.Lighten4).Padding(6).Text(t =>
                    {
                        t.Span("Net: ").SemiBold();
                        t.Span((credits - debits).ToString("N2", _ci));
                    });
                });

                col.Item().PaddingTop(8).Element(TransactionsTable);

                col.Item().PaddingTop(8).Text(txt =>
                {
                    txt.Span("Generated for: ").SemiBold().FontSize(9).FontColor(Colors.Grey.Darken1);
                    txt.Span($" {_user.FirstName} {_user.LastName} · {_user.Email}").FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                col.Item().PaddingTop(2).Text(t =>
                {
                    t.Span("This statement is generated electronically and is valid without signature.")
                     .FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        }

        void TransactionsTable(IContainer container)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(70);   // Date
                    cols.RelativeColumn(2);    // Description
                    cols.ConstantColumn(70);   // Type
                    cols.ConstantColumn(90);   // Amount
                    cols.ConstantColumn(65);   // Currency
                    cols.ConstantColumn(80);   // Ref
                    cols.RelativeColumn();     // IBAN
                });

                table.Header(h =>
                {
                    h.Cell().Element(Th).Text(t => t.Span("Date").SemiBold());
                    h.Cell().Element(Th).Text(t => t.Span("Description").SemiBold());
                    h.Cell().Element(Th).Text(t => t.Span("Type").SemiBold());
                    h.Cell().Element(Th).AlignRight().Text(t => t.Span("Amount").SemiBold());
                    h.Cell().Element(Th).Text(t => t.Span("Curr.").SemiBold());
                    h.Cell().Element(Th).Text(t => t.Span("Ref.").SemiBold());
                    h.Cell().Element(Th).Text(t => t.Span("IBAN").SemiBold());
                });

                static IContainer Th(IContainer c) => c
                    .PaddingVertical(6)
                    .Background(Colors.Grey.Lighten3)
                    .BorderBottom(1)
                    .BorderColor(Colors.Grey.Lighten1);

                var zebra = false;
                foreach (var t in _items)
                {
                    zebra = !zebra;
                    var isDebit = string.Equals(t.TransactionType, "Debit", StringComparison.OrdinalIgnoreCase);
                    var bg = zebra ? Colors.Grey.Lighten5 : Colors.White;
                    var currency = t.Accounts?.Currency ?? "";
                    var iban = t.Accounts?.IBAN ?? "";

                    table.Cell().Background(bg).PaddingVertical(4).Text(t.Date.ToString("dd.MM.yyyy", _ci));
                    table.Cell().Background(bg).PaddingVertical(4).Text(t.Description ?? "");
                    table.Cell().Background(bg).PaddingVertical(4).Text(t.TransactionType);
                    table.Cell().Background(bg).PaddingVertical(4).AlignRight()
                         .Text((isDebit ? "-" : "+") + t.Amount.ToString("N2", _ci));
                    table.Cell().Background(bg).PaddingVertical(4).Text(currency);
                    table.Cell().Background(bg).PaddingVertical(4).Text(t.ReferenceNumber.ToString(CultureInfo.InvariantCulture));
                    table.Cell().Background(bg).PaddingVertical(4).Text(iban);
                }
            });
        }
    }
}
