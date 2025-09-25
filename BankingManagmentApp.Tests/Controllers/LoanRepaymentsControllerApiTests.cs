using BankingManagmentApp.Controllers;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace BankingManagmentApp.Tests.Controllers
{
    public class LoanRepaymentsControllerApiTests
    {
        private ApplicationDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()) 
                .Options;

            return new ApplicationDbContext(options);
        }

        private LoanRepaymentsController GetController(ApplicationDbContext context, string userId)
        {
            var controller = new LoanRepaymentsController(context);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId)
            }, "mock"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            };

            return controller;
        }

        [Fact]
        public async Task Index_ShouldMarkOverdueRepayments()
        {
            var context = GetDbContext();
            var userId = "user123";

            var customer = new Customers
            {
                Id = userId,
                UserName = "TestUser",
                IsActive = true,
                CreateAt = DateTime.UtcNow
            };

            var loan = new Loans
            {
                Id = 1,
                CustomerId = userId,
                Customer = customer,
                Type = "Personal",
                Amount = 5000m,
                Term = DateOnly.FromDateTime(DateTime.Today.AddYears(1)),
                ApprovedAmount = 5000m,
                Status = "Approved",
                ApprovalDate = DateTime.UtcNow,
                Date = DateTime.UtcNow
            };

            var repayment1 = new LoanRepayments
            {
                Id = 1,
                Loan = loan,
                DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-5)), 
                Status = "Pending",
                AmountDue = 100,
                AmountPaid = 0
            };

            var repayment2 = new LoanRepayments
            {
                Id = 2,
                Loan = loan,
                DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5)), 
                Status = "Pending",
                AmountDue = 200,
                AmountPaid = 0
            };

            context.Customers.Add(customer);
            context.Loans.Add(loan);
            context.LoanRepayments.AddRange(repayment1, repayment2);
            await context.SaveChangesAsync();

            var controller = GetController(context, userId);

            var result = await controller.Index();

            result.Should().BeOfType<ViewResult>();

            var updatedRepayment1 = await context.LoanRepayments.FindAsync(1);
            var updatedRepayment2 = await context.LoanRepayments.FindAsync(2);

            updatedRepayment1!.Status.Should().Be("Overdue");  
            updatedRepayment2!.Status.Should().Be("Pending");   
        }

        [Fact]
        public async Task Index_ShouldReturnEmptyList_WhenNoRepayments()
        {
            var context = GetDbContext();
            var controller = GetController(context, "user999");

            var result = await controller.Index();

            result.Should().BeOfType<ViewResult>();
            (await context.LoanRepayments.CountAsync()).Should().Be(0);
        }
    }
}
