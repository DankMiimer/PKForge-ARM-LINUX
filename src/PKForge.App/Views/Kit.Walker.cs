using SkiaSharp;
using SkiaSharp.Views.Maui.Controls;

namespace PKForge.App.Views;

public static partial class Kit
{
    /// <summary>Small friendly species that wander across the status bar.</summary>
    private static readonly int[] TickerWalkers = [25, 133, 175, 39, 52, 54, 7, 4, 1, 152, 158, 255];
    private static readonly Random TickerRandom = new();

    /// <summary>
    /// The strolling-Pokémon easter egg on its own transparent strip: a little mon walks
    /// the width, bobbing; a different one starts each lap. Place anywhere.
    /// </summary>
    public static View WalkerStrip(double height = 24)
    {
        var walker = new SKCanvasView { InputTransparent = true, HeightRequest = height };
        var species = TickerWalkers[TickerRandom.Next(TickerWalkers.Length)];
        var x = -40f;
        var tick = 0;
        Services.ISpriteService? sprites = null;
        // Cached bitmap wrapper per species — never decode or re-wrap per frame.
        SKImage? image = null;
        var imageSpecies = -1;

        void DropImage()
        {
            image?.Dispose();
            image = null;
            imageSpecies = -1;
        }

        walker.PaintSurface += (_, args) =>
        {
            var canvas = args.Surface.Canvas;
            canvas.Clear(SKColors.Transparent);
            sprites ??= IPlatformApplication.Current?.Services.GetService<Services.ISpriteService>();
            if (imageSpecies != species)
            {
                DropImage();
                var bitmap = sprites?.GetSprite(species, 0, false);
                if (bitmap is null)
                {
                    sprites?.Warm(species, 0, false,
                        () => MainThread.BeginInvokeOnMainThread(walker.InvalidateSurface));
                    return;
                }
                image = SKImage.FromBitmap(bitmap);
                imageSpecies = species;
            }
            if (image is null) return;
            var size = args.Info.Height * 0.94f;
            var scale = Math.Min(size / image.Width, size / image.Height);
            var w = image.Width * scale;
            var h = image.Height * scale;
            var bob = (float)Math.Abs(Math.Sin(tick / 3.0)) * args.Info.Height * 0.08f;
            var y = (args.Info.Height - h) / 2f - bob;
            // Sprites face left; the stroll goes right, so mirror around the sprite center.
            canvas.Save();
            canvas.Scale(-1, 1, x + w / 2, 0);
            canvas.DrawImage(image, new SKRect(x, y, x + w, y + h),
                new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
            canvas.Restore();
        };

        // The stroll timer only runs while the strip is actually on screen.
        IDispatcherTimer? timer = null;
        walker.Loaded += (_, _) =>
        {
            if (timer is not null) return;
            timer = walker.Dispatcher.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(90);
            timer.Tick += (_, _) =>
            {
                tick++;
                x += 2.2f;
                if (x > (float)walker.CanvasSize.Width + 40f)
                {
                    x = -40f;
                    species = TickerWalkers[TickerRandom.Next(TickerWalkers.Length)];
                }
                walker.InvalidateSurface();
            };
            timer.Start();
        };
        walker.Unloaded += (_, _) =>
        {
            timer?.Stop();
            timer = null;
            DropImage();
        };
        return walker;
    }

}
