using SkiaSharp;

namespace PKForge.Chrome;

/// <summary>The original DS title/status strips, composed from the shared theme.</summary>
public static class AppChromePaint
{
    public static void Header(SKCanvas canvas, SKRect bounds, string title, SKTypeface face, float unit = 1,
        string statusName = "PKForge", string status = "Offline")
    {
        using var fill = new SKPaint { Color = Pksm.ButtonBlueDeep };
        canvas.DrawRect(bounds.Left, bounds.Top, bounds.Width, 28 * unit, fill);
        using var titleFont = new SKFont(face, 16 * unit);
        PksmPaint.CenterText(canvas, title, bounds.MidX, bounds.Top + 14 * unit, titleFont, ColorTheme.Current.Bright, SKColors.Transparent, SKTextAlign.Center);
        fill.Color = Pksm.Paper; canvas.DrawRect(bounds.Left, bounds.Top + 28 * unit, bounds.Width, 26 * unit, fill);
        using var statusFont = new SKFont(face, 14 * unit);
        var statusLeft = bounds.Left + 12 * unit; var statusY = bounds.Top + 41 * unit;
        PksmPaint.CenterText(canvas, statusName, statusLeft, statusY, statusFont, Pksm.Ink, SKColors.Transparent);
        var marker = statusLeft + statusFont.MeasureText(statusName) + 15 * unit;
        fill.Color = Pksm.InkSoft; canvas.DrawRect(marker, statusY - 3 * unit, 6 * unit, 6 * unit, fill);
        using var flagFont = new SKFont(face, 13.5f * unit);
        PksmPaint.CenterText(canvas, status, marker + 21 * unit, statusY, flagFont, Pksm.InkSoft, SKColors.Transparent);
        fill.Color = Pksm.InkSoft; var battery = SKRect.Create(bounds.Right - 34 * unit, bounds.Top + 35 * unit, 22 * unit, 12 * unit);
        canvas.DrawRoundRect(battery, 2 * unit, 2 * unit, fill);
        fill.Color = Pksm.Paper; canvas.DrawRect(SKRect.Inflate(battery, -2 * unit, -2 * unit), fill);
        fill.Color = Pksm.Legal; canvas.DrawRect(new(battery.Left + 3 * unit, battery.Top + 3 * unit, battery.Right - 8 * unit, battery.Bottom - 3 * unit), fill);
    }

    /// <summary>Kit.HeaderBar: the box-name banner's gradient under an ink rim, its title centred.</summary>
    public static void TitleBanner(SKCanvas canvas, SKRect rect, string title, SKTypeface face, float size = 16)
    {
        // MAUI CornerRadius(10, 3, 10, 3): top-left and bottom-left round, the right side nearly square.
        using var shape = new SKRoundRect();
        shape.SetRectRadii(SKRect.Inflate(rect, -0.75f, -0.75f), [new(10, 10), new(3, 3), new(3, 3), new(10, 10)]);
        using (var fill = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateLinearGradient(new(rect.Left, rect.Top), new(rect.Left, rect.Bottom),
                [StoragePaint.BannerTop, StoragePaint.BannerBottom], SKShaderTileMode.Clamp),
        })
            canvas.DrawRoundRect(shape, fill);
        using (var rim = new SKPaint { Color = Pksm.Ink, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true })
            canvas.DrawRoundRect(shape, rim);
        using var font = new SKFont(face, size) { Edging = SKFontEdging.Antialias };
        var text = Fit(title, font, rect.Width - 20);
        var y = rect.MidY - (font.Metrics.Ascent + font.Metrics.Descent) / 2;
        using (var shadow = new SKPaint { Color = Pksm.LogoVoid.WithAlpha(0x8F), IsAntialias = true })
            canvas.DrawText(text, rect.MidX + 1, y + 1, SKTextAlign.Center, font, shadow);
        using (var ink = new SKPaint { Color = ColorTheme.Current.Bright, IsAntialias = true })
            canvas.DrawText(text, rect.MidX, y, SKTextAlign.Center, font, ink);
    }

    /// <summary>Kit.MiniCapsule: the deep-blue 44×36 key beside a header ("‹ L", "R ›").</summary>
    public static void MiniCapsule(SKCanvas canvas, SKRect rect, string glyph, SKTypeface roundedFace)
    {
        using (var fill = new SKPaint { Color = Pksm.StorageMenuBlueDeep, IsAntialias = true })
            canvas.DrawRoundRect(rect, 6, 6, fill);
        using (var rim = new SKPaint { Color = Pksm.Ink.WithAlpha(0xD9), Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true })
            canvas.DrawRoundRect(SKRect.Inflate(rect, -0.75f, -0.75f), 5.25f, 5.25f, rim);
        using var font = new SKFont(roundedFace, 16) { Edging = SKFontEdging.Antialias, Embolden = true };
        using var ink = new SKPaint { Color = Pksm.Ink, IsAntialias = true };
        canvas.DrawText(glyph, rect.MidX, rect.MidY - (font.Metrics.Ascent + font.Metrics.Descent) / 2, SKTextAlign.Center, font, ink);
    }

    private static string Fit(string text, SKFont font, float width)
    {
        if (font.MeasureText(text) <= width) return text;
        for (var length = text.Length - 1; length > 0; length--)
        {
            var candidate = text[..length].TrimEnd() + "…";
            if (font.MeasureText(candidate) <= width) return candidate;
        }
        return "";
    }

    public static SKFont HintFont(SKRect bounds, IReadOnlyList<(string Key, string Label)> prompts, SKTypeface face, float size)
    {
        var font = new SKFont(face, size);
        var hits = PksmPaint.HintBarHitRects(bounds, prompts, font);
        if (hits.Count > 0 && hits[^1].Right - hits[0].Left > bounds.Width - 12)
            font.Size *= (bounds.Width - 12) / (hits[^1].Right - hits[0].Left);
        return font;
    }
}
