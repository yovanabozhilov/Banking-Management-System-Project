using BankingManagmentApp.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BankingManagmentApp.Data
{
    public class ApplicationDbContext : IdentityDbContext<Customers>
    {
        public DbSet<Customers> Customers { get; set; }
        public DbSet<Accounts> Accounts { get; set; }
        public DbSet<Transactions> Transactions { get; set; }
        public DbSet<Loans> Loans { get; set; }
        public DbSet<LoanRepayments> LoanRepayments { get; set; }
        public DbSet<CreditAssessments> CreditAssessments { get; set; }
        public DbSet<TemplateAnswer> TemplateAnswers { get; set; }
        public DbSet<ChatHistory> ChatHistories { get; set; }
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
    }
}
