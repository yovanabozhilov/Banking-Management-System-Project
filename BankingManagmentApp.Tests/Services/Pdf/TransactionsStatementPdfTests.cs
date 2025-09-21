using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services.Pdf;
using UglyToad.PdfPig;
using Xunit;
using QuestPDF.Fluent;

namespace BankingManagmentApp.Tests.Services.Pdf
{
    public class TransactionsStatementPdfTests
    {
        private static string ExtractAllText(byte[] pdf)
        {
            using var ms = new MemoryStream(pdf);
            using var doc = PdfDocument.Open(ms);
            var sb = new StringBuilder();
            foreach (var page in doc.GetPages())
                sb.AppendLine(page.Text);
            return sb.ToString();
        }

        // по-устойчива нормализация: маха whitespace, NBSP/zero-width, и разплита лигатури (ﬀ/ﬁ/ﬂ/ﬃ/ﬄ)
        private static string Normalize(string s)
        {
            if (s == null) return string.Empty;

            s = s.Replace("\u00A0", " ")   // NBSP
                 .Replace("\u200B", "")    // zero-width space
                 .Replace("\uFB00", "ff")
                 .Replace("\uFB01", "fi")
                 .Replace("\uFB02", "fl")
                 .Replace("\uFB03", "ffi")
                 .Replace("\uFB04", "ffl");

            return new string(s.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
        }

        private static (Customers user, Accounts acc, List<Transactions> tx, DateOnly from, DateOnly to) SampleData()
        {
            var user = new Customers
            {
                FirstName = "Maria",
                LastName = "Georgieva",
                Email = "maria@example.com"
            };

            var acc = new Accounts
            {
                IBAN = "BG80BNBG96611020345678",
                Currency = "BGN"
            };

            var from = new DateOnly(2025, 01, 01);
            var to   = new DateOnly(2025, 01, 31);

            var tx = new List<Transactions>
            {
                new Transactions
                {
                    Date = new DateOnly(2025, 01, 02),
                    Description = "Salary",
                    TransactionType = "Credit",
                    Amount = 1500m,
                    Accounts = acc,
                    ReferenceNumber = 123
                },
                new Transactions
                {
                    Date = new DateOnly(2025, 01, 10),
                    Description = "POS Payment",
                    TransactionType = "Debit",
                    Amount = 300m,
                    Accounts = acc,
                    ReferenceNumber = 456
                },
                new Transactions
                {
                    Date = new DateOnly(2025, 01, 20),
                    Description = "Refund",
                    TransactionType = "Credit",
                    Amount = 200m,
                    Accounts = acc,
                    ReferenceNumber = 789
                }
            };

            return (user, acc, tx, from, to);
        }

        [Fact]
        public void GeneratePdf_Header_And_Period_ShouldBePresent()
        {
            var (user, acc, tx, from, to) = SampleData();

            var doc = new TransactionsStatementPdf(
                user, acc, tx, from, to, CultureInfo.GetCultureInfo("bg-BG"));

            var pdf = doc.GeneratePdf();
            Assert.NotNull(pdf);
            Assert.True(pdf.Length > 100);

            var text = ExtractAllText(pdf);
            var norm = Normalize(text);

            // Заглавие: позволяваме и ед. число, и разплитаме лигатури (напр. Oﬀicial)
            Assert.True(
                norm.Contains("OfficialTransactionsStatement") ||
                norm.Contains("OfficialTransactionStatement"),
                "Expected header 'Official Transactions Statement' (allowing ligatures and singular form)."
            );

            Assert.Contains("Customer:MariaGeorgieva", norm);
            Assert.Contains("IBAN:BG80BNBG96611020345678", norm);
            Assert.Contains("Currency:BGN", norm);

            // Период: някои рендери слагат '-' вместо '–'
            Assert.True(
                norm.Contains("Period:01.01.2025–31.01.2025") ||
                norm.Contains("Period:01.01.2025-31.01.2025"),
                "Expected period '01.01.2025–31.01.2025' (allowing hyphen or en dash)."
            );
        }

        [Fact]
        public void GeneratePdf_Summary_Totals_AreCorrect_InBgCulture()
        {
            var (user, acc, tx, from, to) = SampleData();

            var ci = CultureInfo.GetCultureInfo("bg-BG");
            var doc = new TransactionsStatementPdf(user, acc, tx, from, to, ci);

            var norm = Normalize(ExtractAllText(doc.GeneratePdf()));

            // Общо транзакции
            Assert.Contains("Totaltransactions:3", norm);

            // Суми: credits=1500+200=1700, debits=300, net=1400 (форматирани с bg-BG "N2")
            string fCredits = (1500m + 200m).ToString("N2", ci); // 1 700,00 => нормализирано: 1700,00
            string fDebits  = (300m).ToString("N2", ci);         // 300,00
            string fNet     = (1700m - 300m).ToString("N2", ci); // 1 400,00

            Assert.Contains(Normalize($"Credits (+): {fCredits}"), norm);
            Assert.Contains(Normalize($"Debits (-): {fDebits}"), norm);
            Assert.Contains(Normalize($"Net: {fNet}"), norm);
        }

        [Fact]
        public void GeneratePdf_Table_Rows_HaveSigns_AndColumns()
        {
            var (user, acc, tx, from, to) = SampleData();

            var ci = CultureInfo.GetCultureInfo("bg-BG");
            var doc = new TransactionsStatementPdf(user, acc, tx, from, to, ci);

            var norm = Normalize(ExtractAllText(doc.GeneratePdf()));

            // Хедъри на таблицата
            Assert.Contains("Date", norm);
            Assert.Contains("Description", norm);
            Assert.Contains("Type", norm);
            Assert.Contains("Amount", norm);
            Assert.Contains("Curr.", norm);
            Assert.Contains("Ref.", norm);
            Assert.Contains("IBAN", norm);

            // Префикси +/-
            Assert.Contains("+1500,00", norm);
            Assert.Contains("-300,00",  norm);
            Assert.Contains("+200,00",  norm);

            // Референции и валута
            Assert.Contains("123", norm);
            Assert.Contains("456", norm);
            Assert.Contains("789", norm);
            Assert.Contains("BGN", norm);
        }
    }
}
