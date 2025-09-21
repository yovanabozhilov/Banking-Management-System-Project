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

        // Използваме това име и в тестовете
        public DbSet<LoanApplication> LoanApplication { get; set; } 
        public DbSet<CreditFeatures> CreditFeaturesView => Set<CreditFeatures>();

        public DbSet<Feedback> Feedbacks { get; set; } = default!;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---- CreditFeatures: View в нормална БД, "таблица" с ключ в InMemory за тестове ----
            if (Database.IsInMemory())
            {
                modelBuilder.Entity<CreditFeatures>(eb =>
                {
                    eb.HasKey(x => x.UserId);
                    eb.ToTable("CreditFeatures"); // логическа таблица за InMemory provider
                    eb.Property(p => p.TotalBalance).HasColumnType("decimal(18,2)");
                    eb.Property(p => p.AvgMonthlyInflow).HasColumnType("decimal(18,2)");
                    eb.Property(p => p.AvgMonthlyOutflow).HasColumnType("decimal(18,2)");
                });
            }
            else
            {
                // В реалната БД това е VIEW
                modelBuilder.Entity<CreditFeatures>(eb =>
                {
                    eb.HasNoKey();
                    eb.ToView("vw_CreditFeatures");
                    eb.Metadata.SetIsTableExcludedFromMigrations(true);
                    eb.Property(p => p.UserId).HasColumnName("UserId");
                    eb.Property(p => p.TotalBalance).HasColumnType("decimal(18,2)");
                    eb.Property(p => p.AvgMonthlyInflow).HasColumnType("decimal(18,2)");
                    eb.Property(p => p.AvgMonthlyOutflow).HasColumnType("decimal(18,2)");
                });
            }

            // ---- Precision за парични полета ----
            modelBuilder.Entity<Accounts>().Property(p => p.Balance).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Loans>().Property(p => p.Amount).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Loans>().Property(p => p.ApprovedAmount).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<LoanRepayments>().Property(p => p.AmountDue).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<LoanRepayments>().Property(p => p.AmountPaid).HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Transactions>().Property(p => p.Amount).HasColumnType("decimal(18,2)");

            // ---- TemplateAnswer ----
            modelBuilder.Entity<TemplateAnswer>()
                .Property(t => t.Keyword).IsRequired();

            modelBuilder.Entity<TemplateAnswer>()
                .Property(t => t.AnswerText).IsRequired();

            modelBuilder.Entity<TemplateAnswer>()
                .HasIndex(t => t.Keyword);
        }
    }
}
