using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Application;
using PKForge.Domain;
using PKForge.Chrome;
using PKForge.Engine;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace PKForge.Linux.Workspace;

public sealed class StorageScreen(WorkspaceHost workspace, bool bank) : INativeScreen
{
    public SecondScreenOwner Owner => bank ? SecondScreenOwner.Bank : SecondScreenOwner.Box;
    private string? _wallpaperPath;
    private SKImage? _wallpaper;
    public void Leave() { _wallpaper?.Dispose(); _wallpaper = null; _wallpaperPath = null; _hand.Reset(); _inspector?.ReleaseSurface(); _inspector = null; }
    public bool IsBank => bank;
    public int Box { get; private set; }
    public int Cursor { get; private set; }
    public HashSet<SlotRef> Marks { get; } = [];
    private readonly List<(SKRect Rect, int Slot)> _hits = [];
    private WorkspaceHost Host => workspace;
    private int BoxCount => Host.StorageBoxCount(bank);
    private int SlotCount => Host.StorageSlotCount(bank, Box);
    private int Columns => Box < 0 ? 2 : 6;
    private int _cachedBox = int.MinValue;
    private object? _version;
    private IReadOnlyList<SlotSummary> _slots = [];
    private IReadOnlyList<SlotSummary> _denseSlots = [];
    private IReadOnlyList<EntityDetail?> _party = [];
    private readonly CarryHand _hand = new();
    private (int Box, int Slot, object? Version)? _inspected;
    private MonSummaryView? _inspector;
    public void Enter() { _cachedBox = int.MinValue; _inspected = null; }
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
            }
            return _slots;
        }
    }
    private SlotSummary? Selected => Slots.FirstOrDefault(s => s.Slot == Cursor);
    public PokemonTarget Target => Host.StorageTarget(bank, Box, Cursor);
    public void Paint(SKCanvas canvas, SKRect bounds, bool secondary)
    {
        Box = Math.Clamp(Box, bank || !Host.HasParty ? 0 : -1, Math.Max(0, BoxCount - 1));
        Cursor = Math.Clamp(Cursor, 0, Math.Max(0, SlotCount - 1));
        var slots = Slots;
        PksmPaint.LogoGrid(canvas, bounds, 14, 1);
        AppChromePaint.Header(canvas, bounds, "PKForge", PixelFont.Face, statusName: bank ? "Bank" : Host.SaveTitle, status: bank ? "Open" : "Connected");
        if (secondary) { PaintInspector(canvas, bounds); return; }
        var unit = Math.Max(bounds.Width, bounds.Height) / 1920;
        var left = 48 * unit; var right = 28 * unit;
        var bar = SKRect.Create(left, 54 + 32 * unit, bounds.Width - left - right, 72 * unit);
        using var headerFont = new SKFont(PixelFont.Face, 34 * unit);
        StoragePaint.BoxHeader(canvas, bar, HeaderName, headerFont, Box > (bank || !Host.HasParty ? 0 : -1), Box < BoxCount - 1, unit);
        // BankPage pads its body 20 (BoxBrowserPage 34) and stacks its LCD status line over the footer.
        var well = new SKRect(left, bar.Bottom + 28 * unit, bounds.Right - right,
            bank ? bounds.Bottom - 42 - 6 - StatusHeight - 20 * unit : bounds.Bottom - 42 - 34 * unit);
        var wallpaper = bank ? null : Host.StorageWallpaper(Box);
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
            PartyView.Paint(canvas, info, Host.StorageSprites, null, Cursor, Host.Screens.Invalidate,
                isMarked: Marks.Count > 0 ? index => Marks.Contains(new(-1, index)) : null, snapshot: _party);
            // The party painter owns its staggered octagonal hit geometry.
            _partyBounds = well;
        }
        else
        {
            var scene = new BoxGridRenderer.StorageScene(_denseSlots, Cursor, null, null, Marks.Count > 0,
                index => Marks.Contains(new(Box, index)), _ => null, Box, BdspStyle: Host.StorageUsesBdspIcons(bank));
            BoxGridRenderer.PaintScene(canvas, info, scene, Host.StorageSprites, Host.Screens.Invalidate, _hand, (c, area, scale) =>
            {
                if (bank) { BankBackdrop(c, area, scale); return; }
                if (_wallpaper is not null) StoragePaint.Wallpaper(c, area, _wallpaper, _wallpaperAverage, BoxBackground.Style, scale);
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
            var key = (Box, Cursor, Marks.Count, _version);
            if (!key.Equals(_statusKey)) { _statusKey = key; _status = Host.BankStatus(Box, Cursor, Marks.Count); }
            canvas.DrawText(NativeText.Fit(_status, statusFont, status.Width - 12), status.Left + 6,
                status.MidY - (statusFont.Metrics.Ascent + statusFont.Metrics.Descent) / 2, statusFont, ink);
        }
        (string, string)[] hints = [("A", "Actions"), ("X", "Summary"), ("Y", "Mark"), ("LR", "Box"), ("+", "Tools"), ("B", "Back")];
        using var hintFont = AppChromePaint.HintFont(footer, hints, PixelFont.Face, 14);
        PksmPaint.HintBar(canvas, footer, hints, hintFont);
    }
    private SKRect _partyBounds;
    private SKColor _wallpaperAverage;
    /// <summary>BankPage.Paint's backdrop: a wallpaper picture through the save boxes' painter, else a flat tint.</summary>
    private void BankBackdrop(SKCanvas c, SKRect well, float unit)
    {
        if (BankBoxArt.Art(Box, Host.Screens.Invalidate) is { } art)
        {
            using var image = SKImage.FromBitmap(art.Art);
            StoragePaint.Wallpaper(c, well, image, art.Average, BoxBackground.Style, unit);
            return;
        }
        using var tint = new SKPaint { Color = PksmPaint.Mix(BankBoxArt.Tone(Box), StoragePaint.Well, 0.7f), IsAntialias = true };
        c.DrawRoundRect(SKRect.Inflate(well, -3 * unit, -3 * unit), 25 * unit, 25 * unit, tint);
    }
    /// <summary>Kit.LcdPanel around a 13 dp pixel label, 6 padding.</summary>
    private const float StatusHeight = 26;
    private (int, int, int, object?) _statusKey = (int.MinValue, 0, 0, null);
    private string _status = "";
    /// <summary>BankPage.UpdatePageLabel's counter for the Bank; the save's own box name otherwise.</summary>
    private string HeaderName => !bank ? Host.StorageBoxName(false, Box)
        : BankBoxDecor.Names.Get(Box) is { } name ? $"{Box + 1:00} / {BoxCount:00} · {name}" : $"Box {Box + 1:00} / {BoxCount:00}";
    private void PaintInspector(SKCanvas canvas, SKRect bounds)
    {
        _inspector ??= new MonSummaryView(Host.StorageSprites);
        var key = (Box, Cursor, _version);
        if (_inspected != key)
        {
            _inspected = key;
            _inspector.Show(Selected is { Species: > 0 } ? Host.BuildSummary(Target) : null,
                emptyText: "Empty slot · A: import or generate a Pokémon");
        }
        if (Host.Screens.DrawingDevice is { } device) _inspector.Draw(canvas, new(12, 62, bounds.Right - 12, bounds.Bottom - 45), device);
        NativePaint.Footer(canvas, bounds, "A: edit · X: summary · Start: tools");
    }
    public bool Button(PadButton button)
    {
        switch (button)
        {
            case PadButton.Left: Cursor = Math.Max(0, Cursor - 1); break;
            case PadButton.Right: Cursor = Math.Min(SlotCount - 1, Cursor + 1); break;
            case PadButton.Up: Cursor = Math.Max(0, Cursor - Columns); break;
            case PadButton.Down: Cursor = Math.Min(SlotCount - 1, Cursor + Columns); break;
            case PadButton.L: Box = Math.Max(bank || !Host.HasParty ? 0 : -1, Box - 1); Cursor = Math.Min(Cursor, SlotCount - 1); break;
            case PadButton.R: Box = Math.Min(BoxCount - 1, Box + 1); Cursor = Math.Min(Cursor, SlotCount - 1); break;
            case PadButton.Y: if (!Marks.Remove(new(Box, Cursor))) Marks.Add(new(Box, Cursor)); break;
            case PadButton.X: _ = Host.Screens.RunAsync(() => Host.SummaryAsync(Target)); break;
            case PadButton.A: _ = Host.Screens.RunAsync(() => Host.PokemonMenuAsync(Target)); break;
            case PadButton.Start: _ = Host.Screens.RunAsync(() => Host.StorageToolsAsync(this)); break;
            default: return false;
        }
        return true;
    }
    public void Touch(SKTouchEventArgs touch, bool secondary)
    {
        if (secondary && _inspector?.HandleTouch(touch) == true) return;
        if (touch.ActionType != SKTouchAction.Released) return;
        if (secondary) { Button(PadButton.A); return; }
        if (Box < 0)
        {
            var index = PartyView.SlotFromTouch(new(_partyBounds.Width, _partyBounds.Height), new(touch.Location.X - _partyBounds.Left, touch.Location.Y - _partyBounds.Top));
            if (index >= 0) { var openPartySlot = Cursor == index; Cursor = index; if (openPartySlot) Button(PadButton.A); Host.Screens.Invalidate(); }
            return;
        }
        var hit = _hits.FirstOrDefault(h => h.Rect.Contains(touch.Location));
        if (hit.Rect.Width <= 0) return;
        var activate = Cursor == hit.Slot; Cursor = hit.Slot;
        if (activate) Button(PadButton.A); Host.Screens.Invalidate();
    }
}

