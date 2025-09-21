using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BankingManagmentApp.Models;
using UglyToad.PdfPig;
using Xunit;

namespace BankingManagmentApp.Tests.Services.Pdf
{
    public class LoanContractGeneratorTests
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

        private static string Normalize(string s) =>
            new string(s.Where(ch => !char.IsWhiteSpace(ch)).ToArray());

        /// <summary>
        /// Safe setter for date-like properties that may be DateOnly/DateTime (nullable or not).
        /// </summary>
        private static void SetDateProperty(object obj, string propertyName, int year, int month, int day)
        {
            var prop = obj.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                       ?? throw new InvalidOperationException($"Property '{propertyName}' not found on {obj.GetType().Name}.");

            var t = prop.PropertyType;
            var isNullable = Nullable.GetUnderlyingType(t) != null;
            var coreType = Nullable.GetUnderlyingType(t) ?? t;

            if (coreType == typeof(DateOnly))
            {
                var value = new DateOnly(year, month, day);
                // box as nullable if needed
                object boxed = isNullable ? (DateOnly?)value : value;
                prop.SetValue(obj, boxed);
            }
            else if (coreType == typeof(DateTime))
            {
                var value = new DateTime(year, month, day);
                object boxed = isNullable ? (DateTime?)value : value;
                prop.SetValue(obj, boxed);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Property '{propertyName}' on {obj.GetType().Name} is of unsupported type '{t}'.");
            }
        }

        [Fact]
        public async Task GeneratePdf_ShouldContainCoreFields_In_BG_Culture()
        {
            var prev = CultureInfo.CurrentCulture;
            var prevUi = CultureInfo.CurrentUICulture;
            CultureInfo.CurrentCulture = new CultureInfo("bg-BG");
            CultureInfo.CurrentUICulture = new CultureInfo("bg-BG");

            try
            {
                var loan = new Loans
                {
                    Id = 42,
                    ApprovedAmount = 12345.67m,
                    Customer = new Customers
                    {
                        FirstName = "Ivan",
                        LastName = "Petrov",
                        Email = "ivan@example.com"
                    }
                };

                // Set dates safely regardless of model type (DateOnly/DateTime)
                SetDateProperty(loan, "Term", 2026, 12, 31);
                SetDateProperty(loan, "ApprovalDate", 2025, 1, 15);

                var gen = new LoanContractGenerator();
                var pdf = await gen.GeneratePdfAsync(loan);

                Assert.NotNull(pdf);
                Assert.True(pdf.Length > 100);

                var text = ExtractAllText(pdf);
                var norm = Normalize(text);

                Assert.Contains("LoanAgreement", norm);
                Assert.Contains("ClientName:IvanPetrov", norm);
                Assert.Contains("LoanID:42", norm);

                // сума с локалното форматиране (bg-BG, N2) + " BGN"
                var amountFormatted = loan.ApprovedAmount.ToString("N2", CultureInfo.CurrentCulture);
                Assert.Contains(Normalize($"Loan Amount: {amountFormatted} BGN"), norm);

                // очекувани датуми (текстот во PDF е без празнини по Normalize)
                Assert.Contains("LoanTerm:until31.12.2026", norm);
                Assert.Contains("ApprovalDate:15.01.2025", norm);

                Assert.Contains("TermsandConditions:", norm);
                Assert.Contains("Client’sSignature", norm); // може да се појави и како "Client's"
            }
            finally
            {
                CultureInfo.CurrentCulture = prev;
                CultureInfo.CurrentUICulture = prevUi;
            }
        }

        [Fact]
        public async Task GeneratePdf_ShouldWork_WithNullCustomer()
        {
            var loan = new Loans
            {
                Id = 7,
                ApprovedAmount = 5000m,
                // намерно null клиент (ако е ненулабилен во моделот, ова е тест-случај)
                Customer = null!
            };

            SetDateProperty(loan, "Term", 2025, 5, 10);
            SetDateProperty(loan, "ApprovalDate", 2025, 2, 1);

            var gen = new LoanContractGenerator();
            var pdf = await gen.GeneratePdfAsync(loan);

            Assert.NotNull(pdf);
            Assert.True(pdf.Length > 100);

            var norm = Normalize(ExtractAllText(pdf));

            // Редът с името съществува, дори и да е празен
            Assert.Contains("ClientName:", norm);
            Assert.Contains("LoanID:7", norm);
            Assert.Contains("BGN", norm);
        }
    }
}
