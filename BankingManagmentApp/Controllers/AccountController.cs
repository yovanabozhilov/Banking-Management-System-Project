using Microsoft.AspNetCore.Mvc;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace BankingManagmentApp.Controllers
{
    public class AccountController : Controller
    {
        public IActionResult CaptchaImage()
        {
            var captchaText = new Random().Next(1000, 9999).ToString();
            HttpContext.Session.SetString("Captcha", captchaText);

            using var image = new Image<Rgba32>(120, 40, Color.White);

            var font = SystemFonts.CreateFont("Arial", 20, FontStyle.Bold);
            image.Mutate(ctx => ctx.DrawText(captchaText, font, Color.Black, new PointF(10, 5)));

            using var ms = new MemoryStream();
            image.Save(ms, new PngEncoder());

            return File(ms.ToArray(), "image/png");
        }
    }
}
