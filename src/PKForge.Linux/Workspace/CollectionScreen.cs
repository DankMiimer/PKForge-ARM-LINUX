using PKForge.App.Services;
using PKForge.Application;
using PKForge.Chrome;
using PKForge.Domain;
using PKForge.Engine;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace PKForge.Linux.Workspace;

internal sealed class CollectionScreen(WorkspaceHost host, CollectionSnapshot snapshot, IReadOnlyList<string> names, int maxSpecies, Func<int, int, bool>? storable) : INativeScreen
{
    public SecondScreenOwner Owner => SecondScreenOwner.Pokedex;
    private readonly LivingDexCatalog _catalog = LivingDexCatalogBuilder.Build();
    private readonly HashSet<int> _ownedSpecies = snapshot.Members.Select(m => m.Species).ToHashSet();
    private readonly HashSet<int> _shinySpecies = snapshot.Members.Where(m => m.Shiny).Select(m => m.Species).ToHashSet();
    private readonly HashSet<(int Species, int Form)> _ownedForms = snapshot.Members.Select(m => (m.Species, m.Form)).ToHashSet();
    private readonly HashSet<(int Species, int Form)> _shinyForms = snapshot.Members.Where(m => m.Shiny).Select(m => (m.Species, m.Form)).ToHashSet();
    private (int Species, int Form)[] _entries = [];
    private int _index;
    private int? _generation;
    private bool _forms, _shiny, _missing;
    private readonly List<(SKRect Bounds, int Index)> _hits = [];
    public void Enter() => Filter();
    private bool Owned((int Species, int Form) entry) => _forms ? (_shiny ? _shinyForms : _ownedForms).Contains(entry) : (_shiny ? _shinySpecies : _ownedSpecies).Contains(entry.Species);
    private void Filter()
    {
        var species = Enumerable.Range(1, Math.Min(CollectionDex.MaxSpecies, names.Count - 1)).Where(s => names[s].Length > 0);
        if (_forms) species = species.Where(s => _catalog.Forms.ContainsKey(s));
        IEnumerable<(int Species, int Form)> cells = species.SelectMany(s => (_forms ? _catalog.FormsOf(s) : [0]).Select(form => (s, form)));
        if (_generation is > 0)
        {
            var range = CollectionDex.GenRanges.Single(g => g.Generation == _generation);
            cells = cells.Where(c => c.Species >= range.First && c.Species <= range.Last);
        }
        if (_generation == 0) cells = cells.Where(c => c.Species <= maxSpecies && (!_forms || storable?.Invoke(c.Species, c.Form) != false));
        if (_missing) cells = cells.Where(c => !Owned(c));
        _entries = cells.ToArray(); _index = Math.Clamp(_index, 0, Math.Max(0, _entries.Length - 1));
    }
    public void Paint(SKCanvas canvas, SKRect bounds, bool secondary)
    {
        NativePaint.Background(canvas, bounds, (_shiny ? "Shiny " : "") + (_forms ? "Forms dex" : "Living dex") + (_generation is null ? " · All" : _generation == 0 ? " · This game" : " · Gen " + _generation));
        if (_entries.Length == 0) { NativePaint.Paragraph(canvas, bounds, "No entries in this view. X: scope and view filters."); return; }
        var selected = _entries[_index];
        if (secondary)
        {
            NativePaint.Paragraph(canvas, bounds, $"#{selected.Species} {names[selected.Species]}\n{LivingDexCatalogBuilder.FormName(selected.Species, selected.Form)}\n{(Owned(selected) ? "Owned" : "Missing")}\n" + string.Join('\n', snapshot.Members.Where(m => m.Species == selected.Species && (!_forms || m.Form == selected.Form) && (!_shiny || m.Shiny)).Select(m => m.Location).Distinct()));
            NativePaint.Footer(canvas, bounds, "Select: all source locations · A: reference · X: scope"); return;
        }
        const int columns = 6, visible = 30; var first = Math.Max(0, _index / columns * columns - 2 * columns); first = Math.Min(first, Math.Max(0, ((_entries.Length - 1) / columns - 4) * columns));
        var width = (bounds.Width - 24) / columns; var height = (bounds.Height - 145) / 5;
        _hits.Clear();
        for (var i = first; i < Math.Min(first + visible, _entries.Length); i++)
        {
            var at = _entries[i]; var owned = Owned(at); var rect = SKRect.Create(12 + (i - first) % columns * width, 60 + (i - first) / columns * height, width - 4, height - 4);
            using var fill = new SKPaint { Color = owned ? ColorTheme.Current.Grid : ColorTheme.Current.Deep }; canvas.DrawRoundRect(rect, 4, 4, fill);
            var slot = new SlotSummary(0, 0, at.Species, names[at.Species], _shiny, true, at.Form);
            if (host.Sprite(slot) is { } bitmap)
            {
                var scale = Math.Min((rect.Width - 6) / bitmap.Width, (rect.Height - 18) / bitmap.Height);
                using var tint = new SKPaint { Color = SKColors.White.WithAlpha(owned ? (byte)255 : (byte)45) };
                canvas.DrawBitmap(bitmap, SKRect.Create(rect.MidX - bitmap.Width * scale / 2, rect.Top + 2, bitmap.Width * scale, bitmap.Height * scale), tint);
            }
            NativePaint.Text(canvas, "#" + at.Species, rect.Left + 4, rect.Bottom - 3, 11, ColorTheme.Current.Ink);
            if (i == _index) PksmPaint.Selection(canvas, rect);
            _hits.Add((rect, i));
        }
        NativePaint.Text(canvas, names[selected.Species] + " · " + LivingDexCatalogBuilder.FormName(selected.Species, selected.Form) + (Owned(selected) ? " · Owned" : " · Missing"), 12, bounds.Height - 64, 17, ColorTheme.Current.Ink, bounds.Width - 24);
        NativePaint.Footer(canvas, bounds, $"A: reference · Select: sources · X: scope · Y: missing · Start: Autopilot · {_index + 1}/{_entries.Length}");
    }
    public bool Button(PadButton button)
    {
        switch (button)
        {
            case PadButton.Left: _index = Math.Max(0, _index - 1); break;
            case PadButton.Right: _index = Math.Min(Math.Max(0, _entries.Length - 1), _index + 1); break;
            case PadButton.Up: _index = Math.Max(0, _index - 6); break;
            case PadButton.Down: _index = Math.Min(Math.Max(0, _entries.Length - 1), _index + 6); break;
            case PadButton.L: _generation = _generation is null or <= 1 ? null : _generation - 1; Filter(); break;
            case PadButton.R: _generation = _generation is null ? 1 : _generation >= 9 ? null : _generation + 1; Filter(); break;
            case PadButton.Y: _missing = !_missing; Filter(); break;
            case PadButton.X: _ = host.Screens.RunAsync(ScopeAsync); break;
            case PadButton.A: if (_entries.Length > 0) _ = host.Screens.RunAsync(() => host.CollectionReferenceAsync(_entries[_index].Species, _entries[_index].Form)); break;
            case PadButton.Select: if (_entries.Length > 0) _ = host.Screens.RunAsync(SourcesAsync); break;
            case PadButton.Start: _ = host.Screens.RunAsync(host.OpenAutopilotAsync); break;
            default: return false;
        }
        return true;
    }
    private Task SourcesAsync()
    {
        var selected = _entries[_index];
        var members = snapshot.Members.Where(m => m.Species == selected.Species && (!_forms || m.Form == selected.Form) && (!_shiny || m.Shiny)).ToArray();
        return host.ShowCollectionSourcesAsync(names[selected.Species], selected.Form, members);
    }
    private async Task ScopeAsync()
    {
        var options = new[] { "All generations", "This game", "Toggle shiny dex", "Toggle every form", "Diagnostics" }.Concat(Enumerable.Range(1, 9).Select(i => "Generation " + i)).ToArray();
        var picked = await host.Screens.Dialogs.PickAsync("Collection scope", options); if (picked is null) return;
        switch (picked.Value)
        {
            case 0: _generation = null; break;
            case 1: _generation = 0; break;
            case 2: _shiny = !_shiny; break;
            case 3: _forms = !_forms; break;
            case 4: await host.Screens.Dialogs.MessageAsync("Collection diagnostics", string.Join('\n', snapshot.Diagnostics)); break;
            default: _generation = picked.Value - 4; break;
        }
        _index = 0; Filter();
    }
    public void Touch(SKTouchEventArgs touch, bool secondary)
    {
        if (touch.ActionType != SKTouchAction.Released) return;
        if (secondary) { Button(PadButton.A); return; }
        var hit = _hits.FirstOrDefault(h => h.Bounds.Contains(touch.Location)); if (hit.Bounds.Width <= 0) return;
        var activate = _index == hit.Index; _index = hit.Index; if (activate) Button(PadButton.A);
    }
}

public sealed partial class WorkspaceHost
{
    internal Task ShowCollectionSourcesAsync(string species, int form, IReadOnlyList<CollectionMember> members) => InspectAsync(species + " · source locations", new { Form = form, Count = members.Count, Locations = members.Select(m => m.Location).ToArray(), Details = members });
    internal Task OpenAutopilotAsync() => AutopilotAsync();
    internal async Task CollectionReferenceAsync(int species, int form)
    {
        var action = await Dialogs.MenuAsync(Data.SpeciesNames[species], LivingDexCatalogBuilder.FormName(species, form), "How to get", "Pokédex entries", "Species facts");
        if (action == "How to get") await InspectAsync("Encounters by game", Get<IEncounterLookup>().Describe(species, form));
        if (action == "Pokédex entries") await InspectAsync("Pokédex entries", DexFacts.Default.Entries(species));
        if (action == "Species facts")
        {
            using var blank = Sessions.CurrentSession is null ? Get<ISaveEngine>().OpenBlankSession(9) : null;
            await InspectAsync("Species facts", Get<IMonInfoService>().GetSpeciesCard(Sessions.CurrentSession ?? blank!, species, form));
        }
    }
}
