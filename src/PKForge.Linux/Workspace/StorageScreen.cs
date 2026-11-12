using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Application;
using PKForge.Domain;
using PKForge.Chrome;
using PKForge.Engine;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace PKForge.Linux.Workspace;

/// <summary>
/// A save's boxes and party, or a Bank box, with the original controls. A is the hand: grab,
/// carry across boxes and the party, place or swap. + opens the cursor's menu and B cancels the
/// hand before going back. The save boxes keep X tools, Y save data and − multi-select (the
/// green hand, BoxBrowserPage); the Bank has Y mark, X actions and − summary (BankPage).
/// </summary>
public sealed class StorageScreen(WorkspaceHost workspace, bool bank) : INativeScreen
{
    public SecondScreenOwner Owner => bank ? SecondScreenOwner.Bank : SecondScreenOwner.Box;
    private string? _wallpaperPath;
    private SKImage? _wallpaper;
    public void Leave()
    {
        _wallpaper?.Dispose(); _wallpaper = null; _wallpaperPath = null; _hand.Reset();
        _backdrop?.Dispose(); _backdrop = null; _backdropKey = default;
        _inspector?.ReleaseSurface(); _inspector = null; _selectHeld = false; _rectangle = null;
    }
    public bool IsBank => bank;
    public int Box { get; private set; }
    internal void SelectBox(int box)
    {
        Box = Math.Clamp(box, 0, Math.Max(0, Host.StorageBoxCount(bank) - 1));
        _cursor = 0; Host.Screens.Invalidate();
    }
    internal void RemapBoxes(BankBoxRemap remap)
    {
        var marks = Marks.Where(at => remap.Map(at.Box) is not null).Select(at => new SlotRef(remap.Map(at.Box)!.Value, at.Slot)).ToArray();
        Marks.Clear(); Marks.UnionWith(marks);
        if (_carry is { } carry)
        {
            if (remap.Map(carry.Box) is { } mapped) _carry = new(mapped, carry.Slot);
            else CancelCarry();
        }
        Box = Math.Clamp(remap.Map(Box) ?? Box, 0, Math.Max(0, Host.StorageBoxCount(bank) - 1));
        _rectangle = null; _selectHeld = false; Host.Screens.Invalidate();
    }
    // -1 after a save box turn until the d-pad or A brings the cursor back, as in the original.
    private int _cursor;
    public int Cursor => Math.Max(0, _cursor);
    /// <summary>The organizer's marks: the Bank's Y marks, or the save boxes' multi-select marks.</summary>
    public HashSet<SlotRef> Marks { get; } = [];
    private readonly List<(SKRect Rect, int Slot)> _hits = [];
    private readonly List<(SKRect Rect, PadButton Button)> _hintHits = [];
    private WorkspaceHost Host => workspace;
    private int BoxCount => Shape.BoxCount;
    private int SlotCount => Shape.SlotCount(Box);
    private int Columns => Box < 0 ? 2 : 6;
    private int Rows => Box < 0 ? 3 : 5;
    private int FirstBox => bank || !Shape.HasParty ? 0 : -1;
    // Box and slot counts, the party and the icon set, read once per save or Bank version:
    // each read builds the save's whole slot list, and every frame needed dozens of them.
    private WorkspaceHost.StorageShape? _shape;
    private object? _shapeVersion;
    private WorkspaceHost.StorageShape Shape
    {
        get
        {
            var version = Host.StorageVersion(bank);
            if (_shape is null || !Equals(version, _shapeVersion)) { _shapeVersion = version; _shape = Host.ReadStorageShape(bank); }
            return _shape;
        }
    }
    private int _cachedBox = int.MinValue;
    private object? _version;
    private string _boxName = "";
    private IReadOnlyList<SlotSummary> _slots = [];
    private IReadOnlyList<SlotSummary> _denseSlots = [];
    private IReadOnlyList<EntityDetail?> _party = [];
    private readonly CarryHand _hand = new();
    private (int Box, int Slot, object? Version)? _inspected;
    private MonSummaryView? _inspector;
    private long _partyPulseStart = Environment.TickCount64;
    public void Enter() { _cachedBox = int.MinValue; _inspected = null; _partyPulseStart = Environment.TickCount64; }
    private IReadOnlyList<SlotSummary> Slots
    {
        get
        {
            var version = Host.StorageVersion(bank);
            if (Box != _cachedBox || !Equals(version, _version))
            {
                _cachedBox = Box; _version = version; _slots = Host.StorageSlots(bank, Box);
                _denseSlots = Enumerable.Range(0, 30).Select(index => _slots.FirstOrDefault(s => s.Slot == index && s.Species is > 0)
                    ?? new SlotSummary(Box, index, null, null, false, false)).ToArray();
                if (Box < 0) _party = Host.PartySnapshot();
                _boxName = bank ? "" : Host.StorageBoxName(false, Box);
                _wallpaperFor = bank ? null : Host.StorageWallpaper(Box, BoxCount);
            }
            return _slots;
        }
    }
    private SlotSummary? Selected => _cursor < 0 ? null : Slots.FirstOrDefault(s => s.Slot == _cursor && s.Species is > 0);
    public PokemonTarget Target => Host.StorageTarget(bank, Box, Cursor);

