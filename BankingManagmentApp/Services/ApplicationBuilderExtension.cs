// Services/ApplicationBuilderExtension.cs
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BankingManagmentApp.Services
{
    public static class ApplicationBuilderExtension
    {
        public static async Task<IApplicationBuilder> PrepareDataBase(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var services = scope.ServiceProvider;
            var loggerFactory = services.GetRequiredService<ILoggerFactory>();

            try
            {
                var db = services.GetRequiredService<ApplicationDbContext>();
                var userManager = services.GetRequiredService<UserManager<Customers>>();
                var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

                await db.Database.MigrateAsync();
                await SeedRolesAsync(roleManager);

                var superAdmin = await SeedSuperAdminAsync(userManager);
                await SeedBankDataForUserAsync(db, superAdmin);

                // Ако искаш – демо потребител:
                // await SeedBankDataForEmailAsync(db, userManager, "user22@example.com");
            }
            catch (Exception ex)
            {
                var logger = loggerFactory.CreateLogger<Program>();
                logger.LogError(ex, "An error occurred seeding the DB.");
            }

            return app;
        }

        public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
        {
            if (!await roleManager.RoleExistsAsync("Admin"))
                await roleManager.CreateAsync(new IdentityRole("Admin"));

            if (!await roleManager.RoleExistsAsync("User"))
                await roleManager.CreateAsync(new IdentityRole("User"));
        }

        public static async Task<Customers> SeedSuperAdminAsync(UserManager<Customers> userManager)
        {
            var email = "superadmin@gmail.com";
            var existing = await userManager.FindByEmailAsync(email);
            if (existing != null) return existing;

            var defaultUser = new Customers
            {
                UserName = "superadmin",
                Email = email,
                FirstName = "Experian",
                LastName = "WorkShop",
                PhoneNumber = "0899999999",
                Address = "Sofia",
                EmailConfirmed = true,
                PhoneNumberConfirmed = true,
            };

            var result = await userManager.CreateAsync(defaultUser, "123!@#Qwe");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(defaultUser, "Admin");
                return defaultUser;
            }

            var err = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException("Failed to seed super admin: " + err);
        }

        private static async Task SeedBankDataForUserAsync(ApplicationDbContext db, Customers user)
        {
            // 1) Account + няколко транзакции
            var hasAccount = await db.Accounts.AnyAsync(a => EF.Property<string>(a, "CustomerId1") == user.Id);
            if (!hasAccount)
            {
                var acc = new Accounts
                {
                    IBAN = "BG18RZBB91550123456789",
                    AccountType = "Current",
                    Balance = 1250.50m,
                    Currency = "BGN",
                    Status = "Active",
                    Customer = user
                };
                db.Accounts.Add(acc);
                await db.SaveChangesAsync();

                db.Transactions.AddRange(
                    new Transactions
                    {
                        AccountsId = acc.Id,
                        TransactionType = "Credit",
                        Amount = 200.00m,
                        Date = DateOnly.FromDateTime(DateTime.Today),
                        Description = "Salary",
                        ReferenceNumber = 100001
                    },
                    new Transactions
                    {
                        AccountsId = acc.Id,
                        TransactionType = "Debit",
                        Amount = 50.25m,
                        Date = DateOnly.FromDateTime(DateTime.Today),
                        Description = "Groceries",
                        ReferenceNumber = 100002
                    }
                );
                await db.SaveChangesAsync();
            }

            // 2) Един заем
            var loan = await db.Loans
                .Where(l => EF.Property<string>(l, "CustomerId1") == user.Id)
                .FirstOrDefaultAsync();

            if (loan == null)
            {
                loan = new Loans
                {
                    Type = "Personal",
                    Amount = 5000m,
                    ApprovedAmount = 5000m,
                    Status = "Approved",
                    Term = DateOnly.FromDateTime(DateTime.Today.AddYears(1)),
                    Date = DateTime.UtcNow,
                    ApprovalDate = DateTime.UtcNow,
                    Customer = user
                };
                db.Loans.Add(loan);
                await db.SaveChangesAsync();
            }

            // 3) Падежи (употребяваме навигацията Loans = loan, НЯМА LoansId свойство)
            var hasRepayments = await db.LoanRepayments.AnyAsync(r => r.LoanId == loan.Id);
            if (!hasRepayments)
            {
                var today = DateOnly.FromDateTime(DateTime.Today);

                db.LoanRepayments.AddRange(
                    new LoanRepayments
                    {
                        Loans = loan,
                        LoanId = loan.Id,
                        DueDate = today.AddMonths(1),
                        AmountDue = 420.00m,
                        AmountPaid = 0m,
                        PaymentDate = today,
                        Status = "Due"
                    },
                    new LoanRepayments
                    {
                        Loans = loan,
                        LoanId = loan.Id,
                        DueDate = today.AddMonths(2),
                        AmountDue = 420.00m,
                        AmountPaid = 0m,
                        PaymentDate = today,
                        Status = "Scheduled"
                    },
                    new LoanRepayments
                    {
                        Loans = loan,
                        LoanId = loan.Id,
                        DueDate = today.AddMonths(3),
                        AmountDue = 420.00m,
                        AmountPaid = 0m,
                        PaymentDate = today,
                        Status = "Scheduled"
                    }
                );
                await db.SaveChangesAsync();
            }
            else
            {
                // Фикс на евентуални "осиротели" редове
                var toFix = await db.LoanRepayments
                    .Where(r => r.LoanId == loan.Id && r.Loans == null)
                    .ToListAsync();
                if (toFix.Count > 0)
                {
                    foreach (var r in toFix) r.Loans = loan;
                    await db.SaveChangesAsync();
                }
            }

            // НЯМА seed за CreditAssessments – скорът се смята динамично в контролера.
        }
    }
}
