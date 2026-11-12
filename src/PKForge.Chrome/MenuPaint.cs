using SkiaSharp;

namespace PKForge.Chrome;

public static class MenuPaint
{
    public static int Columns(int count, int longestLabel) => count switch
    {
        <= 4 => 1, <= 12 => longestLabel > 24 ? 1 : 2, _ => longestLabel > 16 ? 2 : 3,
    };
    public static float ButtonHeight(int columns) => columns == 1 ? 58 : columns == 2 ? 52 : 46;

    /// <summary>The original DsFolderButton row, shared by Android and SDL.</summary>
    public static void Row(SKCanvas canvas, SKRect rect, bool selected)
    {
        using var fill = new SKPaint
        {
            IsAntialias = true,
            Shader = selected ? SKShader.CreateLinearGradient(new(rect.Left, rect.Top), new(rect.Left, rect.Bottom),
                [EditorPaint.ChipTop, EditorPaint.ChipBottom], SKShaderTileMode.Clamp) : null,
            Color = EditorPaint.Band.WithAlpha(200),
        };
        canvas.DrawRoundRect(rect, 6, 6, fill);
        using var rim = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = selected ? 2f : 1.2f,
            Color = selected ? EditorPaint.Cyan : StoragePaint.WellEdge };
        canvas.DrawRoundRect(SKRect.Inflate(rect, -.6f, -.6f), 6, 6, rim);
    }
}
