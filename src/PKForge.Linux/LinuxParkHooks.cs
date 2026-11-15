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
    /// <summary>What the upper screen draws over the park on two screens; nothing while the
    /// screensaver hides the pills, as it does on one screen.</summary>
    public View? UpperOverlay => _asleep || _upperOverlay is null ? null : _upperOverlay;
    public void ReleaseUpperOverlay() => _upperOverlay?.ReleaseSurface();
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