    // ── The hand ──────────────────────────────────────────────────────────────

    private SlotRef? _carry;
    private SlotSummary? _carried;
    private Guid? _carryId;
    // Moves write in the order they were dropped: a quick second drop waits for the first.
    private Task _writing = Task.CompletedTask;
    private bool _dropWaiting;
    /// <summary>The last dropped move's write (headless scripts wait for it).</summary>
    internal Task PendingWrite => _writing;

    /// <summary>A: drop what is in hand, else pick up the Pokémon under the cursor, else open the empty slot's menu.</summary>
    private void Confirm()
    {
        if (_cursor < 0) _cursor = 0;
        if (_carry is not null) { _ = DropAsync(); return; }
        if (Selected is { } slot)
        {
            _carry = new(Box, _cursor); _carried = slot;
            _carryId = bank ? Host.StorageTarget(true, Box, _cursor).BankId : null;
            return;
        }
        Run(() => Host.PokemonMenuAsync(Target));
    }
    private void CancelCarry() { _carry = null; _carried = null; _carryId = null; }
    private async Task DropAsync()
    {
        if (_carry is null || _dropWaiting) return;
        if (!_writing.IsCompleted)
        {
            // The Pokémon stays in hand until the previous move is written.
            _dropWaiting = true;
            try { await _writing; } finally { _dropWaiting = false; }
        }
        if (_carry is not { } from) return;
        var to = new SlotRef(Box, Math.Max(0, _cursor));
        var carried = _carried; var id = _carryId;
        CancelCarry();
        if (from == to) return;
        if (bank)
        {
            if (id is not { } entry) return;
            try { Host.MoveBankEntry(entry, to.Box, to.Slot); }
            catch (Exception refused) when (refused is InvalidOperationException or IOException or UnauthorizedAccessException) { _message = "Move aborted: " + refused.Message; }
            return;
        }
        // As BoxBrowserViewModel.DropAsync: the open save moves at once (the engine's own swap
        // and party rules), so the grid shows it before the write; a refused write reverts it.
        var partySwap = from.Box == -1 && to.Box == -1 && Host.StorageEntity(to) is not null;
        try { Host.MoveLive(from, to); }
        catch (InvalidOperationException refused)
        {
            // The ROM-hack engines refuse a move (a full party) instead of skipping it.
            await Host.Screens.Dialogs.MessageAsync("Move", "Move aborted: " + refused.Message);
            return;
        }
        _cachedBox = int.MinValue;
        // Party swap between two Pokémon: the cards slide past each other as they trade places.
        if (partySwap) PartyView.BeginSwap(from.Slot, to.Slot);
        var write = Host.MoveStorageAsync(from, to, carried);
        _writing = write;
        var error = await write;
        _cachedBox = int.MinValue;
        if (error is not null) await Host.Screens.Dialogs.MessageAsync("Move", error);
    }

    // ── Multi-select (the save boxes' green hand) ─────────────────────────────

