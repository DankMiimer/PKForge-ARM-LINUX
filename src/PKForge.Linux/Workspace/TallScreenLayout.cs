using PKForge.App.Views;
using PKForge.Chrome;
using SkiaSharp;

namespace PKForge.Linux.Workspace;

/// <summary>
/// Layout for screens taller than the 4:3 and 3:2 panels the views were drawn for, such as
/// the 720×720 RGCubeXX. Screens still fill the whole panel, but nothing stretches past the
/// shape the designer gave it: the extra height goes around it instead.
/// </summary>
internal static class TallScreenLayout
{
    /// <summary>Taller than the 4:3 RGDS screen, such as a square one.</summary>
    public static bool IsTall(SKRect bounds) => bounds.Height > bounds.Width * .8f;

    /// <summary>
    /// Where content <paramref name="height"/> tall starts between <paramref name="top"/> and
    /// <paramref name="bottom"/>: centered on a tall screen, at the top otherwise, as before.
    /// </summary>
    public static float CenteredTop(SKRect bounds, float top, float bottom, float height) =>
        IsTall(bounds) ? top + MathF.Round(Math.Max(0, (bottom - top - height) / 2)) : top;

    /// <summary>
    /// A save or Bank box's well within <paramref name="available"/>. A box cell is wider than
    /// tall (the designer's 160×134); on a tall screen the cells would otherwise grow taller than
    /// wide. The well then keeps its full width, takes the height that gives cells the design
    /// shape, and is centered in the space it was given. Wide screens are unchanged.
    /// </summary>
    public static SKRect BoxWell(SKRect available)
    {
        if (available.Width <= 0 || available.Height <= 0) return available;
        var cell = BoxGridRenderer.StorageMetrics(available.Size);
        var designHeight = cell.CellWidth * StoragePaint.DesignCellHeight / StoragePaint.DesignCellWidth;
        if (cell.CellHeight <= designHeight) return available;
        // The well's vertical insets scale with its height (16 of 702 design pixels each side).
        var height = designHeight * BoxGridRenderer.Rows / (1 - 2 * StoragePaint.WellInsetY / 702f);
        var top = MathF.Round(available.Top + (available.Height - height) / 2);
        return new SKRect(available.Left, top, available.Right, top + MathF.Round(height));
    }
}
