namespace BankingManagmentApp.Models
{
    public class Transactions
    {
        public int Id { get; set; }
        public int AccountsId { get; set; }       
        public string TransactionType { get; set; }
        public decimal Amount { get; set; }
        public DateOnly Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public int ReferenceNumber { get; set; }

        public Accounts Accounts { get; set; }
    }

}
