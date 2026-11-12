using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Application;
using PKForge.Chrome;
using PKForge.Domain;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace PKForge.Linux.Workspace;

internal sealed class SummaryScreen(WorkspaceHost host, PokemonTarget target) : INativeScreen
{
    public SecondScreenOwner Owner => SecondScreenOwner.Summary;
    public bool ShouldersRepeat => false;
    private PokemonTarget _target = target;
    private MonSummary? _summary;
    private SummaryPage _page;
    private MonSummaryView? _view;
    private bool _overview;
    private readonly List<(SKRect Bounds, int Slot)> _overviewHits = [];
    private readonly List<(SKRect Bounds, PadButton Button)> _hints = [];
    private object? _version;
    private IReadOnlyList<SlotSummary> _slots = [];
    private IReadOnlyList<SlotSummary> Slots
    {
        get
        {
            var version = host.StorageVersion(_target.InBank);
            if (!Equals(version, _version))
            {
                _version = version; _slots = host.StorageSlots(_target.InBank, _target.Box);
                _slotCount = host.StorageSlotCount(_target.InBank, _target.Box);
            }
            return _slots;
        }
    }
    // Read with the slots: counting them rebuilds the save's slot list, too slow for every frame.
    private int _slotCount;
    private int SlotCount { get { _ = Slots; return _slotCount; } }
    private int _sequence;
    private SKRect _previous, _next;
    public void Enter()
    {
        _version = null;
        _view ??= new MonSummaryView(host.StorageSprites);
        _view.SetPage(_page);
        Load();
    }
    /// <summary>As MonSummaryScreen: the quick decode shows at once, its legality verdict arrives second.</summary>
    private void Load()
    {
        var sequence = ++_sequence; var target = _target;
        _summary = host.BuildSummary(target);
        _view!.Show(_summary, legalityPending: _summary is not null, emptyText: "Nothing here");
        if (_summary is null) return;
        _ = Task.Run(() => host.BuildSummary(target, true)).ContinueWith(full => UiDispatcher.Instance.Dispatch(() =>
        {
            if (sequence != _sequence || _view is null) return;
            if (full.IsCompletedSuccessfully && full.Result is { } verdict) _summary = verdict;
            _view.Show(_summary);
            host.Screens.Invalidate();
        }), TaskScheduler.Default);
    }
    public void Leave() { _sequence++; _page = _view?.Page ?? _page; _view?.ReleaseSurface(); _view = null; }
    public void Paint(SKCanvas canvas, SKRect bounds, bool secondary)
    {
        var overview = secondary || _overview && !host.Screens.Dual;
        if (overview) { NativePaint.Background(canvas, bounds, "Summary · Box overview"); Overview(canvas, bounds); return; }
        AppChromePaint.Backdrop(canvas, bounds, 26);
        // MonSummaryScreen's surface: padding 12/10, rows 8 apart; ‹ L capsule, centred header, R › capsule.
        var top = SKRect.Create(bounds.Left + 12, bounds.Top + 10, bounds.Width - 24, 36);
        _previous = SKRect.Create(top.Left, top.Top, 44, top.Height);
        _next = SKRect.Create(top.Right - 44, top.Top, 44, top.Height);
        AppChromePaint.MiniCapsule(canvas, _previous, "‹ L", PixelFont.FallbackFace);
        AppChromePaint.MiniCapsule(canvas, _next, "R ›", PixelFont.FallbackFace);
        var position = SummaryNavigation.Position(_target.Slot, SlotCount,
            i => Slots.Any(s => s.Slot == i && s.Species > 0));
        var context = WorkspaceHost.SummaryContext(_target);
        AppChromePaint.TitleBanner(canvas, new(_previous.Right + 8, top.Top, _next.Left - 8, top.Bottom),
            position.Total > 0 ? $"Summary · {context} · {position.Position} / {position.Total}" : $"Summary · {context}", PixelFont.Face);
        // Kit.HintBar is 37 tall at the bottom padding; PksmPaint.HintBar insets its panel by 3.
        var footer = new SKRect(bounds.Left + 9, bounds.Bottom - 50, bounds.Right - 9, bounds.Bottom - 7);
        if (_view is { } view && host.Screens.DrawingDevice is { } device)
            view.Draw(canvas, new(bounds.Left + 12, top.Bottom + 8, bounds.Right - 12, footer.Top + 3 - 8), device);
        (string, string)[] hints = [("◀▶", "Page"), ("LR", "Pokémon"), ("X", "Edit"), ("B", "Close")];
        using var font = AppChromePaint.HintFont(footer, hints, PixelFont.Face, 15);
        PksmPaint.HintBar(canvas, footer, hints, font);
        _hints.Clear();
        var buttons = new[] { PadButton.Right, PadButton.R, PadButton.X, PadButton.B };
        var rects = PksmPaint.HintBarHitRects(footer, hints, font);
        for (var i = 0; i < buttons.Length; i++) _hints.Add((rects[i], buttons[i]));
    }
    private void Overview(SKCanvas canvas, SKRect bounds)
    {
        var count = SlotCount;
        var columns = _target.Box < 0 ? 2 : 6; var rows = (count + columns - 1) / columns;
        var width = (bounds.Width - 24) / columns; var height = Math.Min(70, (bounds.Height - 120) / rows);
        _overviewHits.Clear();
        for (var i = 0; i < count; i++)
        {
            var rect = SKRect.Create(12 + i % columns * width, 65 + i / columns * height, width - 4, height - 4);
            using var fill = new SKPaint { Color = ColorTheme.Current.Well }; canvas.DrawRoundRect(rect, 4, 4, fill);
            if (i == _target.Slot) StoragePaint.CursorPool(canvas, rect, .4f);
            var slot = Slots.FirstOrDefault(s => s.Slot == i);
            if (slot is { Species: > 0 } && host.Sprite(slot) is { } bitmap)
            {
                var scale = Math.Min(rect.Width / bitmap.Width, rect.Height / bitmap.Height);
                canvas.DrawBitmap(bitmap, SKRect.Create(rect.MidX - bitmap.Width * scale / 2, rect.MidY - bitmap.Height * scale / 2, bitmap.Width * scale, bitmap.Height * scale));
            }
            if (i == _target.Slot) StoragePaint.Pointer(canvas, rect, .4f);
            _overviewHits.Add((rect, i));
        }
        NativePaint.Text(canvas, host.StorageBoxName(_target.InBank, _target.Box) + " · " + (_summary?.DisplayName ?? "Empty slot"), 12, bounds.Bottom - 48, 17, ColorTheme.Current.Ink, bounds.Width - 24);
        NativePaint.Footer(canvas, bounds, host.Screens.Dual ? "Touch an occupied slot to view its summary." : "D-pad: Pokémon · A: actions · Select/B: summary");
    }
    private void SelectSlot(int index)
    {
        if (!Slots.Any(s => s.Slot == index && s.Species > 0)) return;
        _page = _view?.Page ?? _page;
        _target = host.StorageTarget(_target.InBank, _target.Box, index); Enter();
    }
    public bool Button(PadButton button)
    {
        if (_overview && !host.Screens.Dual)
        {
            if (button is PadButton.B or PadButton.Select) { _overview = false; return true; }
            if (button is PadButton.Left or PadButton.Right or PadButton.Up or PadButton.Down)
            {
                var count = SlotCount;
                var columns = _target.Box < 0 ? 2 : 6;
                var direction = button is PadButton.Left or PadButton.Up ? -1 : 1;
                var stride = button is PadButton.Up or PadButton.Down ? columns : 1;
                var index = _target.Slot + direction * stride;
                if (index >= 0 && index < count && Slots.Any(s => s.Slot == index && s.Species > 0)) SelectSlot(index);
                else if (SummaryNavigation.Step(_target.Slot, direction, count, i => Slots.Any(s => s.Slot == i && s.Species > 0)) is { } adjacent) SelectSlot(adjacent);
                return true;
            }
        }
        switch (button)
        {
            case PadButton.Left: _view?.TurnPage(-1); break;
            case PadButton.Right: _view?.TurnPage(1); break;
            case PadButton.Up: _view?.ScrollBy(-1); break;
            case PadButton.Down: _view?.ScrollBy(1); break;
            case PadButton.L: case PadButton.R:
                var next = SummaryNavigation.Step(_target.Slot, button == PadButton.L ? -1 : 1, SlotCount, i => Slots.Any(s => s.Slot == i && s.Species is > 0));
                if (next is { } index) SelectSlot(index); break;
            case PadButton.A: case PadButton.X: _ = host.Screens.RunAsync(async () => { await host.PokemonMenuAsync(_target); Enter(); }); break;
            case PadButton.Start: host.Screens.Pop(); break;
            case PadButton.Select: if (!host.Screens.Dual) _overview = true; break;
            default: return false;
        }
        return true;
    }
    public void Touch(SKTouchEventArgs touch, bool secondary)
    {
        if (secondary || _overview && !host.Screens.Dual)
        {
            if (touch.ActionType != SKTouchAction.Released) return;
            var slot = _overviewHits.FirstOrDefault(h => h.Bounds.Contains(touch.Location));
            if (slot.Bounds.Width > 0) SelectSlot(slot.Slot);
            else if (!secondary && touch.Location.Y > 430) _overview = false;
            return;
        }
        if (_view?.HandleTouch(touch) == true) return;
        if (touch.ActionType != SKTouchAction.Released) return;
        if (_previous.Contains(touch.Location)) Button(PadButton.L);
        else if (_next.Contains(touch.Location)) Button(PadButton.R);
        var hint = _hints.FirstOrDefault(h => h.Bounds.Contains(touch.Location));
        if (hint.Bounds.Width > 0) { if (hint.Button == PadButton.B) host.Screens.Pop(); else Button(hint.Button); }
    }
}

public sealed partial class WorkspaceHost
{
    internal MonSummary? BuildSummary(PokemonTarget target, bool legality = false) => Pokemon.Read(target, (s, b, i) => Get<IMonSummaryService>().Build(s, b, i, legality));
    internal Task ShowSummaryDetailsAsync(PokemonTarget target) => InspectAsync("All summary fields", BuildSummary(target));
    /// <summary>The SummaryDeck context the original pages pass: "Party", "Box 03", "Bank · Box 03".</summary>
    internal static string SummaryContext(PokemonTarget target) =>
        target.InBank ? $"Bank · Box {target.Box + 1:00}" : target.Box < 0 ? "Party" : $"Box {target.Box + 1:00}";
}
