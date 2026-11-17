using PKForge.App.Views;
using PKForge.Chrome;
using PKForge.Linux.Workspace;
using SkiaSharp;
using Xunit;

namespace PKForge.Linux.Tests;

public sealed class TallScreenLayoutTests
{
    // The wells StorageScreen offers on each screen: RGDS 640×480, RG34XXSP 720×480, RGCubeXX 720×720.
    [Theory]
    [InlineData(16, 70, 630.67f, 426.67f)]
    [InlineData(18, 75.5f, 709.5f, 425.25f)]
    public void Box_well_is_unchanged_on_the_reference_handhelds(float left, float top, float right, float bottom)
    {
        var available = new SKRect(left, top, right, bottom);
        Assert.Equal(available, TallScreenLayout.BoxWell(available));
    }

    [Fact]
    public void Box_cells_keep_the_design_shape_on_a_square_screen_and_the_well_is_centered()
    {
        var available = new SKRect(18, 75.5f, 709.5f, 665.25f);
        var well = TallScreenLayout.BoxWell(available);
        Assert.Equal(available.Left, well.Left); Assert.Equal(available.Right, well.Right);
        Assert.True(well.Height < available.Height);
        Assert.InRange(well.Top - available.Top - (available.Bottom - well.Bottom), -1f, 1f);
        var cells = BoxGridRenderer.StorageMetrics(well.Size);
        Assert.InRange(cells.CellWidth / cells.CellHeight, StoragePaint.DesignCellWidth / StoragePaint.DesignCellHeight - .02f,
            StoragePaint.DesignCellWidth / StoragePaint.DesignCellHeight + .02f);
    }

    [Theory]
    [InlineData(640, 480, false)]
    [InlineData(720, 480, false)]
    [InlineData(1280, 720, false)]
    [InlineData(720, 720, true)]
    [InlineData(480, 640, true)]
    public void Only_screens_taller_than_4_by_3_are_tall(int width, int height, bool tall)
    {
        var bounds = new SKRect(0, 0, width, height);
        Assert.Equal(tall, TallScreenLayout.IsTall(bounds));
        Assert.Equal(tall ? 115 : 65, TallScreenLayout.CenteredTop(bounds, 65, 465, 300));
    }

    [Theory]
    [InlineData(640, 480, 1f)]
    [InlineData(720, 480, 1f)]
    [InlineData(1280, 720, 1.5f)]
    [InlineData(720, 720, 1.125f)]
    public void Home_scales_with_the_height_but_no_faster_than_the_width(int width, int height, float unit)
    {
        var layout = HomeLayout.Create(new SKRect(0, 0, width, height));
        Assert.Equal(unit, layout.Unit, 3);
        var tile = layout.Destination(0);
        Assert.True(tile.Width > tile.Height, $"Destination {tile.Width}×{tile.Height} should be wider than tall.");
    }
}
