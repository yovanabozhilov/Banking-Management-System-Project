using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BankingManagmentApp.Controllers;
using BankingManagmentApp.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace BankingManagmentApp.Tests.Controllers
{
    public class AccountsControllerApiTests
    {
        [Fact]
        public async Task Index_Returns_View_With_All_Accounts()
        {
            using var ctx = CtxHelper.NewInMemoryContext();

            var customer = new Customers { Id = "u1", Email = "u1@x.com" };
            ctx.Customers.Add(customer);
            ctx.Accounts.AddRange(
                new Accounts { CustomerId = "u1", IBAN = "BG00", AccountType = "User", Balance = 10, Currency = "BGN", CreateAt = DateTime.UtcNow, Status = "Active" },
                new Accounts { CustomerId = "u1", IBAN = "BG01", AccountType = "User", Balance = 20, Currency = "BGN", CreateAt = DateTime.UtcNow, Status = "Active" }
            );
            await ctx.SaveChangesAsync();

            var store = new FakeUserStore(); store.Add(customer);
            var userManager = UserManagerFactory.Create(store);

            var sut = new AccountsController(ctx, userManager);
            CtxHelper.AttachUser(sut, CtxHelper.PrincipalFor("u1"));

            var result = await sut.Index() as ViewResult;
            result.Should().NotBeNull();
            var model = result!.Model as List<Accounts>;
            model.Should().NotBeNull();
            model!.Count.Should().Be(2);
            model.All(a => a.Customer != null).Should().BeTrue(); // Include(Customer)
        }

        [Fact]
        public async Task Create_Post_InvalidModel_Adds_Account_And_Redirects()
        {
            // NOTE: Current controller adds only when ModelState is INVALID.
            using var ctx = CtxHelper.NewInMemoryContext();

            var user = new Customers { Id = "u1", Email = "u1@x.com" };
            ctx.Customers.Add(user);
            await ctx.SaveChangesAsync();

            var store = new FakeUserStore(); store.Add(user);
            var userManager = UserManagerFactory.Create(store);

            var sut = new AccountsController(ctx, userManager);
            CtxHelper.AttachUser(sut, CtxHelper.PrincipalFor("u1"));

            sut.ModelState.AddModelError("any", "invalid");

            var result = await sut.Create(new Accounts()) as RedirectToActionResult;
            result.Should().NotBeNull();
            result!.ControllerName.Should().Be("Profile");
            result.ActionName.Should().Be("Index");

            ctx.Accounts.Count().Should().Be(1);
            var acc = ctx.Accounts.Single();
            acc.CustomerId.Should().Be("u1");
            acc.AccountType.Should().Be("User");
            acc.Currency.Should().Be("BGN");
            acc.Status.Should().Be("Active");
        }

        [Fact]
        public async Task Create_Post_ValidModel_DoesNotAdd_But_Redirects()
        {
            using var ctx = CtxHelper.NewInMemoryContext();

            var user = new Customers { Id = "u1", Email = "u1@x.com" };
            ctx.Customers.Add(user);
            await ctx.SaveChangesAsync();

            var store = new FakeUserStore(); store.Add(user);
            var userManager = UserManagerFactory.Create(store);

            var sut = new AccountsController(ctx, userManager);
            CtxHelper.AttachUser(sut, CtxHelper.PrincipalFor("u1"));

            var result = await sut.Create(new Accounts
            {
                CustomerId = "ignored",
                IBAN = "BG00",
                AccountType = "User",
                Balance = 0,
                Currency = "BGN",
                CreateAt = DateTime.UtcNow,
                Status = "Active"
            }) as RedirectToActionResult;

            result.Should().NotBeNull();
            ctx.Accounts.Count().Should().Be(0); // current code path does NOT add when model is valid
        }

        [Fact]
        public async Task Details_UnknownId_Returns_NotFound()
        {
            using var ctx = CtxHelper.NewInMemoryContext();

            var store = new FakeUserStore();
            var userManager = UserManagerFactory.Create(store);

            var sut = new AccountsController(ctx, userManager);
            var res = await sut.Details(999);
            res.Should().BeOfType<NotFoundResult>();
        }
    }
}
