using System.Diagnostics;
using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Application;
using PKForge.Chrome;
using PKForge.Domain;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace PKForge.Linux.Workspace;

public sealed partial class WorkspaceHost
{
    private sealed class HomeScreen(WorkspaceHost host) : INativeScreen
    {
        private static readonly (string Icon, string Label)[] Destinations =
            [("bank", "Bank"), ("pokedex", "Dex Autopilot"), ("park", "Poképark"), ("events", "Events"), ("settings", "Settings")];
        private static readonly (string Key, string Label)[] Hints =
            [("A", "Open"), ("B", "Edit game · hold"), ("L", "Filter"), ("Y", "Link"), ("X", "File"), ("+", "Settings")];
        private readonly Spring _scroll = new(0);
        private readonly float[] _scales = [1, 1, 1, 1, 1];
        private readonly Dictionary<string, SKBitmap?> _icons = [];
        private readonly List<(SKRect Rect, int Index)> _cartridgeHits = [], _destinationHits = [], _footerHits = [];
        private IReadOnlyList<IReadOnlyList<DetectedSave>> _groups = [];
        private IReadOnlyList<IReadOnlyList<DetectedSave>>? _catalogueSource;
        private SaveSession? _sessionSource;
        private HomeLayout? _layout;
        private int _shelfIndex = -1, _zone, _destination;
        private long _lastMove, _pressedAt;
        private SKPoint _pressPoint;
        private float _pressOffset;
        private bool _pressed, _dragging, _longPressed;
        private long? _touchId;
        private int _pressedCart = -1;
        private string? _selectedId;
        public SecondScreenOwner Owner => SecondScreenOwner.Home;
        public bool ShouldersRepeat => false;

