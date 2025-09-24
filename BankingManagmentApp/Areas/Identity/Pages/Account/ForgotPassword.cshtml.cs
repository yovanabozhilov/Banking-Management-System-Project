// Areas/Identity/Pages/Account/ForgotPassword.cshtml.cs
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using BankingManagmentApp.Models;
using BankingManagmentApp.Services;

namespace BankingManagmentApp.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<Customers> _userManager;
        private readonly IEmailService _emailSender;
        private readonly ILogger<ForgotPasswordModel> _logger;

        public ForgotPasswordModel(
            UserManager<Customers> userManager,
            IEmailService emailSender,
            ILogger<ForgotPasswordModel> logger)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required, EmailAddress]
            public string Email { get; set; } = string.Empty;
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.FindByEmailAsync(Input.Email);

            // Не издаваме дали акаунтът съществува
            if (user == null /* || !(await _userManager.IsEmailConfirmedAsync(user)) */)
                return RedirectToPage("./ForgotPasswordConfirmation");

            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            // при теб ResetPassword е в /Account/Manage
            var callbackUrl = Url.Page(
                "/Account/Manage/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code = encoded, email = Input.Email },
                protocol: Request.Scheme
            ) ?? $"{Request.Scheme}://{Request.Host}/Identity/Account/Manage/ResetPassword?code={encoded}&email={Uri.EscapeDataString(Input.Email)}";

            try
            {
                await _emailSender.SendEmailAsync(
                    Input.Email,
                    "Reset your password",
                    $"Please reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>."
                );
                _logger.LogInformation("Password reset email queued for {Email}", Input.Email);
            }
            catch (Exception ex) // напр. MailKit.Net.Smtp.SmtpProtocolException при quota
            {
                _logger.LogWarning(ex, "Failed to send reset email (likely SMTP quota). Proceeding to confirmation for {Email}", Input.Email);
                // Няма rethrow – UX продължава нормално
            }

            return RedirectToPage("./ForgotPasswordConfirmation");
        }
    }
}
