using BankingManagmentApp.Models;
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
        public DbSet<Feedback> Feedbacks { get; set; } = default!;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---- vw_CreditFeatures -> keyless VIEW mapping ----
            modelBuilder.Entity<CreditFeatures>(eb =>
            {
                eb.HasNoKey();                  // keyless, защото е VIEW
                eb.ToView("vw_CreditFeatures"); // ИМЕТО на изгледа в SQL (dbo.vw_CreditFeatures)

                // ВАЖНО: това НЕ трябва да присъства (чупи миграциите):
                // eb.Metadata.SetIsTableExcludedFromMigrations(true);

                eb.Property(p => p.UserId).HasColumnName("UserId");

                // Decimal precision за избягване на EF Core warning-и
                eb.Property(p => p.AvgMonthlyInflow).HasPrecision(18, 2);
                eb.Property(p => p.AvgMonthlyOutflow).HasPrecision(18, 2);
                eb.Property(p => p.TotalBalance).HasPrecision(18, 2);
            });

            // НЕ слагай HasKey върху CreditFeatures (то е VIEW, keyless)
            // modelBuilder.Entity<CreditFeatures>().HasKey(x => x.UserId); // <-- махнато

            // ---- Decimal конфигурации за останалите ентитети ----
            modelBuilder.Entity<Accounts>().Property(p => p.Balance).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Loans>().Property(p => p.Amount).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Loans>().Property(p => p.ApprovedAmount).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<LoanRepayments>().Property(p => p.AmountDue).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<LoanRepayments>().Property(p => p.AmountPaid).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Transactions>().Property(p => p.Amount).HasColumnType("decimal(18,2)");

            // ---- TemplateAnswer constraints ----
            modelBuilder.Entity<TemplateAnswer>()
                .Property(t => t.Keyword).IsRequired();

            modelBuilder.Entity<TemplateAnswer>()
                .Property(t => t.AnswerText).IsRequired();

            modelBuilder.Entity<TemplateAnswer>()
                .HasIndex(t => t.Keyword);
        }
    }
}
