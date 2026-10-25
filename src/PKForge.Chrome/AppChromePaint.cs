using SkiaSharp;

namespace PKForge.Chrome;

/// <summary>The original DS title/status strips, composed from the shared theme.</summary>
public static class AppChromePaint
{
    public static void Header(SKCanvas canvas, SKRect bounds, string title, SKTypeface face, float unit = 1)
    {
        using var fill = new SKPaint { Color = Pksm.ButtonBlueDeep };
        canvas.DrawRect(bounds.Left, bounds.Top, bounds.Width, 28 * unit, fill);
        using var titleFont = new SKFont(face, 16 * unit);
        PksmPaint.CenterText(canvas, title, bounds.MidX, bounds.Top + 14 * unit, titleFont, ColorTheme.Current.Bright, SKColors.Transparent, SKTextAlign.Center);
        fill.Color = Pksm.Paper; canvas.DrawRect(bounds.Left, bounds.Top + 28 * unit, bounds.Width, 26 * unit, fill);
        using var statusFont = new SKFont(face, 14 * unit);
        var statusLeft = bounds.Left + 12 * unit; var statusY = bounds.Top + 41 * unit;
        PksmPaint.CenterText(canvas, "PKForge", statusLeft, statusY, statusFont, Pksm.Ink, SKColors.Transparent);
        var marker = statusLeft + statusFont.MeasureText("PKForge") + 15 * unit;
        fill.Color = Pksm.InkSoft; canvas.DrawRect(marker, statusY - 3 * unit, 6 * unit, 6 * unit, fill);
        using var flagFont = new SKFont(face, 13.5f * unit);
        PksmPaint.CenterText(canvas, "Offline", marker + 21 * unit, statusY, flagFont, Pksm.InkSoft, SKColors.Transparent);
        fill.Color = Pksm.InkSoft; var battery = SKRect.Create(bounds.Right - 34 * unit, bounds.Top + 35 * unit, 22 * unit, 12 * unit);
        canvas.DrawRoundRect(battery, 2 * unit, 2 * unit, fill);
        fill.Color = Pksm.Paper; canvas.DrawRect(SKRect.Inflate(battery, -2 * unit, -2 * unit), fill);
        fill.Color = Pksm.Legal; canvas.DrawRect(new(battery.Left + 3 * unit, battery.Top + 3 * unit, battery.Right - 8 * unit, battery.Bottom - 3 * unit), fill);
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
