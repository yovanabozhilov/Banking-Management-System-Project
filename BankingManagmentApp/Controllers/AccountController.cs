﻿using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;              
using SixLabors.ImageSharp.Drawing.Processing;   
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

public class AccountController : Controller
{
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public IActionResult CaptchaImage()
    {
        var captchaText = RandomDigits(4);
        HttpContext.Session.SetString("Captcha", captchaText);

        var fontPath = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fonts", "DejaVuSans.ttf");
        var collection = new FontCollection();
        var family = collection.Add(fontPath);
        var font = family.CreateFont(24, FontStyle.Bold);

        using var img = new Image<Rgba32>(120, 40);
        img.Mutate(ctx =>
        {
            ctx.Fill(Color.White);

            var pb = new PathBuilder();
            pb.AddLine(new PointF(0, 10),  new PointF(120, 15));
            pb.AddLine(new PointF(0, 25),  new PointF(120, 30));
            var path = pb.Build();
            ctx.Draw(Color.LightGray, 1, path);

            ctx.DrawText(captchaText, font, Color.Black, new PointF(10, 5));
        });

        using var ms = new MemoryStream();
        img.Save(ms, new PngEncoder());
        return File(ms.ToArray(), "image/png");
    }

    [HttpPost]
    public IActionResult VerifyCaptcha(string captchaInput)
    {
        var expected = HttpContext.Session.GetString("Captcha");
        if (!string.IsNullOrEmpty(expected) &&
            string.Equals(expected, captchaInput, StringComparison.Ordinal))
        {
            return Ok();
        }
        return BadRequest("CAPTCHA mismatch");
    }

    private static string RandomDigits(int n) =>
        string.Concat(Enumerable.Range(0, n).Select(_ => Random.Shared.Next(0, 10)));
}