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
    private PokemonTarget _target = target;
    private MonSummary? _summary;
    private SummaryPage _page;
    private int _scroll, _lineCount, _visible;
    private bool _overview;
    private readonly List<(SKRect Bounds, int Slot)> _overviewHits = [];
    private readonly List<(SKRect Bounds, SummaryPage Page)> _tabHits = [];
    private object? _version;
    private IReadOnlyList<SlotSummary> _slots = [];
    private IReadOnlyList<SlotSummary> Slots
    {
        get { var version = host.StorageVersion(_target.InBank); if (!Equals(version, _version)) { _version = version; _slots = host.StorageSlots(_target.InBank, _target.Box); } return _slots; }
    }
    public void Enter() { _version = null; _summary = host.BuildSummary(_target); }
    public void Paint(SKCanvas canvas, SKRect bounds, bool secondary)
    {
        var overview = secondary || _overview && !host.Screens.Dual;
        NativePaint.Background(canvas, bounds, overview ? "Summary · Box overview" : "Pokémon summary");
        if (_summary is not { } s) { NativePaint.Paragraph(canvas, bounds, "This slot is empty."); return; }
        if (overview) { Overview(canvas, bounds); return; }
        EditorPaint.PaintHeader(canvas, SKRect.Create(12, 57, bounds.Width - 24, 104), new(s.DisplayName, s.Level, s.IsShiny, s.Gender, s.SpeciesName, $"{s.Format} · {s.FormName}"), PixelFont.FallbackFace, .72f);
        var pages = Enum.GetValues<SummaryPage>();
        _tabHits.Clear();
        for (var i = 0; i < pages.Length; i++)
        {
            var width = (bounds.Width - 24) / pages.Length;
            var rect = SKRect.Create(12 + i * width, 158, width - 3, 32);
            NativePaint.Row(canvas, rect, pages[i].ToString(), pages[i] == _page);
            _tabHits.Add((rect, pages[i]));
        }
        using var font = new SKFont(PixelFont.FallbackFace, 16);
        var text = Content(s); var lines = NativeText.Wrap(text, font, bounds.Width - 36);
        _lineCount = lines.Count; _visible = Math.Max(1, (int)((bounds.Height - 240) / 23));
        _scroll = Math.Clamp(_scroll, 0, Math.Max(0, lines.Count - _visible));
        var y = 218f;
        foreach (var line in lines.Skip(_scroll).Take(_visible)) { NativePaint.Text(canvas, line, 18, y, 16, ColorTheme.Current.Ink, bounds.Width - 36); y += 23; }
        NativePaint.Footer(canvas, bounds, host.Screens.Dual ? "←/→: tabs · ↑/↓: scroll · L/R: Pokémon · A: actions · X: details · Y: legality" : "←/→: tabs · L/R: Pokémon · A: actions · X: details · Y: legality · Select: box");
    }
    private string Content(MonSummary s) => _page switch
    {
        SummaryPage.Info => $"{s.SpeciesName}{(s.IsEgg ? " · Egg" : "")} · {s.FormName}\nOT {s.OriginalTrainer} · ID {s.TrainerId} / {s.SecretId}\n" +
            (s.NatureName is null ? "" : $"Nature: {s.NatureName}\n") +
            (s.AbilityName is null ? "" : $"Ability: {s.AbilityName}\n{s.AbilityEffect}\n") +
            $"Item: {(string.IsNullOrWhiteSpace(s.HeldItemName) ? "None" : s.HeldItemName)}\n" + (string.IsNullOrWhiteSpace(s.HeldItemEffect) ? "" : s.HeldItemEffect + "\n") +
            $"Ball: {s.BallName}\nFriendship: {s.Friendship}\n" + (s.Pokerus is { Supported: true } virus ? $"Pokérus: {virus.Status}" + (virus.Strain > 0 ? $" · Strain {virus.Strain} · {virus.Days} days" : "") : ""),
        SummaryPage.Stats => "Stat          Actual  Base   IV/DV   EV/Exp\n" + string.Join('\n', new[] { "HP", "Attack", "Defense", "Sp. Atk", "Sp. Def", "Speed" }.Select((name, i) =>
            $"{name.PadRight(12)} {At(s.Stats, i),4}   {At(s.BaseStats, i),4}    {At(s.IVs, i),3}    {At(s.EVs, i),5}")) +
            $"\nIV total: {s.IvTotal} · EV total: {s.EvTotal}\n{s.Characteristic}\nTera type: {s.TeraTypeName}\nCaps: IV/DV {s.Caps.IvMax}, EV/Exp {s.Caps.EvMax}",
        SummaryPage.Moves => string.Join("\n\n", s.Moves.Select(m => $"{m.Name} · {m.Category}\nPower {m.Power} · Accuracy {m.Accuracy} · PP {m.PP}/{m.MaxPP} (+{m.PPUps})\n{m.Effect}")) + "\n\nRelearn: " + string.Join(", ", s.RelearnMoves),
        SummaryPage.Origin => NativeForms.Describe(s.Met) + $"\n\nRibbons: {s.RibbonCount} · Marks: {s.MarkCount}\n" + string.Join('\n', s.RibbonNames),
        SummaryPage.Legality => s.Legal is null ? "Press Y to run the pinned engine's offline legality analysis. Formats without legality tables remain unavailable." : (s.Legal == true ? "Legal" : "Legality issues") + "\n" + string.Join('\n', s.LegalityLines),
        _ => ""
    };
    private static int At(IReadOnlyList<int> values, int i) => i < values.Count ? values[i] : 0;
    private void Overview(SKCanvas canvas, SKRect bounds)
    {
        var slots = Slots; var count = host.StorageSlotCount(_target.InBank, _target.Box);
        var columns = _target.Box < 0 ? 2 : count <= 20 ? 5 : 6; var rows = (count + columns - 1) / columns;
        var width = (bounds.Width - 24) / columns; var height = Math.Min(70, (bounds.Height - 120) / rows);
        _overviewHits.Clear();
        for (var i = 0; i < count; i++)
        {
            var rect = SKRect.Create(12 + i % columns * width, 65 + i / columns * height, width - 4, height - 4);
            using var fill = new SKPaint { Color = ColorTheme.Current.Well }; canvas.DrawRoundRect(rect, 4, 4, fill);
            if (i == _target.Slot) StoragePaint.CursorPool(canvas, rect, .4f);
            var slot = slots.FirstOrDefault(s => s.Slot == i);
            if (slot is { Species: > 0 } && host.Sprite(slot) is { } bitmap)
            {
                var scale = Math.Min(rect.Width / bitmap.Width, rect.Height / bitmap.Height);
                canvas.DrawBitmap(bitmap, SKRect.Create(rect.MidX - bitmap.Width * scale / 2, rect.MidY - bitmap.Height * scale / 2, bitmap.Width * scale, bitmap.Height * scale));
            }
            if (i == _target.Slot) StoragePaint.Pointer(canvas, rect, .4f);
            _overviewHits.Add((rect, i));
        }
        NativePaint.Text(canvas, host.StorageBoxName(_target.InBank, _target.Box) + " · " + _summary!.DisplayName, 12, bounds.Bottom - 48, 17, ColorTheme.Current.Ink, bounds.Width - 24);
        NativePaint.Footer(canvas, bounds, host.Screens.Dual ? "Touch an occupied slot to view its summary." : "D-pad: Pokémon · A: actions · Select/B: summary");
    }
    private void SelectSlot(int index)
    {
        if (!Slots.Any(s => s.Slot == index && s.Species > 0)) return;
        _target = host.StorageTarget(_target.InBank, _target.Box, index); Enter(); _scroll = 0;
    }
    public bool Button(PadButton button)
    {
        if (_overview && !host.Screens.Dual)
        {
            if (button is PadButton.B or PadButton.Select) { _overview = false; return true; }
            if (button is PadButton.Left or PadButton.Right or PadButton.Up or PadButton.Down)
            {
                var count = host.StorageSlotCount(_target.InBank, _target.Box);
                var columns = _target.Box < 0 ? 2 : count <= 20 ? 5 : 6;
                var direction = button is PadButton.Left or PadButton.Up ? -1 : 1;
                var stride = button is PadButton.Up or PadButton.Down ? columns : 1;
                // Prefer the requested row/column, then an occupied adjacent slot.
                var index = _target.Slot + direction * stride;
                if (index >= 0 && index < count && Slots.Any(s => s.Slot == index && s.Species > 0)) SelectSlot(index);
                else if (SummaryNavigation.Step(_target.Slot, direction, count, i => Slots.Any(s => s.Slot == i && s.Species > 0)) is { } adjacent) SelectSlot(adjacent);
                return true;
            }
        }
        switch (button)
        {
            case PadButton.Left: _page = (SummaryPage)(((int)_page + 4) % 5); _scroll = 0; break;
            case PadButton.Right: _page = (SummaryPage)(((int)_page + 1) % 5); _scroll = 0; break;
            case PadButton.Up: _scroll = Math.Max(0, _scroll - 1); break;
            case PadButton.Down: _scroll = Math.Min(Math.Max(0, _lineCount - _visible), _scroll + 1); break;
            case PadButton.L: case PadButton.R:
                var slots = Slots;
                var next = SummaryNavigation.Step(_target.Slot, button == PadButton.L ? -1 : 1, host.StorageSlotCount(_target.InBank, _target.Box), i => slots.Any(s => s.Slot == i && s.Species is > 0));
                if (next is { } index) { _target = host.StorageTarget(_target.InBank, _target.Box, index); Enter(); _scroll = 0; } break;
            case PadButton.A: _ = host.Screens.RunAsync(async () => { await host.PokemonMenuAsync(_target); Enter(); }); break;
            case PadButton.X: _ = host.Screens.RunAsync(() => host.ShowSummaryDetailsAsync(_target)); break;
            case PadButton.Y: _ = host.Screens.RunAsync(async () => _summary = await Task.Run(() => host.BuildSummary(_target, true))); break;
            case PadButton.Select: if (!host.Screens.Dual) _overview = true; break;
            default: return false;
        }
        return true;
    }
    public void Touch(SKTouchEventArgs touch, bool secondary)
    {
        if (touch.ActionType != SKTouchAction.Released) return;
        if (secondary || _overview && !host.Screens.Dual)
        {
            var slot = _overviewHits.FirstOrDefault(h => h.Bounds.Contains(touch.Location));
            if (slot.Bounds.Width > 0) SelectSlot(slot.Slot);
            else if (!secondary && touch.Location.Y > 430) _overview = false;
            return;
        }
        var tab = _tabHits.FirstOrDefault(h => h.Bounds.Contains(touch.Location));
        if (tab.Bounds.Width > 0) { _page = tab.Page; _scroll = 0; }
        else if (touch.Location.Y > 430) host.Screens.Pop(); else Button(PadButton.Down);
    }
}

public sealed partial class WorkspaceHost
{
    internal MonSummary? BuildSummary(PokemonTarget target, bool legality = false) => Pokemon.Read(target, (s, b, i) => Get<IMonSummaryService>().Build(s, b, i, legality));
    internal Task ShowSummaryDetailsAsync(PokemonTarget target) => InspectAsync("All summary fields", BuildSummary(target));
}
