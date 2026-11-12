using SkiaSharp;

namespace PKForge.Chrome;

/// <summary>Pixel art fits in the framebuffer, after the UI's design-unit transform.</summary>
public static class PixelSpritePaint
{
    public static SKRect Fit(SKSize source, SKRect available)
    {
        // Inset fractional edges so rounding cannot cross the caller's allocation.
        var area = new SKRect(MathF.Ceiling(available.Left), MathF.Ceiling(available.Top),
            MathF.Floor(available.Right), MathF.Floor(available.Bottom));
        if (source.Width <= 0 || source.Height <= 0 || area.Width <= 0 || area.Height <= 0) return SKRect.Empty;
        var fit = Math.Min(area.Width / source.Width, area.Height / source.Height);
        var scale = fit >= 1 ? MathF.Floor(fit) : fit;
        var width = source.Width * scale; var height = source.Height * scale;
        var x = Math.Clamp(MathF.Round(area.MidX - width / 2), area.Left, area.Right - width);
        var y = Math.Clamp(MathF.Round(area.MidY - height / 2), area.Top, area.Bottom - height);
        return SKRect.Create(x, y, width, height);
    }

    public static void Draw(SKCanvas canvas, SKImage image, SKRect available)
    {
        var target = Fit(new(image.Width, image.Height), canvas.TotalMatrix.MapRect(available));
        if (target.IsEmpty) return;
        var saved = canvas.Save();
        try
        {
            // The clip is already in device space. Avoid scaling a whole-number
            // design-space sprite a second time through a fractional UI matrix.
            canvas.ResetMatrix();
            canvas.DrawImage(image, target, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
        }
        finally { canvas.RestoreToCount(saved); }
    }
}