public sealed partial class WorkspaceHost
{
    internal ISpriteService StorageSprites => Get<ISpriteService>();
    /// <summary>BankPage.UpdateStatus's line, naming the Linux buttons that do each thing.</summary>
    internal string BankStatus(int box, int slot, int marked)
    {
        if (marked > 0) return $"{marked} marked · + tools";
        if (Bank.GetAll().FirstOrDefault(e => e.Box == box && e.Slot == slot) is not { Info: var info })
            return $"Box {box + 1:00} slot {slot + 1:00} empty · A adds";
        var species = info.Species < Data.SpeciesNames.Count ? Data.SpeciesNames[info.Species] : $"#{info.Species}";
        var nickname = BankFilter.IsDefaultNamed(info.Nickname, species) ? "" : $" “{info.Nickname}”";
        return $"#{info.Species:000} {species}{nickname} Lv.{info.Level} · Gen {info.Generation} · {info.SourceName} · Y mark · A actions";
    }
    internal bool StorageUsesBdspIcons(bool bank) => !bank && BdspIcons.AppliesTo(Session.Snapshot.Format);
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
    public string? StorageWallpaper(int box)
    {
        if (box < 0) return null;
        string? stored = null;
        try { if (BoxLayoutService.SupportsWallpapers(Session) && box < StorageBoxCount(false)) stored = BoxLayoutService.GetBox(Session, box).WallpaperAsset; }
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
