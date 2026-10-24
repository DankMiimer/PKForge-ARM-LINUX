using System.Diagnostics;
using PKForge.App.Services;
using PKForge.Domain;
using PKForge.Park;
using SkiaSharp;
using Xunit;

namespace PKForge.Park.Tests;

public sealed class ParkAnimationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pkforge-animation-tests", Guid.NewGuid().ToString("N"));
    private readonly PokeparkSpriteService _sprites = new();
    private static readonly string[] Names = ["Walk", "Idle", "Float", "FlapAround", "Rotate", "Special0", "Tumble", "Hover"];

    public ParkAnimationTests() => ParkPlatform.Configure(_root, _root);

    private async Task Warm(int species, params string[] animations)
    {
        var folder = Path.Combine(_root, "cache", "pokepark-pmd-v1", ParkSprites.Key(species, 0, false)!);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "AnimData.xml"), "<AnimData><Anims>" + string.Concat(animations.Select(name =>
            $"<Anim><Name>{name}</Name><FrameWidth>2</FrameWidth><FrameHeight>2</FrameHeight><Durations><Duration>1</Duration><Duration>1</Duration></Durations></Anim>")) + "</Anims></AnimData>");
        File.WriteAllText(Path.Combine(folder, "credits.txt"), "");
        foreach (var name in animations)
        {
            using var bitmap = new SKBitmap(4, 16);
            for (var y = 0; y < 16; y++)
            for (var x = 0; x < 4; x++)
                bitmap.SetPixel(x, y, new SKColor((byte)(Array.IndexOf(Names, name) + 1), (byte)(y / 2), (byte)(x / 2)));
            using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(folder, name + "-Anim.png"), png.ToArray());
            // A cached transparent shadow keeps these tests fully offline.
            bitmap.Erase(SKColors.Transparent);
            using var shadow = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            File.WriteAllBytes(Path.Combine(folder, name + "-Shadow.png"), shadow.ToArray());
        }
        var loaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _sprites.Warm(species, 0, false, () => loaded.TrySetResult());
        await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private async Task WaitFor(int species, ParkPose pose, string animation)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < TimeSpan.FromSeconds(5))
        {
            var frame = _sprites.GetFrame(species, 0, false, pose, 0, 0);
            if (frame is not null && Pixel(frame).Red == Array.IndexOf(Names, animation) + 1) return;
            await Task.Delay(10);
        }
        Assert.Fail($"{pose} never selected {animation} for species {species}.");
    }

    private static SKColor Pixel(ParkSpriteFrame frame) => frame.Bitmap.GetPixel((int)frame.Source.Left, (int)frame.Source.Top);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Flight_keeps_its_direction_without_loading_the_turning_attack(bool hasHover)
    {
        string[] animations = hasHover ? ["Walk", "Idle", "Float", "FlapAround", "Hover"] : ["Walk", "Idle", "Float", "FlapAround"];
        await Warm(277, animations);
        var selected = hasHover ? "Hover" : "Float";
        await WaitFor(277, ParkPose.Fly, selected);
        for (var direction = 0; direction < 8; direction++)
        foreach (var elapsed in new long[] { 0, 16, 32, 1000 })
        {
            var frame = Assert.IsType<ParkSpriteFrame>(_sprites.GetFrame(277, 0, false, ParkPose.Fly, direction, elapsed));
            var pixel = Pixel(frame);
            Assert.Equal(Array.IndexOf(Names, selected) + 1, pixel.Red);
            Assert.Equal(direction, pixel.Green);
            Assert.Equal(elapsed / 16 % 2, pixel.Blue);
        }
    }

    [Fact]
    public async Task Donphan_uses_its_rolling_frames_and_still_has_deliberate_spin_actions()
    {
        await Warm(232, "Walk", "Idle", "Special0", "Tumble", "Rotate");
        await WaitFor(232, ParkPose.Roll, "Special0");
        var first = Pixel(_sprites.GetFrame(232, 0, false, ParkPose.Roll, 6, 0)!);
        var next = Pixel(_sprites.GetFrame(232, 0, false, ParkPose.Roll, 6, 48)!);
        Assert.Equal(6, first.Green); Assert.Equal(6, next.Green);
        Assert.Equal(0, first.Blue); Assert.Equal(1, next.Blue); // Rolling loops, rather than freezing a one-shot pose.
        await WaitFor(232, ParkPose.Spin, "Rotate");
    }

    [Fact]
    public async Task Other_rollers_do_not_use_an_unrelated_species_specific_animation()
    {
        await Warm(74, "Walk", "Idle", "Special0", "Tumble", "Rotate");
        await WaitFor(74, ParkPose.Roll, "Tumble");
    }

    [Fact]
    public async Task Missing_flight_and_rolling_animations_fall_back_without_spinning()
    {
        await Warm(232, "Walk", "Idle", "FlapAround", "Rotate");
        await WaitFor(232, ParkPose.Roll, "Walk");
        await WaitFor(232, ParkPose.Fly, "Idle");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
