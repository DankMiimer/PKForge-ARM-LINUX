using System.ComponentModel;
using PKForge.App.Services;
using PKForge.Chrome;
using PKForge.Domain;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace PKForge.App.Views;

/// <summary>The lower screen while the park is open: the selected resident's portrait, hearts,
/// mood, tastes and needs, and the park's latest moments (<see cref="ParkCardPaint.PaintJournal"/>).</summary>
public sealed class PokeparkJournalView : SKCanvasView
{
    private readonly PokeparkJournalState? _state;
    private readonly PokeparkSpriteService? _sprites;
    private readonly PropertyChangedEventHandler _changed;
    private SKBitmap? _portraitSource;
    private SKImage? _portrait;

    public PokeparkJournalView()
    {
        var services = IPlatformApplication.Current?.Services;
        _state = services?.GetService<PokeparkJournalState>();
        _sprites = services?.GetService<PokeparkSpriteService>();
        // The park sets the needs four times a second, each time a new array, so every update read
        // as a change and the whole journal was painted again. Their icons change only when a need
        // crosses a half icon; anything else that changes repaints as before.
        _changed = (_, e) =>
        {
            if (e.PropertyName == nameof(PokeparkJournalState.Needs) && !NeedsLookDifferent()) return;
            MainThread.BeginInvokeOnMainThread(InvalidateSurface);
        };
        if (_state is not null) _state.PropertyChanged += _changed;
        PaintSurface += PaintGuard.Wrap("PokeparkJournalView.Paint", Paint);
    }

    /// <summary>Stops listening to the park; call when the lower screen lets go of the view.</summary>
    public void Detach()
    {
        if (_state is not null) _state.PropertyChanged -= _changed;
        _portrait?.Dispose();
        _portrait = null;
    }

    private int[] _paintedNeeds = [];
    private bool NeedsLookDifferent()
    {
        var needs = _state?.Needs ?? [];
        if (needs.Length != _paintedNeeds.Length) return true;
        for (var i = 0; i < needs.Length; i++)
            if (ParkCardPaint.NeedHalves(needs[i]) != _paintedNeeds[i]) return true;
        return false;
    }

    private void Paint(object? sender, SKPaintSurfaceEventArgs e)
    {
        _paintedNeeds = [.. (_state?.Needs ?? []).Select(ParkCardPaint.NeedHalves)];
        var c = e.Surface.Canvas;
        // Designed for a 1240×1080 lower screen. A wider, shorter one (the RG DS above its hint
        // bar, 640×424) keeps the needs and the RIGHT NOW, LIKES and BONDS lines on it: they need
        // about 860 design pixels of height. Taller screens are laid out exactly as before.
        var unit = Math.Min(e.Info.Width / 1240f, e.Info.Height / 860f);
        PksmPaint.LogoGrid(c, new SKRect(0, 0, e.Info.Width, e.Info.Height), 24 * unit, 3 * unit);
        if (_state?.Resident is not { } mon) return;
        var bitmap = _sprites?.GetPortrait(mon.Species, mon.Form, mon.Shiny, ParkLife.PortraitFor(_state.Mood));
        if (!ReferenceEquals(bitmap, _portraitSource))
        {
            _portrait?.Dispose();
            _portrait = bitmap is null ? null : SKImage.FromBitmap(bitmap);
            _portraitSource = bitmap;
        }
        var card = new ParkCardPaint.Card(mon.Name, mon.Shiny, _state.Personality, _state.Mood, _state.Activity, _state.Likes,
            _state.Needs, _state.Hearts);
        ParkCardPaint.PaintJournal(c, new SKRect(0, 0, e.Info.Width, e.Info.Height),
            new ParkCardPaint.Journal(card, _state.Taste, _state.Story, _state.Recent, _state.Bonds), _portrait, PixelFont.Face, unit);
    }
}
