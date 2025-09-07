#nullable disable

using BankingManagmentApp.Data;
using BankingManagmentApp.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

namespace BankingManagmentApp.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<Customers> _signInManager;
        private readonly UserManager<Customers> _userManager;
        private readonly IUserStore<Customers> _userStore;
        private readonly IUserEmailStore<Customers> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;

        public RegisterModel(
            UserManager<Customers> userManager,
            IUserStore<Customers> userStore,
            SignInManager<Customers> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _userStore = userStore;
            //_emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [Display(Name = "FirstName")]
            public string FirstName { get; set; }

            [Required]
            [Display(Name = "LastName")]
            public string LastName { get; set; }

            [Required]
            [Display(Name = "Address")]
            public string Address { get; set; }

            [Required]
            [Display(Name = "PhoneNumber")]
            public string PhoneNumber { get; set; }

            [Required]
            [Display(Name = "DateOfBirth")]
            public DateOnly DateOfBirth { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {

                var user = new Customers
                {
                    UserName = Input.Email,
                    Email = Input.Email,
                    FirstName = Input.FirstName,
                    LastName = Input.LastName,
                    Address = Input.Address,
                    PhoneNumber = Input.PhoneNumber,
                    CreateAt = DateTime.Now,
                    DateOfBirth = Input.DateOfBirth,
                    IsActive = true
                };



                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "User");
                    _logger.LogInformation("User created a new account with password.");

                    const string digits = "0123456789";
                    const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

                    char[] ibanChars = new char[20];

                    // първо 10 цифри
                    for (int i = 0; i < 10; i++)
                    {
                        int idx = RandomNumberGenerator.GetInt32(digits.Length);
                        ibanChars[i] = digits[idx];
                    }

                    // после 10 букви
                    for (int i = 8; i < 20; i++)
                    {
                        int idx = RandomNumberGenerator.GetInt32(letters.Length);
                        ibanChars[i] = letters[idx];
                    }

                    // разбъркваме, за да не са подредени
                    ibanChars = ibanChars.OrderBy(c => RandomNumberGenerator.GetInt32(1000)).ToArray();

                    string iban = "BG" + new string(ibanChars);

                    Accounts acc = new Accounts();
                    acc.CreateAt = DateTime.Now;
                    acc.Currency = "BGN";
                    acc.Status = "Active";
                    acc.IBAN = iban;
                    acc.CustomerId = user.Id;
                    acc.AccountType = "User";
                    acc.Balance = 0;
                    _context.Accounts.Add(acc);
                    _context.SaveChanges();



                    var userId = await _userManager.GetUserIdAsync(user);
                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new { area = "Identity", userId = userId, code = code, returnUrl = returnUrl },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                        $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, returnUrl = returnUrl });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            return Page();
        }
    }
}