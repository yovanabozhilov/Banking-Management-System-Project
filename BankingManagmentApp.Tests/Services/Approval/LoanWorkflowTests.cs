using System;
using System.Threading.Tasks;
using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services.Approval;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Linq;

public class LoanWorkflowTests
{
    private ApplicationDbContext GetInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ProcessNewApplicationAsync_NullLoan_ThrowsArgumentNullException()
    {
        var db = GetInMemoryDb();
        var engineMock = new Mock<ILoanApprovalEngine>();
        var policy = new LoanApprovalPolicy { DefaultMonths = 12, AnnualInterest = 0.05m };
        var workflow = new LoanWorkflow(db, engineMock.Object, policy);

        await Assert.ThrowsAsync<ArgumentNullException>(() => workflow.ProcessNewApplicationAsync(null));
    }

    [Fact]
    public async Task ProcessNewApplicationAsync_MissingCustomerId_ThrowsInvalidOperationException()
    {
        var db = GetInMemoryDb();
        var engineMock = new Mock<ILoanApprovalEngine>();
        var policy = new LoanApprovalPolicy { DefaultMonths = 12, AnnualInterest = 0.05m };
        var workflow = new LoanWorkflow(db, engineMock.Object, policy);

        var loan = new Loans { Id = 1, CustomerId = "", Amount = 1000, Type = "Personal", Status = "Pending" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => workflow.ProcessNewApplicationAsync(loan));
    }

    [Fact]
    public async Task ProcessNewApplicationAsync_AutoApprovedLoan_CreatesRepaymentPlan()
    {
        var db = GetInMemoryDb();
        var engineMock = new Mock<ILoanApprovalEngine>();
        engineMock.Setup(e => e.DecideAsync(It.IsAny<string>(), It.IsAny<ApplicationFeatures>()))
                  .ReturnsAsync(new ApprovalDecision(
                      ApprovalOutcome.AutoApproved,
                      1000m,
                      1,
                      750,
                      "Good credit"
                  ));

        var policy = new LoanApprovalPolicy { DefaultMonths = 12, AnnualInterest = 0.12m };
        var workflow = new LoanWorkflow(db, engineMock.Object, policy);

        var loan = new Loans
        {
            Id = 1,
            CustomerId = "cust1",
            Amount = 1000,
            Type = "Personal",
            Status = "Pending"
        };
        db.Loans.Add(loan);
        await db.SaveChangesAsync();

        await workflow.ProcessNewApplicationAsync(loan);

        var updatedLoan = await db.Loans.FindAsync(loan.Id);
        Assert.Equal("PendingReview", updatedLoan.Status);  // <-- corrected
        Assert.Equal(1000, updatedLoan.ApprovedAmount);

        var repayments = await db.LoanRepayments.Where(r => r.LoanId == loan.Id).ToListAsync();
        Assert.Equal(policy.DefaultMonths, repayments.Count);
    }

    [Fact]
    public async Task ProcessNewApplicationAsync_AutoDeclinedLoan_UpdatesStatus()
    {
        var db = GetInMemoryDb();
        var engineMock = new Mock<ILoanApprovalEngine>();
        engineMock.Setup(e => e.DecideAsync(It.IsAny<string>(), It.IsAny<ApplicationFeatures>()))
                  .ReturnsAsync(new ApprovalDecision(
                      ApprovalOutcome.AutoDeclined,
                      0m,
                      3,
                      400,
                      "Low credit"
                  ));
    
        var policy = new LoanApprovalPolicy { DefaultMonths = 12, AnnualInterest = 0.12m };
        var workflow = new LoanWorkflow(db, engineMock.Object, policy);
    
        var loan = new Loans
        {
            Id = 2,
            CustomerId = "cust2",
            Amount = 5000,
            Type = "Personal",
            Status = "Pending"
        };
        db.Loans.Add(loan);
        await db.SaveChangesAsync();
    
        await workflow.ProcessNewApplicationAsync(loan);
    
        var updatedLoan = await db.Loans.FindAsync(loan.Id);
        Assert.Equal("AutoDeclined", updatedLoan.Status);  // <-- corrected
        Assert.Equal(0, updatedLoan.ApprovedAmount);
    }
}