        private void Refresh()
        {
            var groups = host.LibraryGroups;
            var current = host.Sessions.Current;
            if (ReferenceEquals(groups, _catalogueSource) && ReferenceEquals(current, _sessionSource)) return;
            // A scan/identity change can replace or reorder the shelf under a finger.
            // Release that gesture rather than opening a different cartridge by index.
            _pressed = false; _touchId = null;
            _catalogueSource = groups; _sessionSource = current; _groups = groups;
            var catalogue = host.Get<ISaveCatalogue>().Saves;
            if (current is not null && !catalogue.Any(s => s.DocumentId == current.Document.DocumentId))
            {
                var description = host.Get<ISaveEngine>().TryDescribe(current.Snapshot.OriginalBytes, current.Document.DisplayName);
                DetectedSave opened = new(current.Document.DocumentId, current.Document.DisplayName,
                    description?.GameName ?? current.Snapshot.DisplayName ?? current.Document.DisplayName, EmulatorKind.RetroArch,
                    false, File.Exists(current.Document.DocumentId) ? File.GetLastWriteTimeUtc(current.Document.DocumentId) : null,
                    current.Snapshot.Generation, description?.TrainerName, description?.PlayTime);
                var guess = SaveIdentityRules.Guess(opened.GameLabel, opened.Generation, opened.FileName, null, opened.GameLabel);
                var identity = SaveIdentityResolver.Resolve(guess, host.Get<ISaveIdentityStore>().Get(opened.DocumentId));
                opened = opened with { Guess = guess, Identity = identity, GameLabel = identity.DisplayName };
                _groups = SaveLibraryWorkflow.Group(identity.IsHidden ? catalogue : catalogue.Append(opened).ToArray(), host._libraryFilter, host._libraryQuery);
            }
            groups = _groups;
            _shelfIndex = _selectedId is null ? -1 : groups.ToList().FindIndex(group => group.Any(s => s.DocumentId == _selectedId));
            _selectedId = _shelfIndex >= 0 ? groups[_shelfIndex][0].DocumentId : null;
            if (_layout is { } layout) _scroll.Snap(layout.CenteredScroll(Math.Max(0, _shelfIndex), groups.Count));
        }
        public void Enter() { Refresh(); _zone = 0; Array.Fill(_scales, 1); }
        public void RefreshIdentity() { _catalogueSource = null; Refresh(); }
        public void Leave()
        {
            _pressed = false; _touchId = null;
            UiDispatcher.Instance.Animations.Cancel(this);
            for (var i = 0; i < _scales.Length; i++) _scales[i] = _zone == 1 && i == _destination ? 1.05f : 1;
            foreach (var bitmap in _icons.Values) bitmap?.Dispose();
            _icons.Clear(); host.ClearHomeArt();
        }
        public void Tick(float seconds)
        {
            _scroll.Step(seconds, 300, 34.6f);
            if (!_pressed || _dragging || _longPressed || _pressedCart < 0 || Stopwatch.GetElapsedTime(_pressedAt).TotalMilliseconds < 500) return;
            _longPressed = true;
            _pressed = false; _touchId = null;
            var group = _groups.ElementAtOrDefault(_pressedCart);
            if (group is not null) _ = host.Screens.RunAsync(() => host.HomeIdentityAsync(group));
        }
        private SKBitmap? Icon(string name)
        {
            if (_icons.TryGetValue(name, out var bitmap)) return bitmap;
            var path = Path.Combine(ParkPlatform.AssetDirectory, "ui", "pksm", PksmIconCatalog.Asset(name));
            bitmap = File.Exists(path) ? SKBitmap.Decode(path) : null;
            if (bitmap is not null)
            {
                using var canvas = new SKCanvas(bitmap); using var paint = new SKPaint { Color = SKColors.White, BlendMode = SKBlendMode.SrcIn };
                canvas.DrawRect(new(0, 0, bitmap.Width, bitmap.Height), paint);
            }
            _icons.Add(name, bitmap); return bitmap;
        }
        public void Paint(SKCanvas canvas, SKRect bounds, bool secondary)
        {
            Refresh();
            if (secondary) { host.PaintGamePreview(canvas, bounds, _shelfIndex >= 0 ? _groups[_shelfIndex][0] : null); return; }
            var layout = _layout = HomeLayout.Create(bounds); var unit = layout.Unit;
            PksmPaint.LogoGrid(canvas, bounds, 14 * unit, unit);
            AppChromePaint.Header(canvas, bounds, "PKForge", PixelFont.Face, unit);
            NativePaint.Text(canvas, "Games", layout.Header.Left, layout.Header.MidY + 5 * unit, 15 * unit, Pksm.Ink);
            PksmPaint.BlackButton(canvas, layout.Filter, 4 * unit);
            HomePaint.Text(canvas, "Filter · " + (host._libraryFilter == "all" ? "All" : host._libraryFilter), layout.Filter,
                layout.Filter.MidY + 5 * unit, PixelFont.Face, 14 * unit, Pksm.Ink);
            _cartridgeHits.Clear();
            canvas.Save(); canvas.ClipRect(layout.Shelf);
            for (var i = 0; i < _groups.Count; i++)
            {
                var rect = layout.Cartridge(i, _scroll.Value);
                if (rect.Right < layout.Shelf.Left || rect.Left > layout.Shelf.Right) continue;
                var group = _groups[i]; var art = group[0].ArtLabel is { } label ? host.HomeArt("gameart", label) : null;
                HomePaint.Cartridge(canvas, rect, group, art, _zone == 0 && i == _shelfIndex, PixelFont.Face, unit, PixelFont.FallbackFace);
                _cartridgeHits.Add((SKRect.Intersect(rect, layout.Shelf), i));
            }
            if (_groups.Count == 0)
            {
                HomePaint.Text(canvas, "Let's find your games!", layout.Shelf, layout.Shelf.MidY - 14 * unit, PixelFont.Face, 20 * unit, Pksm.Ink);
                HomePaint.Text(canvas, "Y: Link an emulator folder   X: Open a save file", layout.Shelf, layout.Shelf.MidY + 15 * unit, PixelFont.Face, 14 * unit, Pksm.InkSoft);
            }
            canvas.Restore();
            _destinationHits.Clear();
            for (var i = 0; i < Destinations.Length; i++)
            {
                var rect = layout.Destination(i); var selected = _zone == 1 && _destination == i;
                canvas.Save(); canvas.Scale(_scales[i], _scales[i], rect.MidX, rect.MidY);
                if (selected) PksmPaint.SelectedButton(canvas, rect, 6 * unit); else PksmPaint.BlackButton(canvas, rect, 6 * unit);
                if (Icon(Destinations[i].Icon) is { } icon)
                {
                    var scale = Math.Min(36 * unit / icon.Width, 36 * unit / icon.Height);
                    canvas.DrawBitmap(icon, SKRect.Create(rect.MidX - icon.Width * scale / 2, rect.Top + 10 * unit, icon.Width * scale, icon.Height * scale));
                }
                var labelBounds = SKRect.Inflate(rect, -5 * unit, 0);
                using var font = new SKFont(PixelFont.Face, 15 * unit);
                var words = NativeText.Wrap(Destinations[i].Label, font, labelBounds.Width);
                for (var line = 0; line < Math.Min(2, words.Count); line++)
                    HomePaint.Text(canvas, words[line], labelBounds, rect.Top + (words.Count > 1 ? 61 : 68) * unit + line * 16 * unit, PixelFont.Face, 15 * unit, Pksm.Ink);
                canvas.Restore();
                _destinationHits.Add((SKRect.Create(rect.MidX - rect.Width * _scales[i] / 2, rect.MidY - rect.Height * _scales[i] / 2,
                    rect.Width * _scales[i], rect.Height * _scales[i]), i));
            }
            using var hints = AppChromePaint.HintFont(layout.Footer, Hints, PixelFont.Face, 14 * unit);
            PksmPaint.HintBar(canvas, layout.Footer, Hints, hints);
            _footerHits.Clear(); var hits = PksmPaint.HintBarHitRects(layout.Footer, Hints, hints);
            for (var i = 0; i < hits.Count; i++) _footerHits.Add((hits[i], i));
        }
        private void Focus(int zone, int index = 0)
        {
            index = Math.Clamp(index, 0, 4);
            if (_zone == zone && (zone == 0 || _destination == index)) return;
            _zone = zone; _destination = index;
            for (var i = 0; i < _scales.Length; i++)
            {
                var at = i; var selected = zone == 1 && i == _destination;
                _ = UiDispatcher.Instance.Animations.Animate(this, "tile" + i, _scales[i], selected ? 1.05 : 1,
                    selected ? 130u : 110u, value => _scales[at] = (float)value, selected ? UiEasing.SpringOut : UiEasing.CubicOut);
            }
        }
        private void MoveShelf(int delta)
        {
            Refresh(); if (_groups.Count == 0) return;
            Focus(0); _shelfIndex = Math.Clamp(_shelfIndex < 0 ? 0 : _shelfIndex + delta, 0, _groups.Count - 1);
            _selectedId = _groups[_shelfIndex][0].DocumentId;
            if (_layout is { } layout)
            {
                _scroll.Target = layout.CenteredScroll(_shelfIndex, _groups.Count);
                if (_lastMove != 0 && Stopwatch.GetElapsedTime(_lastMove).TotalMilliseconds < 200) _scroll.Snap(_scroll.Target);
            }
            _lastMove = Stopwatch.GetTimestamp();
        }
        private void Activate()
        {
            if (_zone == 0)
            {
                if (_shelfIndex >= 0) { var group = _groups[_shelfIndex]; _ = host.Screens.RunAsync(() => host.OpenLibraryGroupAsync(group)); }
                else MoveShelf(0);
                return;
            }
            Func<Task> action = _destination switch
            {
                0 => () => { host.OpenStorage(true); return Task.CompletedTask; },
                1 => host.AutopilotAsync, 2 => () => { host.ParkRequested?.Invoke(); return Task.CompletedTask; },
                3 => () => host.GiftsAsync(new(0, 0, null, true)), _ => host.SettingsAsync,
            };
            _ = host.Screens.RunAsync(action);
        }
        public bool Button(PadButton button)
        {
            Refresh();
            switch (button)
            {
                case PadButton.Down when _zone == 0: Focus(1, 0); break;
                case PadButton.Up when _zone == 1: Focus(0); break;
                case PadButton.Left: if (_zone == 0) MoveShelf(-1); else Focus(1, _destination - 1); break;
                case PadButton.Right: if (_zone == 0) MoveShelf(1); else Focus(1, _destination + 1); break;
                case PadButton.A: Activate(); break;
                case PadButton.B: if (_zone == 0 && _shelfIndex >= 0) { var group = _groups[_shelfIndex]; _ = host.Screens.RunAsync(() => host.HomeIdentityAsync(group)); } break;
                case PadButton.L: _ = host.Screens.RunAsync(host.LibraryFilterAsync); break;
                case PadButton.Y: _ = host.Screens.RunAsync(host.LinkFolderAsync); break;
                case PadButton.X: _ = host.Screens.RunAsync(host.OpenFileAsync); break;
                case PadButton.Start: _ = host.Screens.RunAsync(host.SettingsAsync); break;
                case PadButton.R: _ = host.Screens.RunAsync(host.Sessions.Current is null ? () => host.Dialogs.MessageAsync("Restore points", "Open a save first.") : host.RestoreAsync); break;
                case PadButton.Select: host.Screens.Push(new NativeListScreen(host.Screens, "Library actions", host.HomeItems, () => host.Status)); break;
                default: return false;
            }
            return true;
        }
        public void Touch(SKTouchEventArgs touch, bool secondary)
        {
            if (secondary || _layout is not { } layout) return;
            if (touch.ActionType == SKTouchAction.Pressed ? _touchId is not null : _touchId != touch.Id) return;
            if (touch.ActionType == SKTouchAction.Pressed)
            {
                _touchId = touch.Id;
                _pressed = true; _dragging = _longPressed = false; _pressPoint = touch.Location;
                _pressOffset = _scroll.Value; _pressedAt = Stopwatch.GetTimestamp();
                _pressedCart = _cartridgeHits.Where(h => h.Rect.Contains(touch.Location)).Select(h => h.Index).DefaultIfEmpty(-1).First();
            }
            else if (touch.ActionType == SKTouchAction.Moved && _pressed)
            {
                if (Math.Abs(touch.Location.X - _pressPoint.X) > 8 * layout.Unit || Math.Abs(touch.Location.Y - _pressPoint.Y) > 8 * layout.Unit) _dragging = true;
                if (_dragging && layout.Shelf.Contains(_pressPoint)) _scroll.Snap(Math.Clamp(_pressOffset + _pressPoint.X - touch.Location.X, 0, layout.MaxScroll(_groups.Count)));
            }
            else if (touch.ActionType == SKTouchAction.Released && _pressed)
            {
                _pressed = false; _touchId = null; if (_dragging || _longPressed) return;
                if (Math.Abs(touch.Location.X - _pressPoint.X) > 8 * layout.Unit || Math.Abs(touch.Location.Y - _pressPoint.Y) > 8 * layout.Unit) return;
                if (layout.Filter.Contains(touch.Location)) { Button(PadButton.L); return; }
                var cart = _cartridgeHits.FirstOrDefault(h => h.Rect.Contains(touch.Location));
                if (_pressedCart >= 0 && cart.Rect.Width > 0 && cart.Index == _pressedCart)
                { Focus(0); _shelfIndex = cart.Index; _selectedId = _groups[cart.Index][0].DocumentId; Activate(); return; }
                var tile = _destinationHits.FirstOrDefault(h => h.Rect.Contains(touch.Location));
                if (tile.Rect.Width > 0) { Focus(1, tile.Index); Activate(); return; }
                var hint = _footerHits.FirstOrDefault(h => h.Rect.Contains(touch.Location));
                if (hint.Rect.Width > 0) Button(new[] { PadButton.A, PadButton.B, PadButton.L, PadButton.Y, PadButton.X, PadButton.Start }[hint.Index]);
            }
            else if (touch.ActionType == SKTouchAction.Cancelled) { _pressed = false; _touchId = null; }
        }
    }

