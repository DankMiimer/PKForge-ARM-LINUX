using System.Text;
using PKForge.App.Views;
using PKForge.Chrome;
using SkiaSharp;

namespace Microsoft.Maui.Controls;

// Native labels fall back per glyph. An unsupported symbol must not replace the
// pixel face of the rest of a sentence. Keep wrapping and drawing on the same runs.
internal static class LabelText
{
    private static IEnumerable<(string Text, SKTypeface Face)> Runs(string text, SKFont font)
    {
        var primary = font.Typeface ?? PixelFont.Face;
        var run = new StringBuilder(); SKTypeface? previous = null;
        foreach (var rune in text.EnumerateRunes())
        {
            var next = primary.GetGlyph(rune.Value) != 0 ? primary : NativeLabelFonts.ForMissingGlyph(rune.Value);
            if (previous is not null && !ReferenceEquals(previous, next)) { yield return (run.ToString(), previous); run.Clear(); }
            previous = next; run.Append(rune.ToString());
        }
        if (run.Length > 0) yield return (run.ToString(), previous!);
    }
    private static SKFont WithFace(SKFont font, SKTypeface face) => new(face, font.Size)
    {
        Embolden = font.Embolden, Edging = font.Edging, Hinting = font.Hinting,
        LinearMetrics = font.LinearMetrics, Subpixel = font.Subpixel,
        ForceAutoHinting = font.ForceAutoHinting, EmbeddedBitmaps = font.EmbeddedBitmaps,
        ScaleX = font.ScaleX, SkewX = font.SkewX
    };
    public static (float Ascent, float Descent) Metrics(string text, SKFont font)
    {
        // TextView includes the primary font's outer padding. Fallback glyphs
        // extend the line with their rounded FontMetricsInt ascent/descent,
        // rather than the fallback face's outer bounds.
        var ascent = MathF.Floor(font.Metrics.Top); var descent = MathF.Ceiling(font.Metrics.Bottom);
        foreach (var run in Runs(text, font))
        {
            if (ReferenceEquals(run.Face, font.Typeface)) continue;
            using var fallback = WithFace(font, run.Face);
            ascent = Math.Min(ascent, MathF.Floor(fallback.Metrics.Ascent + .5f));
            descent = Math.Max(descent, MathF.Floor(fallback.Metrics.Descent + .5f));
        }
        return (ascent, descent);
    }
    public static float Measure(string text, SKFont font)
    {
        var result = 0f;
        foreach (var run in Runs(text, font))
        {
            using var face = WithFace(font, run.Face);
            var widths = FontDeviceMetrics.Advances(face).Span;
            foreach (var rune in run.Text.EnumerateRunes())
            {
                var glyph = face.Typeface.GetGlyph(rune.Value);
                result += glyph < widths.Length ? widths[glyph] : face.MeasureText(rune.ToString());
            }
        }
        return result;
    }
    public static void Draw(SKCanvas canvas, string text, float x, float y, SKFont font, SKPaint paint)
    {
        foreach (var run in Runs(text, font))
        {
            using var face = WithFace(font, run.Face);
            var widths = FontDeviceMetrics.Advances(face).Span;
            foreach (var rune in run.Text.EnumerateRunes())
            {
                var glyph = face.Typeface.GetGlyph(rune.Value); var character = rune.ToString();
                canvas.DrawText(character, x, y, face, paint);
                x += glyph < widths.Length ? widths[glyph] : face.MeasureText(character);
            }
        }
    }
    public static string Fit(string text, SKFont font, float width)
    {
        if (width <= 0) return "";
        if (Measure(text, font) <= width) return text;
        var scalars = text.EnumerateRunes().ToArray(); var count = scalars.Length;
        while (count > 0 && Measure(string.Concat(scalars.Take(count).Select(r => r.ToString())) + "…", font) > width) count--;
        return string.Concat(scalars.Take(count).Select(r => r.ToString())) + "…";
    }
    public static List<string> Wrap(string text, SKFont font, float width)
    {
        var lines = new List<string>();
        foreach (var paragraph in text.Split('\n'))
        {
            var line = "";
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = line.Length == 0 ? word : line + " " + word;
                if (line.Length > 0 && Measure(candidate, font) > width) { lines.Add(line); line = word; }
                else line = candidate;
            }
            lines.Add(line);
        }
        return lines;
    }
}
