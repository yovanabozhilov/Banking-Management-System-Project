namespace BankingManagmentApp.Models
{
    public class LoanApplication
    {
        public int Id { get; set; }
        public int LoanId { get; set; }
        public Loans Loan { get; set; }

        public string FileName { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public byte[] Data { get; set; } 
        public DateTime UploadedAt { get; set; } = DateTime.Now;
    }
}