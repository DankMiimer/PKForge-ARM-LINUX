using SkiaSharp;

namespace PKForge.Chrome;

/// <summary>The original DS title/status strips, composed from the shared theme.</summary>
public static class AppChromePaint
{
    /// <summary>The original grid begins at the content host, below its title/status rows.</summary>
    public static void Backdrop(SKCanvas canvas, SKRect bounds, float contentTop)
    {
        using var fill = new SKPaint { Color = Pksm.Housing };
        canvas.DrawRect(bounds, fill);
        var body = new SKRect(bounds.Left, bounds.Top + contentTop, bounds.Right, bounds.Bottom);
        if (body.Height <= 0) return;
        var saved = canvas.Save(); canvas.ClipRect(body); canvas.Translate(body.Left, body.Top);
        PksmPaint.LogoGrid(canvas, new(0, 0, body.Width, body.Height));
        canvas.RestoreToCount(saved);
    }
    public static void Header(SKCanvas canvas, SKRect bounds, string title, SKTypeface face, float unit = 1,
        string statusName = "PKForge", string status = "Offline")
    {
        using var fill = new SKPaint { Color = Pksm.ButtonBlueDeep };
        canvas.DrawRect(bounds.Left, bounds.Top, bounds.Width, 28 * unit, fill);
        using var titleFont = new SKFont(face, 16 * unit);
        PksmPaint.CenterText(canvas, title, bounds.MidX, bounds.Top + 14 * unit, titleFont, ColorTheme.Current.Bright, SKColors.Transparent, SKTextAlign.Center);
        StatusStrip(canvas, new(bounds.Left, bounds.Top + 28 * unit, bounds.Right, bounds.Bottom), face, unit, statusName, status);
    }

    public static void StatusStrip(SKCanvas canvas, SKRect bounds, SKTypeface face, float unit = 1,
        string statusName = "PKForge", string status = "Offline")
    {
        using var fill = new SKPaint { Color = Pksm.Paper };
        canvas.DrawRect(bounds.Left, bounds.Top, bounds.Width, 26 * unit, fill);
        using var statusFont = new SKFont(face, 14 * unit);
        var statusLeft = bounds.Left + 12 * unit; var statusY = bounds.Top + 13 * unit;
        PksmPaint.CenterText(canvas, statusName, statusLeft, statusY, statusFont, Pksm.Ink, SKColors.Transparent);
        var marker = statusLeft + statusFont.MeasureText(statusName) + 15 * unit;
        fill.Color = Pksm.InkSoft; canvas.DrawRect(marker, statusY - 3 * unit, 6 * unit, 6 * unit, fill);
        using var flagFont = new SKFont(face, 13.5f * unit);
        PksmPaint.CenterText(canvas, status, marker + 21 * unit, statusY, flagFont, Pksm.InkSoft, SKColors.Transparent);
        fill.Color = Pksm.InkSoft; var battery = SKRect.Create(bounds.Right - 34 * unit, bounds.Top + 7 * unit, 22 * unit, 12 * unit);
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
        var room = bounds.Width - 12;
        // The 8-pixel gap after each key disc does not shrink with the font, so one proportional
        // step left a long bar too wide: the park's eight prompts were cut off at both ends on the
        // handhelds. Measure again until the prompts really fit.
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var hits = PksmPaint.HintBarHitRects(bounds, prompts, font);
            if (hits.Count == 0) break;
            var used = hits[^1].Right - hits[0].Left;
            if (used <= room) break;
            var fixedGaps = 8f * prompts.Count;
            font.Size *= Math.Max(.5f, (room - fixedGaps) / Math.Max(1f, used - fixedGaps)) * .995f;
        }
        return font;
    }
}
