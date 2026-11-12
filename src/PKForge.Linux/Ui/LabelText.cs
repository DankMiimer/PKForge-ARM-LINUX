using System.Text;
using PKForge.App.Views;
using SkiaSharp;

namespace Microsoft.Maui.Controls;

// Native labels fall back per glyph. An unsupported symbol must not replace the
// pixel face of the rest of a sentence. Keep wrapping and drawing on the same runs.
internal static class LabelText
{
    private static IEnumerable<(string Text, bool Fallback)> Runs(string text)
    {
        var run = new StringBuilder(); bool? fallback = null;
        foreach (var rune in text.EnumerateRunes())
        {
            var next = PixelFont.Face.GetGlyph(rune.Value) == 0;
            if (fallback is not null && fallback != next) { yield return (run.ToString(), fallback.Value); run.Clear(); }
            fallback = next; run.Append(rune.ToString());
        }
        if (run.Length > 0) yield return (run.ToString(), fallback!.Value);
    }
    private static SKFont Fallback(SKFont font) => new(PixelFont.FallbackFace, font.Size) { Embolden = font.Embolden };
    public static (float Ascent, float Descent) Metrics(string text, SKFont font)
    {
        var metrics = font.Metrics;
        if (!text.EnumerateRunes().Any(rune => PixelFont.Face.GetGlyph(rune.Value) == 0)) return (metrics.Ascent, metrics.Descent);
        using var fallback = Fallback(font); var other = fallback.Metrics;
        return (Math.Min(metrics.Ascent, other.Ascent), Math.Max(metrics.Descent, other.Descent));
    }
    public static float Measure(string text, SKFont font)
    {
        using var fallback = Fallback(font);
        return Runs(text).Sum(run => (run.Fallback ? fallback : font).MeasureText(run.Text));
    }
    public static void Draw(SKCanvas canvas, string text, float x, float y, SKFont font, SKPaint paint)
    {
        using var fallback = Fallback(font);
        foreach (var run in Runs(text))
        {
            var face = run.Fallback ? fallback : font;
            canvas.DrawText(run.Text, x, y, face, paint); x += face.MeasureText(run.Text);
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