    private async Task HomeIdentityAsync(IReadOnlyList<DetectedSave> group)
    {
        var index = group.Count == 1 ? 0 : await Dialogs.PickAsync("Which save?", group.Select(s => s.TrainerName + " · " + s.FileName).ToArray());
        if (index is null) return;
        var save = group[index.Value]; var store = Get<ISaveIdentityStore>(); var identity = store.Get(save.DocumentId) ?? new(save.DocumentId);
        var action = await Dialogs.MenuAsync(HomeCardText.Title(save), save.FileName, "Rename", "Cartridge color", "Set game", "Hide from Home", "Reset to detected");
        switch (action)
        {
            case "Rename": var name = await Dialogs.TextAsync("Game name", identity.DisplayName ?? HomeCardText.Title(save)); if (name is not null) store.Set(identity with { DisplayName = string.IsNullOrWhiteSpace(name) ? null : name.Trim() }); break;
            case "Cartridge color": var color = await Dialogs.PickAsync("Cartridge color", new[] { "Console era" }.Concat(SaveIdentityPalette.Swatches.Select(s => s.Name)).ToArray()); if (color is { } c) store.Set(identity with { ColorKey = c == 0 ? null : SaveIdentityPalette.Swatches[c - 1].Key }); break;
            case "Set game": var guess = save.Guess; if (guess is null) break; var choices = SaveIdentityRules.ChoicesFor(guess.Family, guess.Label); var choice = await Dialogs.PickAsync("Game / edition", choices.Select(c => c.Label).ToArray()); if (choice is { } at) store.Set(identity with { GameChoiceId = choices[at].Id }); break;
            case "Hide from Home": store.Set(identity with { Hidden = true }); break;
            case "Reset to detected": store.Reset(save.DocumentId); break;
        }
        Get<SaveCatalogue>().Refresh(); (Screens.Current as HomeScreen)?.RefreshIdentity(); Screens.Invalidate();
    }
}
