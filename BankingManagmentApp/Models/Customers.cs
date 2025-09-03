namespace BankingManagmentApp.Models
{
    public class Customers
    {
        public int Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Adress { get; set; }
        public DateOnly DateOfBirth { get; set; }
        public DateTime CreateAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; }

        public ICollection<Accounts> Accounts { get; set; } = new List<Accounts>();
    }

}
