using PKForge.App.Services;
using PKForge.Linux;
using PKForge.Domain;

namespace PKForge.App.Views;

public sealed partial class PokeparkPage
{
    public View ParkView => _canvas.View;
    public Grid ControlRoot => _host;
    public PokeparkScene Scene => _scene;
    public bool MenuOpen => _menuOpen;
    public TimeSpan ParkPaintInterval { get => _canvas.PaintInterval; set => _canvas.PaintInterval = value; }
    public void RefreshRoster() => Reload();

    private Grid? _upperOverlay;
    /// <summary>
    /// Two screens: the park's floating pills (the status, and what the selected resident is
    /// doing) go over the park on the upper screen, where a single screen shows them too. On the
    /// lower screen they covered the journal's last lines, and the journal already says what the
    /// resident is doing.
    /// </summary>
    public void UseUpperScreenForPills(bool dual)
    {
        var below = _bottom.Children.Contains(_nowPill);
        if (dual && below)
        {
            _bottom.Remove(_nowPill); _host.Remove(_statusPill);
            _nowPill.VerticalOptions = LayoutOptions.End;
            _upperOverlay ??= new Grid { InputTransparent = true };
            _upperOverlay.Add(_statusPill); _upperOverlay.Add(_nowPill);
        }
        else if (!dual && !below && _upperOverlay is not null)
        {
            _upperOverlay.Remove(_statusPill); _upperOverlay.Remove(_nowPill);
            _nowPill.VerticalOptions = LayoutOptions.Fill;
            // Back where the page put them: the status just above the park, the pill above the hints.
            _host.Children.Insert(1, _statusPill);
            _bottom.Children.Insert(0, _nowPill);
        }
    }
    private ReplayedOverlay? _replayedOverlay;
    /// <summary>What the upper screen draws over the park on two screens; nothing while the
    /// screensaver hides the pills, as it does on one screen. Laid out and drawn afresh only when
    /// the pills change: at 60 frames a second doing it every frame took 1.8 ms of each frame.</summary>
    public View? UpperOverlay => _asleep || _upperOverlay is null ? null
        : _replayedOverlay ??= new ReplayedOverlay(_upperOverlay, () => (_now.Text, _elsewhere.Text, _elsewhere.IsVisible, _nowPill.IsVisible,
            _status.Text, _statusPill.IsVisible, _statusPill.Opacity));
    public void ReleaseUpperOverlay() { if (_replayedOverlay is not null) _replayedOverlay.ReleaseSurface(); else _upperOverlay?.ReleaseSurface(); }
    public void PrepareBenchmark(ParkDayPhase? phase, ParkWeather? weather, bool showPerformance)
    {
        _scene.PhaseOverride = phase; _scene.WeatherOverride = weather;
        _scene.ShowPerformance = showPerformance;
    }
    private async Task<bool> HandleLinuxOptionAsync(string? choice)
    {
        var host = IPlatformApplication.Current!.Services.GetRequiredService<ParkLibrary>();
        if (choice == "Open save / Bank")
        {
            Disappear(); IPlatformApplication.Current!.Services.GetRequiredService<PKForge.Linux.Workspace.WorkspaceHost>().OpenHome();
            return true;
        }
        if (choice == "Quit PKForge") { host.QuitRequested = true; return true; }
        if (choice == "Download this park's assets")
        {
            await host.DownloadAsync(_walking, _scene.Residents); return true;
        }
        return false;
    }
    private Task FindLinuxSourceAsync(ParkPokemon mon) =>
        IPlatformApplication.Current!.Services.GetRequiredService<ParkLibrary>().ShowSourceAsync(mon);
}

/// <summary>Draws a view tree once into a picture and replays it until what it shows (a key from
/// the caller), its place or an animation in it changes.</summary>
internal sealed class ReplayedOverlay(View content, Func<object> state) : View
{
    private SkiaSharp.SKPicture? _picture;
    private object? _drawnFor;
    private SkiaSharp.SKRect _drawnIn;
    internal int Recordings { get; private set; }
    protected override void DrawContent(SkiaSharp.SKCanvas canvas, PKForge.Linux.Native.GraphicsDevice device, View? excluded)
    {
        var key = state();
        if (_picture is null || _drawnIn != Bounds || !Equals(key, _drawnFor) || content.HasActiveAnimations)
        {
            using var recorder = new SkiaSharp.SKPictureRecorder();
            content.Draw(recorder.BeginRecording(Bounds), Bounds, device, excluded);
            _picture?.Dispose(); _picture = recorder.EndRecording(); _drawnFor = key; _drawnIn = Bounds; Recordings++;
        }
        canvas.DrawPicture(_picture);
    }
    public override void ReleaseSurface() { _picture?.Dispose(); _picture = null; content.ReleaseSurface(); base.ReleaseSurface(); }
}
