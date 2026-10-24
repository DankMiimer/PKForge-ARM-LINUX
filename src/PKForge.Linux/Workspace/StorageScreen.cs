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
    public void Leave() { _wallpaper?.Dispose(); _wallpaper = null; _wallpaperPath = null; }
    public bool IsBank => bank;
    public int Box { get; private set; }
    public int Cursor { get; private set; }
    public HashSet<SlotRef> Marks { get; } = [];
    private readonly List<(SKRect Rect, int Slot)> _hits = [];
    private WorkspaceHost Host => workspace;
    private int BoxCount => Host.StorageBoxCount(bank);
    private int SlotCount => Host.StorageSlotCount(bank, Box);
    private int Columns => Box < 0 ? 2 : SlotCount <= 20 ? 5 : 6;
    private int _cachedBox = int.MinValue;
    private object? _version;
    private IReadOnlyList<SlotSummary> _slots = [];
    private (int Box, int Slot, object? Version)? _inspected;
    private string _description = "";
    public void Enter() { _cachedBox = int.MinValue; _inspected = null; }
    private IReadOnlyList<SlotSummary> Slots
    {
        get { var version = Host.StorageVersion(bank); if (Box != _cachedBox || !Equals(version, _version)) { _cachedBox = Box; _version = version; _slots = Host.StorageSlots(bank, Box); } return _slots; }
    }
    private SlotSummary? Selected => Slots.FirstOrDefault(s => s.Slot == Cursor);
    public PokemonTarget Target => Host.StorageTarget(bank, Box, Cursor);
    public void Paint(SKCanvas canvas, SKRect bounds, bool secondary)
    {
        Box = Math.Clamp(Box, bank || !Host.HasParty ? 0 : -1, Math.Max(0, BoxCount - 1));
        Cursor = Math.Clamp(Cursor, 0, Math.Max(0, SlotCount - 1));
        var slots = Slots;
        NativePaint.Background(canvas, bounds, bank ? $"Bank · Box {Box + 1}/{BoxCount}" : Box < 0 ? "Party" : $"{Host.SaveTitle} · Box {Box + 1}/{BoxCount}");
        if (secondary) { PaintInspector(canvas, bounds); return; }
        using var headerFont = new SKFont(PixelFont.FallbackFace, 18);
        StoragePaint.BoxHeader(canvas, SKRect.Create(12, 54, bounds.Width - 24, 38), Host.StorageBoxName(bank, Box), headerFont, Box > (bank || !Host.HasParty ? 0 : -1), Box < BoxCount - 1, .45f);
        var well = SKRect.Create(12, 98, bounds.Width - 24, bounds.Height - 178);
        StoragePaint.WellPanel(canvas, well, .45f);
        if (bank) PksmPaint.Wallpaper(canvas, SKRect.Inflate(well, -2, -2), Host.BankWallpaperColor(Box));
        var wallpaper = Host.StorageWallpaper(bank, Box);
        if (wallpaper != _wallpaperPath)
        {
            _wallpaper?.Dispose(); _wallpaper = wallpaper is not null && File.Exists(wallpaper) ? SKImage.FromEncodedData(wallpaper) : null; _wallpaperPath = wallpaper;
        }
        if (_wallpaper is not null) StoragePaint.Wallpaper(canvas, well, _wallpaper, ColorTheme.Current.WallpaperMid, BoxBackground.Style, .45f);
        _hits.Clear();
        var columns = Columns; var rows = Math.Max(1, (SlotCount + columns - 1) / columns);
        var width = (bounds.Width - 24) / columns; var height = well.Height / rows;
        for (var index = 0; index < SlotCount; index++)
        {
            var slot = slots.FirstOrDefault(s => s.Slot == index);
            var rect = SKRect.Create(12 + index % columns * width, well.Top + index / columns * height, width - 4, height - 4);
            if (index == Cursor) StoragePaint.CursorPool(canvas, rect, .45f);
            if (Marks.Contains(new(Box, index))) PksmPaint.RangeWash(canvas, rect, true);
            if (slot is { Species: > 0 })
            {
                var bitmap = Host.Sprite(slot);
                if (bitmap is not null)
                {
                    var scale = Math.Min((rect.Width - 10) / bitmap.Width, (rect.Height - 18) / bitmap.Height);
                    canvas.DrawBitmap(bitmap, SKRect.Create(rect.MidX - bitmap.Width * scale / 2, rect.Top + 2, bitmap.Width * scale, bitmap.Height * scale));
                }
                NativePaint.Text(canvas, slot.Nickname ?? "#" + slot.Species, rect.Left + 5, rect.Bottom - 4, 11, SKColors.White, rect.Width - 10);
            }
            else NativePaint.Text(canvas, (index + 1).ToString(), rect.Left + 7, rect.Top + 18, 12, new SKColor(125, 147, 170));
            if (index == Cursor) StoragePaint.Pointer(canvas, rect, .4f);
            _hits.Add((rect, index));
        }
        var mon = Selected;
        NativePaint.Text(canvas, mon?.Species is > 0 ? $"{mon.Nickname} · #{mon.Species}{(mon.IsShiny ? " ★" : "")}" : "Empty slot", 15, bounds.Height - 65, 18, SKColors.White, bounds.Width - 30);
        NativePaint.Footer(canvas, bounds, "A: actions · X: summary · Y: mark · Start: tools · L/R: box · B: back");
    }
    private void PaintInspector(SKCanvas canvas, SKRect bounds)
    {
        if (Selected is not { Species: > 0 } slot) { NativePaint.Paragraph(canvas, bounds, "Empty slot\nA: import or generate a Pokémon\nStart: storage tools"); return; }
        var key = (Box, Cursor, _version);
        if (_inspected != key) { _inspected = key; _description = Host.StorageDescription(Target); }
        NativePaint.Paragraph(canvas, bounds, _description);
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
        if (touch.ActionType != SKTouchAction.Released) return;
        if (secondary) { Button(PadButton.A); return; }
        var hit = _hits.FirstOrDefault(h => h.Rect.Contains(touch.Location));
        if (hit.Rect.Width <= 0) return;
        var activate = Cursor == hit.Slot; Cursor = hit.Slot;
        if (activate) Button(PadButton.A); Host.Screens.Invalidate();
    }
}

public sealed partial class WorkspaceHost
{
    public object StorageVersion(bool bank) => bank ? Get<JournaledBankService>().Revision : Session;
    public string SaveTitle => Sessions.Current?.Document.DisplayName ?? "Save";
    public bool HasParty => Sessions.CurrentSession?.Snapshot.Slots.Any(s => s.Box < 0) == true;
    public SKColor BankWallpaperColor(int box) => Pksm.BoxWallpapers[(BankBoxDecor.Wallpapers.Get(box)?.ColorIndex ?? box) % Pksm.BoxWallpapers.Length];
    public int StorageSlotCount(bool bank, int box) => bank ? IBankService.SlotsPerBox : box < 0 ? 6 : Session.Snapshot.Slots.Count(s => s.Box == box);
    public string StorageBoxName(bool bank, int box) => box < 0 ? "Party" : bank ? BankBoxDecor.Label(box) : Session.GetBoxName(box);
    public string? StorageWallpaper(bool bank, int box)
    {
        if (box < 0) return null;
        var asset = bank ? BankBoxDecor.Wallpapers.Get(box)?.ArtAsset : BoxLayoutService.IsSupported(Session) ? BoxLayoutService.GetBox(Session, box).WallpaperAsset : null;
        return asset is null ? null : Path.Combine(ParkPlatform.AssetDirectory, "wallpapers", asset + ".png");
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
