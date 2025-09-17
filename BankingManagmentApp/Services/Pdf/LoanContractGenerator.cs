using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using System.IO;
using BankingManagmentApp.Models;
using System.Threading.Tasks;

public class LoanContractGenerator
{
    public Task<byte[]> GeneratePdfAsync(Loans loan)
    {
        using (var stream = new MemoryStream())
        {
            var writer = new PdfWriter(stream);
            var pdf = new PdfDocument(writer);
            var document = new Document(pdf);

            document.Add(new Paragraph("Loan Agreement")
                .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                .SetFontSize(24)
                );

            document.Add(new Paragraph(" "));

            document.Add(new Paragraph($"This loan agreement is made between the Client and the Bank.")
                .SetFontSize(12));

            document.Add(new Paragraph($"Client Name: {loan.Customer?.FirstName} {loan.Customer?.LastName}"));
            document.Add(new Paragraph($"Loan ID: {loan.Id}"));
            document.Add(new Paragraph($"Loan Amount: {loan.ApprovedAmount:N2} BGN"));
            document.Add(new Paragraph($"Loan Term: until {loan.Term.ToString("dd.MM.yyyy")}"));
            document.Add(new Paragraph($"Approval Date: {loan.ApprovalDate.ToString("dd.MM.yyyy")}"));

            document.Add(new Paragraph(" "));
            document.Add(new Paragraph("Terms and Conditions:")
                .SetUnderline());
            document.Add(new Paragraph("1. The Client undertakes to repay the loan according to the attached repayment schedule."));
            document.Add(new Paragraph("2. All payments shall be made via bank transfer."));

            // Here you can add more information, such as a repayment schedule table, if available

            document.Add(new Paragraph(" "));
            document.Add(new Paragraph("Signatures:"));
            document.Add(new Paragraph("_________________________")
                .SetPaddingTop(30));
            document.Add(new Paragraph("Client’s Signature"));

            document.Close();
            return Task.FromResult(stream.ToArray());
        }
    }
}
