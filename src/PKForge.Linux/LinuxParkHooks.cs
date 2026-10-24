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
    public void RefreshRoster() => Reload();
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