    private bool _selectMode;
    private bool _selectHeld;
    private (int From, int To, bool Mark)? _rectangle;
    private bool Marking => !bank && _selectMode;
    private bool MarksShown => bank || _selectMode;
    private IEnumerable<SlotSummary> Occupied => Slots.Where(s => s.Species is > 0);
    private bool PageFullyMarked => Occupied.Any() && Occupied.All(s => Marks.Contains(new(Box, s.Slot)));
    private void EnterSelectMode() { CancelCarry(); _selectMode = true; _selectHeld = false; if (_cursor < 0) _cursor = 0; }
    private void ExitSelectMode() { _selectMode = false; _selectHeld = false; _rectangle = null; Marks.Clear(); }
    /// <summary>Flips one occupied slot's mark; false for an empty slot.</summary>
    private bool ToggleMark(int slot)
    {
        if (!Occupied.Any(s => s.Slot == slot)) return false;
        if (!Marks.Remove(new(Box, slot))) Marks.Add(new(Box, slot));
        return true;
    }
    private void TogglePage()
    {
        if (PageFullyMarked) Marks.RemoveWhere(m => m.Box == Box);
        else foreach (var slot in Occupied) Marks.Add(new(Box, slot.Slot));
    }
    // Hold A and steer: the rectangle from where A went down marks (or unmarks, when the
    // anchor was unmarked by the press) every Pokémon inside it on release.
    private void BeginRectangle(int slot)
    {
        var toggled = ToggleMark(slot);
        _rectangle = (slot, slot, !toggled || Marks.Contains(new(Box, slot)));
    }
    private void CommitRectangle()
    {
        if (_rectangle is not { } r) return;
        _rectangle = null;
        if (r.From == r.To) return;
        foreach (var slot in Occupied.Where(s => StorageRectangle.Contains(r.From, r.To, s.Slot, Columns)).ToArray())
            if (r.Mark) Marks.Add(new(Box, slot.Slot)); else Marks.Remove(new(Box, slot.Slot));
    }
    private bool? RangeMark(int slot) => _rectangle is { } r && StorageRectangle.Contains(r.From, r.To, slot, Columns) ? r.Mark : null;

    // ── Painting ──────────────────────────────────────────────────────────────

