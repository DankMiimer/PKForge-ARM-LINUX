using System.Diagnostics;
using PKForge.App.Services;
using PKForge.Domain;
using PKForge.Park;
using SkiaSharp;

internal static class ParkAnimationCheck
{
    // The caller copies the two cached sprite folders here. Live userdata is never opened for writing.
    public static async Task<int> Run(string directory)
    {
        var root = Path.GetFullPath(directory);
        if (!root.Split(Path.DirectorySeparatorChar).Contains("pkforge-full-test"))
            throw new ArgumentException("Use copied sprite caches below pkforge-full-test.");
        ParkPlatform.Configure(root, root);
        var sprites = new PokeparkSpriteService();
        foreach (var (species, pose, animation) in new[] { (277, ParkPose.Fly, "Float"), (232, ParkPose.Roll, "Special0") })
        {
            var folder = Path.Combine(root, "cache", "pokepark-pmd-v1", ParkSprites.Key(species, 0, false)!);
            using var expected = SKBitmap.Decode(Path.Combine(folder, animation + "-Anim.png"))
                ?? throw new InvalidDataException("Missing cached animation: " + animation);
            var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            sprites.Warm(species, 0, false, () => loaded.TrySetResult());
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var watch = Stopwatch.StartNew();
            ParkSpriteFrame? first = null;
            while (watch.Elapsed < TimeSpan.FromSeconds(5))
            {
                var candidate = sprites.GetFrame(species, 0, false, pose, 2, 0);
                if (candidate is not null && SameBitmap(candidate.Bitmap, expected)) { first = candidate; break; }
                await Task.Delay(10);
            }
            if (first is null) throw new InvalidDataException($"Species {species}: {pose} did not select cached {animation}.");
            for (var direction = 0; direction < 8; direction++)
            for (var time = 0; time < 2000; time += 20)
            {
                var frame = sprites.GetFrame(species, 0, false, pose, direction, time)!;
                if (!SameBitmap(frame.Bitmap, expected) || frame.Source.Top != direction * frame.Source.Height)
                    throw new InvalidDataException($"Species {species}: animation/direction changed at {time} ms.");
            }
            var width = (int)first.Source.Width; var height = (int)first.Source.Height;
            using var strip = new SKBitmap(width * 8, height);
            strip.Erase(SKColors.Transparent);
            using (var canvas = new SKCanvas(strip))
            for (var index = 0; index < 8; index++)
            {
                var frame = sprites.GetFrame(species, 0, false, pose, 2, index * 70)!;
                canvas.DrawBitmap(frame.Bitmap, frame.Source, SKRect.Create(index * width, 0, width, height));
            }
            using var png = strip.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(root, $"{species}-{pose}.png"), png.ToArray());
            Console.WriteLine($"ANIMATION PASS: {species} {pose} selects {animation}; 8 directions × 100 timestamps retain facing.");
        }
        return 0;
    }

    private static bool SameBitmap(SKBitmap actual, SKBitmap expected) =>
        actual.Width == expected.Width && actual.Height == expected.Height && actual.GetPixelSpan().SequenceEqual(expected.GetPixelSpan());
}
