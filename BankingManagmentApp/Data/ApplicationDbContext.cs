using BankingManagmentApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BankingManagmentApp.Models.ML;

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
        public DbSet<TemplateAnswer> TemplateAnswer { get; set; }
        public DbSet<ChatHistory> ChatHistory { get; set; }
        public DbSet<CreditFeatures> CreditFeaturesView => Set<CreditFeatures>();

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }
        
        public DbSet<Feedback> Feedbacks { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1) Keyless entity за SQL изгледа vw_CreditFeatures
            modelBuilder.Entity<CreditFeatures>(eb =>
            {
                eb.HasNoKey();
                eb.ToView("vw_CreditFeatures");
                eb.Property(p => p.UserId).HasColumnName("UserId");
            });

            // 2) DECIMAL precision за SQL Server (премахва warning-ите и пази парите „цели“)
            modelBuilder.Entity<Accounts>().Property(p => p.Balance).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Loans>().Property(p => p.Amount).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Loans>().Property(p => p.ApprovedAmount).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<LoanRepayments>().Property(p => p.AmountDue).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<LoanRepayments>().Property(p => p.AmountPaid).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Transactions>().Property(p => p.Amount).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<CreditFeatures>().HasKey(x => x.UserId);
        }
    }
}