    public void Paint(SKCanvas canvas, SKRect bounds, bool secondary)
    {
        Box = Math.Clamp(Box, FirstBox, Math.Max(0, BoxCount - 1));
        _cursor = Math.Clamp(_cursor, bank ? 0 : -1, Math.Max(0, SlotCount - 1));
        var slots = Slots;
        AppChromePaint.Backdrop(canvas, bounds, 26);
        AppChromePaint.StatusStrip(canvas, bounds, PixelFont.Face, statusName: bank ? "Bank" : Host.SaveTitle, status: bank ? "Open" : "Connected");
        if (secondary) { PaintInspector(canvas, bounds); return; }
        var unit = Math.Max(bounds.Width, bounds.Height) / 1920;
        var left = 48 * unit; var right = 28 * unit;
        var bar = SKRect.Create(left, bounds.Top + 26 + 32 * unit, bounds.Width - left - right, 72 * unit);
        using var headerFont = new SKFont(PixelFont.Face, 34 * unit);
        StoragePaint.BoxHeader(canvas, bar, HeaderName, headerFont, Box > 0, Box < BoxCount - 1, unit);
        // BankPage pads its body 20 (BoxBrowserPage 34) and stacks its LCD status line over the footer.
        var well = new SKRect(left, bar.Bottom + 28 * unit, bounds.Right - right,
            bank ? bounds.Bottom - 42 - 6 - StatusHeight - 20 * unit : bounds.Bottom - 42 - 34 * unit);
        var wallpaper = _wallpaperFor;
        if (wallpaper != _wallpaperPath)
        {
            _wallpaper?.Dispose(); _wallpaper = null; _wallpaperPath = wallpaper;
            if (wallpaper is not null && File.Exists(wallpaper) && SKBitmap.Decode(wallpaper) is { } art)
                using (art) { _wallpaper = SKImage.FromBitmap(art); _wallpaperAverage = StoragePaint.AverageColor(art); }
        }
        _hits.Clear();
        var info = new SKImageInfo(Math.Max(1, (int)well.Width), Math.Max(1, (int)well.Height));
        // The original painters clear their own view's canvas: give them a layer, so the
        // well's rounded corners show the grid behind as they do over Android's page.
        canvas.SaveLayer(well, null); canvas.ClipRect(well); canvas.Translate(well.Left, well.Top);
        if (Box < 0)
        {
            // The party deck: the selected card breathes, a carried party Pokémon stays lifted on its card.
            var phase = (float)((Environment.TickCount64 - _partyPulseStart) / 1000.0 * Math.PI * 2 / 1.4);
            PartyView.Paint(canvas, info, Host.StorageSprites, null, _cursor, Host.Screens.Invalidate,
                _carry is { } source ? (source.Box, source.Slot) : null, phase,
                isMarked: _selectMode ? index => Marks.Contains(new(-1, index)) : null, rangeMark: RangeMark, snapshot: _party);
            _hand.Reset();
            // The party painter owns its staggered octagonal hit geometry.
            _partyBounds = well;
        }
        else
        {
            var scene = new BoxGridRenderer.StorageScene(_denseSlots, _cursor, _carry is { } from && from.Box == Box ? from.Slot : null,
                _carry is null ? null : _carried, Marking, index => MarksShown && Marks.Contains(new(Box, index)), RangeMark, Box,
                BdspStyle: Shape.BdspStyle);
            BoxGridRenderer.PaintScene(canvas, info, scene, Host.StorageSprites, Host.Screens.Invalidate, _hand, (c, area, scale) =>
            {
                if (bank) { BankBackdrop(c, area, scale); return; }
                if (_wallpaper is { } art) DrawWallpaper(c, area, art, () => art, _wallpaperAverage, scale);
            });
            var layout = BoxGridRenderer.StorageMetrics(new SKSize(info.Width, info.Height));
            for (var index = 0; index < SlotCount; index++) { var cell = layout.Cell(index); cell.Offset(well.Left, well.Top); _hits.Add((cell, index)); }
        }
        canvas.Restore();
        var footer = new SKRect(0, bounds.Bottom - 42, bounds.Right, bounds.Bottom);
        if (bank)
        {
            var status = new SKRect(12, footer.Top - 6 - StatusHeight, bounds.Right - 12, footer.Top - 6);
            PksmPaint.Panel(canvas, status);
            using var statusFont = new SKFont(PixelFont.Face, 13) { Edging = SKFontEdging.Antialias, Embolden = true };
            using var ink = new SKPaint { Color = Pksm.Ink, IsAntialias = true };
            var key = (Box, _cursor, Marks.Count, _version, _message);
            if (!key.Equals(_statusKey)) { _statusKey = key; _status = _message ?? Host.BankStatus(Box, Cursor, Marks.Count); }
            canvas.DrawText(NativeText.Fit(_status, statusFont, status.Width - 12), status.Left + 6,
                status.MidY - (statusFont.Metrics.Ascent + statusFont.Metrics.Descent) / 2, statusFont, ink);
        }
        var hints = Hints();
        var prompts = hints.Select(h => (h.Key, h.Label)).ToArray();
        using var hintFont = AppChromePaint.HintFont(footer, prompts, PixelFont.Face, 14);
        PksmPaint.HintBar(canvas, footer, prompts, hintFont);
        _hintHits.Clear();
        var rects = PksmPaint.HintBarHitRects(footer, prompts, hintFont);
        for (var i = 0; i < Math.Min(rects.Count, hints.Length); i++) _hintHits.Add((rects[i], hints[i].Button));
    }
    /// <summary>The footer of the active hand: BankPage's, or BoxBrowserPage's storage / multi-select footers.</summary>
    private (string Key, string Label, PadButton Button)[] Hints() => bank
        ? [("A", "Grab", PadButton.A), ("Y", "Mark", PadButton.Y), ("X", "Actions", PadButton.X), ("B", "Back", PadButton.B),
            ("LR", "Box", PadButton.R), ("-", Host.Screens.Dual ? "Page" : "Summary", PadButton.Select), ("+", "Menu", PadButton.Start)]
        : _selectMode
            ? [("A", "Mark · hold for area", PadButton.A), ("Y", PageFullyMarked ? "Unmark box" : "Select box", PadButton.Y),
                ("X", $"Actions ({Marks.Count})", PadButton.X), ("+", "Boxes", PadButton.Start), ("LR", "Box", PadButton.R),
                ("-", "Move mode", PadButton.Select), ("B", Marks.Count > 0 ? "Clear" : "Done", PadButton.B)]
            : [("A", _carry is null ? "Grab" : "Place", PadButton.A), ("-", "Multi-select", PadButton.Select), ("LR", "Box", PadButton.R),
                ("X", "Tools", PadButton.X), ("Y", "Save data", PadButton.Y), ("+", "Menu", PadButton.Start),
                ("B", _carry is null ? "Back" : "Cancel", PadButton.B)];
    private SKRect _partyBounds;
    private string? _wallpaperFor;
    private SKColor _wallpaperAverage;
    /// <summary>BankPage.Paint's backdrop: a wallpaper picture through the save boxes' painter, else a flat tint.</summary>
    private void BankBackdrop(SKCanvas c, SKRect well, float unit)
    {
        if (BankBoxArt.Art(Box, Host.Screens.Invalidate) is { } art)
        {
            DrawWallpaper(c, well, art.Art, () => SKImage.FromBitmap(art.Art), art.Average, unit);
            return;
        }
        using var tint = new SKPaint { Color = PksmPaint.Mix(BankBoxArt.Tone(Box), StoragePaint.Well, 0.7f), IsAntialias = true };
        c.DrawRoundRect(SKRect.Inflate(well, -3 * unit, -3 * unit), 25 * unit, 25 * unit, tint);
    }
    // The styled wallpaper changes only with the art, the well's size, the style or the theme:
    // drawn once into an image, not through its colour filter on every frame.
    private SKImage? _backdrop;
    private (object? Art, SKSizeI Size, StoragePaint.WallpaperStyle Style, int Theme) _backdropKey;
    private void DrawWallpaper(SKCanvas c, SKRect area, object source, Func<SKImage> image, SKColor average, float unit)
    {
        var key = (source, new SKSizeI((int)MathF.Ceiling(area.Width), (int)MathF.Ceiling(area.Height)), BoxBackground.Style, ColorTheme.Version);
        if (_backdrop is null || key != _backdropKey)
        {
            _backdrop?.Dispose(); _backdrop = null;
            using var surface = SKSurface.Create(new SKImageInfo(key.Item2.Width, key.Item2.Height));
            if (surface is null) return;
            var art = image();
            try { StoragePaint.Wallpaper(surface.Canvas, new SKRect(0, 0, area.Width, area.Height), art, average, key.Style, unit); }
            finally { if (!ReferenceEquals(art, source)) art.Dispose(); }
            _backdrop = surface.Snapshot(); _backdropKey = key;
        }
        c.DrawImage(_backdrop, area.Left, area.Top);
    }
    /// <summary>Kit.LcdPanel around a 13 dp pixel label, 6 padding.</summary>
    private const float StatusHeight = 26;
    private (int, int, int, object?, string?) _statusKey = (int.MinValue, 0, 0, null, null);
    private string _status = "";
    // A Bank report ("Nothing to mark…", a refused move) until the cursor or box changes.
    private string? _message;
    /// <summary>The Bank's counter, the save box's own name; multi-select adds the mark counts.</summary>
    private string HeaderName
    {
        get
        {
            if (bank) return BankBoxDecor.Names.Get(Box) is { } name ? $"{Box + 1:00} / {BoxCount:00} · {name}" : $"Box {Box + 1:00} / {BoxCount:00}";
            return _selectMode ? $"{_boxName} · marked {Marks.Count(m => m.Box == Box)}/{Marks.Count}" : _boxName;
        }
    }
    private void PaintInspector(SKCanvas canvas, SKRect bounds)
    {
        _inspector ??= new MonSummaryView(Host.StorageSprites);
        var key = (Box, _cursor, _version);
        if (_inspected != key)
        {
            _inspected = key;
            _inspector.Show(Selected is not null ? Host.BuildSummary(Target) : null,
                emptyText: "Empty slot · A: import or generate a Pokémon");
        }
        if (Host.Screens.DrawingDevice is { } device) _inspector.Draw(canvas, new(12, bounds.Top + 34, bounds.Right - 12, bounds.Bottom - 45), device);
        NativePaint.Footer(canvas, bounds, bank ? "+: menu · −: page" : "+: menu");
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    private void Run(Func<Task> action) => _ = Host.Screens.RunAsync(async () =>
    {
        try { await action(); }
        finally
        {
            // Menus may have moved, released or marked Pokémon: marks only name occupied slots,
            // and marks made from the save tools show the multi-select hand.
            try
            {
                var live = Marks.Select(m => m.Box).Distinct().SelectMany(box => Host.StorageSlots(bank, box))
                    .Where(s => s.Species is > 0).Select(s => new SlotRef(s.Box, s.Slot)).ToHashSet();
                Marks.RemoveWhere(m => !live.Contains(m));
            }
            catch (InvalidOperationException) { Marks.Clear(); } // the save was closed from the menu
            if (!bank && Marks.Count > 0) _selectMode = true;
            _cachedBox = int.MinValue; _inspected = null;
        }
    });
    private void MoveCursor(int dx, int dy)
    {
        var slot = Math.Max(0, _cursor);
        _cursor = Math.Clamp(slot / Columns + dy, 0, Rows - 1) * Columns + Math.Clamp(slot % Columns + dx, 0, Columns - 1);
        _cursor = Math.Min(_cursor, Math.Max(0, SlotCount - 1));
        _message = null;
    }
    private void ChangeBox(int delta)
    {
        _message = null; _rectangle = null;
        if (bank) { Box = Math.Clamp(Box + delta, 0, Math.Max(0, BoxCount - 1)); return; }
        // The party wraps around the boxes: last box, then the party, then box 1 again.
        var next = Box + delta;
        if (next > BoxCount - 1) next = FirstBox;
        else if (next < FirstBox) next = BoxCount - 1;
        if (next == -1 && Box != -1) _partyPulseStart = Environment.TickCount64;
        Box = next; _cursor = -1;
    }
    public bool Button(PadButton button)
    {
        if (_selectMode) return SelectButton(button);
        switch (button)
        {
            case PadButton.Up: MoveCursor(0, -1); break;
            case PadButton.Down: MoveCursor(0, 1); break;
            case PadButton.Left: MoveCursor(-1, 0); break;
            case PadButton.Right: MoveCursor(1, 0); break;
            case PadButton.L: ChangeBox(-1); break;
            case PadButton.R: ChangeBox(1); break;
            case PadButton.A: Confirm(); break;
            case PadButton.B:
                if (bank && Marks.Count > 0) { Marks.Clear(); break; }
                if (_carry is not null) { CancelCarry(); break; }
                return false;
            case PadButton.X: Run(() => Host.StorageToolsAsync(this)); break;
            case PadButton.Y:
                if (!bank) { Run(Host.SaveToolsAsync); break; }
                if (!ToggleMark(Cursor)) _message = "Nothing to mark in an empty slot.";
                break;
            case PadButton.Start: if (_cursor < 0) _cursor = 0; Run(() => Host.PokemonMenuAsync(Target)); break;
            case PadButton.Select:
                if (!bank) EnterSelectMode();
                else if (Host.Screens.Dual) _inspector?.TurnPage(1);
                else Run(() => Host.SummaryAsync(Target));
                break;
            default: return false;
        }
        return true;
    }
    /// <summary>The green hand: A marks (hold and steer for an area), Y the whole box, X the actions, + the boxes.</summary>
    private bool SelectButton(PadButton button)
    {
        // Anything but steering ends an area first, so no menu opens over a live span.
        if (_selectHeld && button is not (PadButton.A or PadButton.Up or PadButton.Down or PadButton.Left or PadButton.Right))
        {
            _selectHeld = false;
            CommitRectangle();
        }
        switch (button)
        {
            case PadButton.Up: MoveCursor(0, -1); break;
            case PadButton.Down: MoveCursor(0, 1); break;
            case PadButton.Left: MoveCursor(-1, 0); break;
            case PadButton.Right: MoveCursor(1, 0); break;
            case PadButton.L: ChangeBox(-1); break;
            case PadButton.R: ChangeBox(1); break;
            case PadButton.A:
                if (_selectHeld) break;
                if (_cursor < 0) _cursor = 0;
                _selectHeld = true;
                BeginRectangle(_cursor);
                break;
            case PadButton.Y: TogglePage(); break;
            case PadButton.X: Run(() => Host.StorageToolsAsync(this)); break;
            case PadButton.Start: Run(() => Host.BoxManagementAsync(this)); break;
            case PadButton.B: if (Marks.Count > 0) Marks.Clear(); else ExitSelectMode(); break;
            case PadButton.Select: ExitSelectMode(); break;
            default: return false;
        }
        if (_selectHeld && _rectangle is { } r && button is PadButton.Up or PadButton.Down or PadButton.Left or PadButton.Right)
            _rectangle = r with { To = Cursor };
        return true;
    }
    public void Released(PadButton button)
    {
        if (button != PadButton.A || !_selectHeld) return;
        _selectHeld = false;
        CommitRectangle();
    }
    public void Touch(SKTouchEventArgs touch, bool secondary)
    {
        if (secondary && _inspector?.HandleTouch(touch) == true) return;
        if (touch.ActionType != SKTouchAction.Released) return;
        if (secondary) { Button(PadButton.Start); return; }
        var hint = _hintHits.FirstOrDefault(h => h.Rect.Contains(touch.Location));
        if (hint.Rect.Width > 0)
        {
            Host.Screens.OnPadButton(hint.Button);
            if (hint.Button == PadButton.A) Released(PadButton.A);
            return;
        }
        int index;
        if (Box < 0) index = PartyView.SlotFromTouch(new(_partyBounds.Width, _partyBounds.Height), new(touch.Location.X - _partyBounds.Left, touch.Location.Y - _partyBounds.Top));
        else { var hit = _hits.FirstOrDefault(h => h.Rect.Contains(touch.Location)); index = hit.Rect.Width > 0 ? hit.Slot : -1; }
        if (index < 0) return;
        // A tap moves the cursor; a tap on the cursor is A. With the green hand a tap flips one mark.
        var activate = _cursor == index; _cursor = index; _message = null;
        if (_selectMode) ToggleMark(index);
        else if (activate) Button(PadButton.A);
        Host.Screens.Invalidate();
    }
}

public sealed partial class WorkspaceHost
{
    internal ISpriteService StorageSprites => Get<ISpriteService>();
    /// <summary>BankPage.UpdateStatus's line: the marks, the empty slot, or the cursor's Pokémon.</summary>
    internal string BankStatus(int box, int slot, int marked)
    {
        if (marked > 0) return $"{marked} marked · X actions · B clears";
        if (Bank.GetAll().FirstOrDefault(e => e.Box == box && e.Slot == slot) is not { Info: var info })
            return $"Box {box + 1:00} slot {slot + 1:00} empty" + (HardcoreMode.IsOn ? $" · {HardcoreMode.Marker}" : " · A adds");
        var species = info.Species < Data.SpeciesNames.Count ? Data.SpeciesNames[info.Species] : $"#{info.Species}";
        var nickname = BankFilter.IsDefaultNamed(info.Nickname, species) ? "" : $" “{info.Nickname}”";
        return $"#{info.Species:000} {species}{nickname} Lv.{info.Level} · Gen {info.Generation} · {info.SourceName} · Y mark · X actions";
    }
    /// <summary>BoxBrowserViewModel.DropAsync: moves (or swaps) one Pokémon with a restore point; the refusal, or null.</summary>
    internal async Task<string?> MoveStorageAsync(SlotRef from, SlotRef to, SlotSummary? carried)
    {
        var name = carried is { } mon ? mon.Nickname ?? $"#{mon.Species}" : "Pokémon";
        var moved = await Operations.ExecuteAsync(session => { session.MoveSlot(from.Box, from.Slot, to.Box, to.Slot); return new(true, $"Moved {name}."); },
            WriteScope.Only(from, to), SaveAction.Move, $"Move {name}: {SlotLabel(from)} -> {SlotLabel(to)}");
        return moved ? null : Operations.Status;
    }
    private static string SlotLabel(SlotRef at) => at.Box == -1 ? $"Party {at.Slot + 1}" : $"Box {at.Box + 1}, Slot {at.Slot + 1}";
    internal void MoveBankEntry(Guid id, int box, int slot) => Bank.Move(id, box, slot);
    /// <summary>The move on the open session only; the safe write persists it, or reverts the session.</summary>
    internal void MoveLive(SlotRef from, SlotRef to) => Session.MoveSlot(from.Box, from.Slot, to.Box, to.Slot);
    internal EntityDetail? StorageEntity(SlotRef at)
    {
        try { return Sessions.CurrentSession?.ReadEntity(at.Box, at.Slot) is { IsEmpty: false } mon ? mon : null; }
        catch (ArgumentException) { return null; }
    }
    /// <summary>The storage's layout, read in one pass over the save's slot list.</summary>
    internal sealed record StorageShape(int BoxCount, bool HasParty, bool BdspStyle, IReadOnlyDictionary<int, int>? Counts)
    {
        public int SlotCount(int box) => Counts is null ? IBankService.SlotsPerBox : box < 0 ? 6 : Counts.GetValueOrDefault(box);
    }
    internal StorageShape ReadStorageShape(bool bank)
    {
        if (bank) return new(Bank.BoxCount, false, false, null);
        var snapshot = Session.Snapshot;
        var counts = snapshot.Slots.GroupBy(s => s.Box).ToDictionary(g => g.Key, g => g.Count());
        return new(counts.Keys.Where(box => box >= 0).DefaultIfEmpty(0).Max() + 1, counts.ContainsKey(-1), BdspIcons.AppliesTo(snapshot.Format), counts);
    }
    internal IReadOnlyList<EntityDetail?> PartySnapshot() => Enumerable.Range(0, 6).Select(index =>
    {
        try { return Sessions.CurrentSession?.ReadEntity(-1, index); } catch (ArgumentException) { return null; }
    }).ToArray();
    public object StorageVersion(bool bank) => bank ? Get<JournaledBankService>().Revision : Session;
    public string SaveTitle => Sessions.Current?.Document.DisplayName ?? "Save";
    public bool HasParty => Sessions.CurrentSession?.Snapshot.Slots.Any(s => s.Box < 0) == true;
    public int StorageSlotCount(bool bank, int box) => bank ? IBankService.SlotsPerBox : box < 0 ? 6 : Session.Snapshot.Slots.Count(s => s.Box == box);
    public string StorageBoxName(bool bank, int box) => box < 0 ? "Party" : bank ? BankBoxDecor.Label(box) : Session.GetBoxName(box);
    /// <summary>BoxBrowserPage.BoxWallpaper: the game's stored wallpaper, else the Emerald set in turn.</summary>
    public string? StorageWallpaper(int box, int boxCount)
    {
        if (box < 0) return null;
        string? stored = null;
        try { if (BoxLayoutService.SupportsWallpapers(Session) && box < boxCount) stored = BoxLayoutService.GetBox(Session, box).WallpaperAsset; }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException) { }
        return Path.Combine(ParkPlatform.AssetDirectory, "wallpapers", (stored ?? $"box_wp{box % 16 + 1:00}e") + ".png");
    }
    public int StorageBoxCount(bool bank) => bank ? Bank.BoxCount : Session.Snapshot.Slots.Where(s => s.Box >= 0).Select(s => s.Box).DefaultIfEmpty(0).Max() + 1;
    public IReadOnlyList<SlotSummary> StorageSlots(bool bank, int box) => bank
        ? Bank.GetAll().Where(e => e.Box == box).Select(e => new SlotSummary(e.Box, e.Slot, e.Info.Species, e.Info.Nickname, e.Info.Shiny, true, e.Info.Form, HeldItem: e.Info.HeldItem ?? 0, Traits: e.Info.Traits ?? default)).ToArray()
        : Session.Snapshot.Slots.Where(s => s.Box == box).ToArray();
    public PokemonTarget StorageTarget(bool bank, int box, int slot) => bank ? new(box, slot, Bank.GetAll().FirstOrDefault(e => e.Box == box && e.Slot == slot)?.Id, true) : new(box, slot);
    public SKBitmap? Sprite(SlotSummary slot)
    {
        var sprites = Get<ISpriteService>(); var bitmap = sprites.GetSprite(slot.Look);
        if (bitmap is null) sprites.Warm(slot.Look, Screens.Invalidate); return bitmap;
    }
    public string StorageDescription(PokemonTarget target)
    {
        try
        {
            return Pokemon.Read(target, (s, b, i) =>
            {
                var mon = s.ReadEntity(b, i);
                return $"{mon.Nickname} · {mon.SpeciesName}\nLevel {mon.Level} · {(mon.IsShiny ? "Shiny" : "Normal")}\nOT: {mon.OriginalTrainer}\nIVs: {string.Join('/', mon.IVs)}\nEVs: {string.Join('/', mon.EVs)}\nFriendship: {mon.Friendship}";
            });
        }
        catch (Exception error) { return error.Message; }
    }
}
