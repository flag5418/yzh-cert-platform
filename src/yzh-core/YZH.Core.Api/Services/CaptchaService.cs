using Microsoft.Extensions.Caching.Memory;
using SkiaSharp;

namespace YZH.Core.Api.Services;

/// <summary>
///     验证码服务实现
///     使用 SkiaSharp 生成图片，IMemoryCache 缓存验证码
/// </summary>
public class CaptchaService : ICaptchaService
{
    private readonly IMemoryCache _cache;
    private static readonly string[] Chars = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
    private static readonly SKColor[] Colors = { SKColors.Black, SKColors.Green, SKColors.Brown, SKColors.DarkBlue, SKColors.Purple };
    private static readonly string[] Fonts = { "Arial", "Verdana", "Microsoft Sans Serif" };

    public CaptchaService(IMemoryCache cache)
    {
        _cache = cache;
    }

    /// <summary>
    ///     生成验证码
    /// </summary>
    public string Generate(out string code, out string uuid)
    {
        // 生成 4 位随机数字
        code = RandomText();
        uuid = Guid.NewGuid().ToString("N");

        // 生成图片
        var imageBase64 = CreateBase64Image(code);

        // 缓存验证码（5分钟过期）
        _cache.Set($"captcha_{uuid}", code, TimeSpan.FromMinutes(5));

        return imageBase64;
    }

    /// <summary>
    ///     验证验证码
    /// </summary>
    public bool Verify(string uuid, string code)
    {
        if (string.IsNullOrEmpty(uuid) || string.IsNullOrEmpty(code))
            return false;

        var key = $"captcha_{uuid}";
        if (!_cache.TryGetValue(key, out string? cachedCode))
            return false;

        // 验证后清除（一次性使用）
        _cache.Remove(key);

        // 不区分大小写比较
        return string.Equals(cachedCode, code, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     生成随机验证码文本（4位数字）
    /// </summary>
    private static string RandomText()
    {
        var code = "";
        var temp = -1;
        var rand = new Random();
        for (int i = 1; i < 5; i++)
        {
            if (temp != -1)
            {
                rand = new Random(i * temp * unchecked((int)DateTime.Now.Ticks));
            }
            var t = rand.Next(Chars.Length);
            if (temp != -1 && temp == t)
            {
                return RandomText();
            }
            temp = t;
            code += Chars[t];
        }
        return code;
    }

    /// <summary>
    ///     创建 Base64 验证码图片
    /// </summary>
    private static string CreateBase64Image(string code)
    {
        var random = new Random();
        var width = (int)code.Length * 18;
        var height = 32;
        var info = new SKImageInfo(width, height);

        using var bitmap = new SKBitmap(info);
        using var canvas = new SKCanvas(bitmap);

        // 白色背景
        canvas.Clear(SKColors.White);

        using var pen = new SKPaint
        {
            FakeBoldText = true,
            Style = SKPaintStyle.Fill,
            TextSize = 20
        };

        // 绘制验证码字符
        for (int i = 0; i < code.Length; i++)
        {
            pen.Color = Colors[random.Next(Colors.Length)];
            pen.Typeface = SKTypeface.FromFamilyName(
                Fonts[random.Next(Fonts.Length)],
                700, 20, SKFontStyleSlant.Italic);

            var point = new SKPoint(i * 16, 22);
            canvas.DrawText(code.Substring(i, 1), point, pen);
        }

        // 绘制噪点
        var points = Enumerable.Range(0, 100)
            .Select(_ => new SKPoint(random.Next(bitmap.Width), random.Next(bitmap.Height)))
            .ToArray();
        canvas.DrawPoints(SKPointMode.Points, points, pen);

        // 绘制干扰线
        for (int i = 0; i < 2; i++)
        {
            using var linePen = new SKPaint
            {
                Color = Colors[random.Next(Colors.Length)],
                Style = SKPaintStyle.Stroke
            };

            using var path = new SKPath();
            path.MoveTo(random.Next(width), random.Next(height));
            path.CubicTo(
                random.Next(width), random.Next(height),
                random.Next(width), random.Next(height),
                random.Next(width), random.Next(height));
            canvas.DrawPath(path, linePen);
        }

        // 编码为 PNG Base64
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return Convert.ToBase64String(data.ToArray());
    }
}
