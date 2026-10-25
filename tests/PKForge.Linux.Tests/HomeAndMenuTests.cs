using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Chrome;
using PKForge.Domain;
using PKForge.Linux.Native;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PKForge.Linux.Tests;

public sealed class HomeAndMenuTests
{
    [Theory]
    [InlineData(640, 480)]
    [InlineData(720, 480)]
    [InlineData(1280, 720)]
    public void Home_layout_keeps_all_destinations_visible_and_can_reach_the_last_cartridge(int width, int height)
    {
        var bounds = new SKRect(0, 0, width, height); var layout = HomeLayout.Create(bounds);
        for (var i = 0; i < 5; i++) Assert.True(bounds.Contains(layout.Destination(i)));
        Assert.True(layout.Destinations.Bottom < layout.Footer.Top);
        Assert.True(layout.Shelf.Bottom < layout.Destinations.Top);
        var last = layout.Cartridge(11, layout.CenteredScroll(11, 12));
        Assert.InRange(last.Right, layout.Shelf.Right - .001f, layout.Shelf.Right + .001f);
        Assert.True(layout.Shelf.Contains(last));
    }

    [Fact]
    public void Shelf_text_preserves_the_Android_group_and_normalization_rules()
    {
        DetectedSave Save(string id, string? trainer, EmulatorKind emulator = EmulatorKind.MelonDS) =>
            new(id, id + ".sav", "Poke\u0301mon Emerald", emulator, false, null, 3, trainer, "12:34");
        var ash = Save("one", "Ash"); var misty = Save("two", "Misty", EmulatorKind.DraStic);
        Assert.Equal("Emerald", HomeCardText.Title(ash));
        Assert.Equal("Ash · 12:34", HomeCardText.Trainer([ash]));
        Assert.Equal("Ash · 2 saves", HomeCardText.Trainer([ash, ash with { DocumentId = "other" }]));
        Assert.Equal("Ash, Misty", HomeCardText.Trainer([ash, misty]));
        Assert.Equal("melonDS, DraStic", HomeCardText.Detail([ash, misty]));
        Assert.Equal(SKColor.Parse("#8B7BD8"), HomePaint.SaveColor(null, 3));
        Assert.Equal(SKColor.Parse("#C0457A"), HomePaint.SaveColor("berry", 3));
    }

    [Fact]
    public async Task Actual_menu_uses_column_navigation_and_releases_its_entrance_animation()
    {
        var before = IPlatformApplication.Current;
        var router = new GamepadRouter();
        using var services = new ServiceCollection().AddSingleton(router).BuildServiceProvider();
        IPlatformApplication.Current = new PlatformApplication(services);
        var host = new Grid();
        try
        {
            var result = PadMenu.ShowAsync(host, "Choose", null, Enumerable.Range(1, 6).Select(i => "Choice " + i).ToArray());
            Assert.True(host.HasActiveAnimations);
            router.Dispatch(PadButton.Down); // Six short choices use two columns: 1 -> 3.
            router.Dispatch(PadButton.A);
            Assert.Equal("Choice 3", await result);
            Assert.Empty(host.Children); Assert.False(host.HasActiveAnimations);
        }
        finally { host.ReleaseSurface(); IPlatformApplication.Current = before; }
    }

    [Fact]
    public async Task Confirmation_uses_the_same_default_choice_as_Android()
    {
        var before = IPlatformApplication.Current; var router = new GamepadRouter();
        using var services = new ServiceCollection().AddSingleton(router).BuildServiceProvider();
        IPlatformApplication.Current = new PlatformApplication(services); var host = new Grid();
        try
        {
            var accepted = PadMenu.ConfirmAsync(host, "Confirm", "Test", "Proceed");
            router.Dispatch(PadButton.A); Assert.True(await accepted);
            var cancelled = PadMenu.ConfirmAsync(host, "Confirm", "Test", "Proceed");
            router.Dispatch(PadButton.Down); router.Dispatch(PadButton.A); Assert.False(await cancelled);
            Assert.Empty(host.Children);
        }
        finally { host.ReleaseSurface(); IPlatformApplication.Current = before; }
    }

    [Fact]
    public async Task Tapping_the_rendered_choose_hint_selects_the_option_instead_of_cancelling()
    {
        var before = IPlatformApplication.Current; var router = new GamepadRouter();
        using var services = new ServiceCollection().AddSingleton(router).BuildServiceProvider();
        IPlatformApplication.Current = new PlatformApplication(services);
        using var device = GraphicsDevice.CreateHeadless(640, 480); var host = new Grid();
        try
        {
            host.Draw(device.CurrentSurface.Canvas, new(0, 0, 640, 480), device);
            var result = PadMenu.ShowAsync(host, "Choose", "Test", "First", "Second");
            var overlay = Assert.IsType<Grid>(Assert.Single(host.Children));
            var window = Assert.IsType<SkiaSharp.Views.Maui.Controls.SKCanvasView>(overlay.Children[^1]);
            window.CancelAnimations(); window.Opacity = window.Scale = 1;
            host.Draw(device.CurrentSurface.Canvas, new(0, 0, 640, 480), device);
            (string, string)[] hints = [("A", "Choose"), ("B", "Cancel")];
            var footer = new SKRect(10, window.CanvasSize.Height - 44, window.CanvasSize.Width - 10, window.CanvasSize.Height - 7);
            using var font = AppChromePaint.HintFont(footer, hints, PixelFont.Face, 13);
            var hit = PksmPaint.HintBarHitRects(footer, hints, font)[0];
            var point = new SKPoint(window.Bounds.Left + hit.MidX, window.Bounds.Top + hit.MidY);
            Assert.True(host.HandleTouch(new(1, SKTouchAction.Pressed, point)));
            Assert.True(host.HandleTouch(new(1, SKTouchAction.Released, point)));
            Assert.Equal("First", await result);
        }
        finally { host.ReleaseSurface(); IPlatformApplication.Current = before; }
    }
}
