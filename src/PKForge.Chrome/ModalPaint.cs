using SkiaSharp;

namespace PKForge.Chrome;

/// <summary>Original Kit modal geometry and colors, without display or input ownership.</summary>
public static class ModalPaint
{
    public const float HeaderHeight = 33, ProgressHeight = 16, CapsuleHeight = 34;

    public static float CapsuleWidth(string text, SKTypeface face)
    {
        using var font = new SKFont(face, 14);
        return font.MeasureText(text) + 28;
    }

    public static void Progress(SKCanvas canvas, SKRect bounds, double fraction, SKColor color)
    {
        var track = SKRect.Create(bounds.Left, bounds.MidY - 2, bounds.Width, 4);
        using var paint = new SKPaint { Color = SKColors.Black.WithAlpha(41) };
        canvas.DrawRect(track, paint); paint.Color = color;
        var progress = double.IsFinite(fraction) ? (float)Math.Clamp(fraction, 0, 1) : 0;
        canvas.DrawRect(new(track.Left, track.Top, track.Left + track.Width * progress, track.Bottom), paint);
    }

    public static void Capsule(SKCanvas canvas, SKRect bounds, string text, SKTypeface face, SKColor color, SKColor ink, bool pressed)
    {
        const int drop = 2;
        var sink = pressed ? drop : 0;
        using var paint = new SKPaint { Color = Pksm.LogoVoid.WithAlpha(230), IsAntialias = true };
        canvas.DrawRoundRect(new(bounds.Left, bounds.Top + drop, bounds.Right, bounds.Bottom), 4, 4, paint);
        var body = new SKRect(bounds.Left, bounds.Top + sink, bounds.Right, bounds.Bottom - drop + sink);
        using var gradient = SKShader.CreateLinearGradient(new(0, body.Top), new(0, body.Bottom),
            [pressed ? PksmPaint.Darker(color, .02f) : PksmPaint.Lighter(color, .16f), PksmPaint.Darker(color, pressed ? .12f : .14f)], SKShaderTileMode.Clamp);
        paint.Shader = gradient; canvas.DrawRoundRect(body, 4, 4, paint); paint.Shader = null;
        paint.Color = Pksm.LogoGrid; paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 2;
        canvas.DrawRoundRect(SKRect.Inflate(body, -1, -1), 4, 4, paint);
        paint.Style = SKPaintStyle.Fill; paint.Color = ColorTheme.Current.Bright.WithAlpha(pressed ? (byte)15 : (byte)56);
        canvas.DrawRect(new(body.Left + 4, body.Top + 2, body.Right - 4, body.Top + 3), paint);
        using var font = new SKFont(face, 14);
        PksmPaint.CenterText(canvas, text, body.MidX, body.MidY, font, ink, Pksm.LogoVoid.WithAlpha(204), SKTextAlign.Center);
    }

    public static void Header(SKCanvas canvas, SKRect bounds, string title, SKTypeface face)
    {
        using var font = new SKFont(face, 16);
        var rect = SKRect.Inflate(bounds, -.75f, -.75f);
        using var path = new SKRoundRect(); path.SetRectRadii(rect, [new(10, 10), new(3, 3), new(3, 3), new(10, 10)]);
        using var gradient = SKShader.CreateLinearGradient(new(0, bounds.Top), new(0, bounds.Bottom), [StoragePaint.BannerTop, StoragePaint.BannerBottom], SKShaderTileMode.Clamp);
        using var paint = new SKPaint { Shader = gradient, IsAntialias = true };
        canvas.DrawRoundRect(path, paint); paint.Shader = null;
        paint.Color = Pksm.Ink; paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 1.5f;
        canvas.DrawRoundRect(path, paint); paint.Style = SKPaintStyle.Fill;
        var baseline = bounds.Top + 6.5f + MathF.Ceiling(-font.Metrics.Ascent);
        paint.Color = Pksm.LogoVoid.WithAlpha(143); canvas.DrawText(title, bounds.Left + 12.5f, baseline + 1, font, paint);
        paint.Color = ColorTheme.Current.Bright; canvas.DrawText(title, bounds.Left + 11.5f, baseline, font, paint);
    }

    public static void Window(SKCanvas canvas, SKRect bounds)
    {
        using var paint = new SKPaint { Color = Pksm.LogoVoid.WithAlpha(140), IsAntialias = true };
        var shadow = bounds; shadow.Offset(3, 3); canvas.DrawRoundRect(shadow, 12, 12, paint);
        using var gradient = SKShader.CreateLinearGradient(new(0, bounds.Top), new(0, bounds.Bottom),
            [PksmPaint.Mix(StoragePaint.Well, StoragePaint.FrameEdge, .25f), StoragePaint.Well], [0, .12f], SKShaderTileMode.Clamp);
        paint.Color = SKColors.White; paint.Shader = gradient; canvas.DrawRoundRect(bounds, 12, 12, paint); paint.Shader = null;
        paint.Color = StoragePaint.FrameEdge; paint.Style = SKPaintStyle.Stroke; paint.StrokeWidth = 2;
        canvas.DrawRoundRect(SKRect.Inflate(bounds, -1, -1), 12, 12, paint);
    }
}
