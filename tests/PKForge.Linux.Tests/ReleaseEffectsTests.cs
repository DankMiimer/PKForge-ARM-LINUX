using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Domain;
using PKForge.Linux.Native;
using PKForge.Park;
using SkiaSharp;
using Xunit;

namespace PKForge.Linux.Tests;

public sealed class ReleaseEffectsTests
{
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Evolution_skip_or_suspend_releases_its_modal_input_and_expiry_timer(bool suspend)
    {
        using var fixture = new Fixture();
        var baseline = UiDispatcher.Instance.ActiveTimerCount;
        var animation = EvolutionOverlay.PlayAsync(fixture.Host, Choice(), fixture.Sprites, () => fixture.Time);
        Assert.False(animation.IsCompleted); fixture.Draw();
        if (suspend) PKForge.App.App.Suspend(); else Assert.True(fixture.Router.Dispatch(PadButton.Y));
        await animation.WaitAsync(TimeSpan.FromSeconds(2)); UiDispatcher.Instance.Pump();
        Assert.Empty(fixture.Host.Children); Assert.False(fixture.Router.Dispatch(PadButton.A));
        Assert.Equal(baseline, UiDispatcher.Instance.ActiveTimerCount);
    }
    [Fact]
    public async Task Reduced_motion_does_not_claim_the_pad_or_create_an_evolution_timer()
    {
        using var fixture = new Fixture(); MotionSetting.Level = MotionLevel.Reduced;
        var baseline = UiDispatcher.Instance.ActiveTimerCount;
        await EvolutionOverlay.PlayAsync(fixture.Host, Choice(), fixture.Sprites, () => fixture.Time);
        Assert.Empty(fixture.Host.Children); Assert.False(fixture.Router.Dispatch(PadButton.A));
        Assert.Equal(baseline, UiDispatcher.Instance.ActiveTimerCount);
    }
    [Fact]
    public async Task Repeated_portals_use_one_surface_and_release_their_timers_when_skipped()
    {
        using var fixture = new Fixture(); PortalOverlay.Warm(fixture.Host);
        var baseline = UiDispatcher.Instance.ActiveTimerCount;
        var anchor = new PortalAnchor(new(480, 300), new(20, 80, 500, 380), () => { }, () => fixture.Host.IsLoaded, () => fixture.Time);
        PortalSource[] source = [new(new(0, 0, 25, "Pika", false, false), 0)];
        for (var cycle = 0; cycle < 5; cycle++)
        {
            Task? animation = null;
            // Warm is deliberately asynchronous. A transfer skips a portal whose art is not ready.
            for (var attempt = 0; attempt < 200; attempt++)
            {
                animation = PortalOverlay.PlayAsync(fixture.Host, anchor, source, fixture.Sprites);
                if (!animation.IsCompleted) break;
                await animation; await Task.Delay(5);
            }
            Assert.NotNull(animation); Assert.False(animation!.IsCompleted);
            fixture.Draw(); Assert.True(fixture.Router.Dispatch(PadButton.B));
            await animation.WaitAsync(TimeSpan.FromSeconds(2)); UiDispatcher.Instance.Pump();
            Assert.Single(fixture.Host.Children); Assert.False(fixture.Host.Children[0].IsVisible);
            Assert.False(fixture.Router.Dispatch(PadButton.A)); Assert.Equal(baseline, UiDispatcher.Instance.ActiveTimerCount);
        }
    }
    private static EvolutionChoice Choice()
    {
        var option = new EvolutionOption(1, 2, 0, "Ivysaur", EvolutionTrigger.LevelUp, "Level 16", true, true, null, null, null, null, "Overgrow", "Overgrow", [], [], []);
        return new(new(1), new(1, 0, "Bulbasaur", "Bulbasaur", false, 16, 3, [], [option], null), option);
    }
    private sealed class Fixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "pkforge-effects-" + Guid.NewGuid().ToString("N"));
        private readonly IPlatformApplication? _previous = IPlatformApplication.Current;
        private readonly ServiceProvider _services;
        private readonly GraphicsDevice _device = GraphicsDevice.CreateHeadless(640, 480);
        public readonly Grid Host = new();
        public readonly GamepadRouter Router = new();
        public readonly Sprites Sprites = new();
        public TimeSpan Time;
        public Fixture()
        {
            ParkPlatform.Configure(_root, Path.Combine(AppContext.BaseDirectory, "assets")); PKForge.App.App.Resume();
            _services = new ServiceCollection().AddSingleton(Router).BuildServiceProvider();
            IPlatformApplication.Current = new PlatformApplication(_services);
            UiDispatcher.Instance.UseAnimationClock(() => Time); MotionSetting.Level = MotionLevel.Full; Draw();
        }
        public void Draw() { Time += TimeSpan.FromMilliseconds(17); UiDispatcher.Instance.Pump(); Host.Draw(_device.CurrentSurface.Canvas, new(0, 0, 640, 480), _device); }
        public void Dispose()
        {
            EvolutionOverlay.Stop(); PortalOverlay.Stop(); PKForge.App.App.Resume(); Host.ReleaseSurface(); UiDispatcher.Instance.Pump();
            UiDispatcher.Instance.UseAnimationClock(() => System.Diagnostics.Stopwatch.GetElapsedTime(0));
            IPlatformApplication.Current = _previous; _device.Dispose(); _services.Dispose(); Sprites.Dispose(); Directory.Delete(_root, true);
        }
    }
    private sealed class Sprites : ISpriteService, IDisposable
    {
        private readonly SKBitmap _bitmap = new(48, 48);
        public Sprites() => _bitmap.Erase(SKColors.White);
        public SKBitmap? GetSprite(SpriteLook look) => _bitmap;
        public void Warm(SpriteLook look, Action onLoaded) => onLoaded();
        public SKBitmap? GetBall(int ball) => null;
        public void WarmBall(int ball, Action onLoaded) => onLoaded();
        public SKBitmap? GetHome(SpriteLook look) => null;
        public void WarmHome(SpriteLook look, Action onLoaded) => onLoaded();
        public bool HomeUnavailable(SpriteLook look) => true;
        public bool TryGetShowdown(SpriteLook look, out AnimatedSprite? sprite) { sprite = null; return true; }
        public void WarmShowdown(SpriteLook look, Action onLoaded) => onLoaded();
        public bool TryGetBdspIcon(SpriteLook look, Action onLoaded, out SKBitmap? icon) { icon = null; return true; }
        public bool TryGetShowdownFront(SpriteLook look, Action onLoaded, out SKBitmap? sprite) { sprite = _bitmap; return true; }
        public bool TryGetShinySprite(SpriteLook look, Action onLoaded, out SKBitmap? sprite) { sprite = _bitmap; return true; }
        public bool TryGetShowdownIcon(SpriteLook look, Action onLoaded, out SKBitmap? sheet, out SKRectI cell) { sheet = null; cell = default; return true; }
        public void ForgetAddOnLookups() { }
        public Task<bool> DownloadPackFileAsync(SpritePack.Entry entry, CancellationToken cancellationToken) => Task.FromResult(false);
        public void Dispose() => _bitmap.Dispose();
    }
}
