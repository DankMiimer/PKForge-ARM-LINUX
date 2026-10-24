using System.Globalization;
using System.Text;
using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Application;
using PKForge.Domain;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace PKForge.Linux.Workspace;

public sealed class NativeDialogs(Grid host, GamepadRouter router)
{
    public Grid Host => host;
    public Task MessageAsync(string title, string message) => PadMenu.ShowAsync(host, title, message, "OK");
    public Task<bool> ConfirmAsync(string title, string message, string action = "Apply") => PadMenu.ConfirmAsync(host, title, message, action);
    public Task<string?> MenuAsync(string title, string message, params string[] options) => PadMenu.ShowAsync(host, title, message, options);
    public async Task<int?> PickAsync(string title, IReadOnlyList<string> labels, int start = 0)
    {
        var query = "";
        while (true)
        {
            var filtered = labels.Select((label, index) => (label, index)).Where(p => p.label.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
            var actions = new[] { "Search…", "Clear search" }.Concat(filtered.Select(p => $"{p.index + 1}. {p.label}")).ToArray();
            var choice = await PadMenu.ShowAtAsync(host, title, query.Length == 0 ? $"{labels.Count} entries" : "Search: " + query,
                Math.Clamp(start + 2, 0, actions.Length - 1), actions.Select(s => new PadOption(s)).ToArray());
            if (choice is null) return null;
            if (choice == actions[0]) { query = await TextAsync("Search", query) ?? query; continue; }
            if (choice == actions[1]) { query = ""; continue; }
            return filtered[Array.IndexOf(actions, choice) - 2].index;
        }
    }
    public Task<string?> TextAsync(string title, string initial = "", string? alphabet = null, GameTextRule? rule = null) => new Keyboard(host, router, title, initial, alphabet, rule)._result.Task;
    public async Task<long?> NumberAsync(string title, long initial, long minimum = 0, long maximum = int.MaxValue)
    {
        while (true)
        {
            var text = await TextAsync(title + $" ({minimum}–{maximum})", initial.ToString(CultureInfo.InvariantCulture), "0123456789-ABCDEFx.");
            if (text is null) return null;
            if ((text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? long.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
                : long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) && value >= minimum && value <= maximum) return value;
            await MessageAsync("Value unavailable", "Enter a number in the displayed range.");
        }
    }
    private sealed class Keyboard : IPadHandler
    {
        internal readonly TaskCompletionSource<string?> _result = new();
        private readonly Grid _host; private readonly GamepadRouter _router; private readonly string _title;
        private readonly SKCanvasView _view = new() { EnableTouchEvents = true };
        private readonly List<(SKRect Rect, int Index)> _hits = [];
        private string _text; private string[] _keys; private int _index; private int _page; private bool _closed;
        private readonly string? _alphabet;
        private readonly GameTextRule? _rule;
        private static readonly string[] Pages = ["abcdefghijklmnopqrstuvwxyz0123456789-_' .", "ABCDEFGHIJKLMNOPQRSTUVWXYZ!@#$%^&*()+/:;?", "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろわをん", "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワヲン"];
        public Keyboard(Grid host, GamepadRouter router, string title, string initial, string? alphabet, GameTextRule? rule = null)
        {
            _host = host; _router = router; _title = title; _text = initial; _alphabet = alphabet; _rule = rule;
            _keys = Keys(); _view.PaintSurface += Paint;
            _view.Touch += (_, e) => { e.Handled = true; if (e.ActionType == SKTouchAction.Released) { var hit = _hits.FirstOrDefault(h => h.Rect.Contains(e.Location)); if (hit.Rect.Width > 0) { _index = hit.Index; Activate(); } } };
            host.Add(_view); router.Push(this); PKForge.App.App.Suspended += Cancel;
        }
        private string[] Keys() => (_alphabet ?? Pages[_page]).Select(c => c.ToString()).Where(c => _rule?.Accepts(c) != false).Concat(_alphabet is null ? ["Space", "Delete", "Page", "U+", "Enter", "Copy", "Paste", "Done", "Cancel"] : new[] { "Space", "Delete", "U+", "Copy", "Paste", "Done", "Cancel" }).ToArray();
        private void Paint(object? sender, SKPaintSurfaceEventArgs e)
        {
            var c = e.Surface.Canvas; c.Clear(new SKColor(13, 20, 32));
            using var ink = new SKPaint { Color = SKColors.White, IsAntialias = true };
            using var font = new SKFont(PixelFont.FallbackFace, 18);
            c.DrawText(NativeText.Fit(_title, font, e.Info.Width - 30), 15, 30, font, ink);
            c.DrawText(NativeText.Fit(_text + "▏", font, e.Info.Width - 30), 15, 69, font, ink);
            var columns = 10; var rows = (_keys.Length + columns - 1) / columns;
            var height = Math.Min(44, (e.Info.Height - 145f) / rows); var width = (e.Info.Width - 20f) / columns;
            _hits.Clear();
            for (var i = 0; i < _keys.Length; i++)
            {
                var rect = SKRect.Create(10 + i % columns * width, 95 + i / columns * height, width - 3, height - 3);
                using var back = new SKPaint { Color = i == _index ? new SKColor(34, 102, 157) : new SKColor(29, 43, 61) };
                c.DrawRoundRect(rect, 4, 4, back);
                c.DrawText(_keys[i], rect.MidX, rect.MidY + 6, SKTextAlign.Center, font, ink); _hits.Add((rect, i));
            }
            using var small = new SKFont(PixelFont.FallbackFace, 13);
            c.DrawText("D-pad: choose · A: type · X: delete · Y: page · Start: done · B: cancel", 12, e.Info.Height - 18, small, ink);
        }
        public bool OnPadButton(PadButton button)
        {
            switch (button)
            {
                case PadButton.Left: _index = Math.Max(0, _index - 1); break;
                case PadButton.Right: _index = Math.Min(_keys.Length - 1, _index + 1); break;
                case PadButton.Up: _index = Math.Max(0, _index - 10); break;
                case PadButton.Down: _index = Math.Min(_keys.Length - 1, _index + 10); break;
                case PadButton.A: Activate(); break;
                case PadButton.X: Delete(); break;
                case PadButton.Y: Page(); break;
                case PadButton.Start: Close(_text); break;
                case PadButton.B: Cancel(); break;
            }
            _view.InvalidateSurface(); return true;
        }
        private void Activate()
        {
            var key = _keys[_index];
            switch (key)
            {
                case "Done": if (_rule?.Accepts(_text) != false) Close(_text); break;
                case "Cancel": Cancel(); break;
                case "Delete": Delete(); break;
                case "Page": Page(); break;
                case "Space": Append(" "); break;
                case "Enter": Append("\n"); break;
                case "Copy": Preferences.Default.Set("linux.clipboard.v1", _text); break;
                case "Paste": Append(Preferences.Default.Get("linux.clipboard.v1", "")); break;
                case "U+": _ = UnicodeAsync(); break;
                default: Append(key); break;
            }
            _view.InvalidateSurface();
        }
        private void Append(string value)
        {
            if (_rule?.Accepts(_text + value) == false) { _ = PadMenu.ShowAsync(_host, "Game text", $"This game stores at most {_rule.MaximumLength} characters and cannot encode that text.", "OK"); return; }
            _text += value;
        }
        private async Task UnicodeAsync()
        {
            var input = new Keyboard(_host, _router, "Unicode code point (hex)", "", "0123456789ABCDEF");
            var value = await input._result.Task;
            if (int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var scalar) && Rune.IsValid(scalar)) Append(char.ConvertFromUtf32(scalar));
            _view.InvalidateSurface();
        }
        private void Page() { if (_alphabet is not null) return; _page = (_page + 1) % Pages.Length; _keys = Keys(); _index = Math.Min(_index, _keys.Length - 1); }
        private void Delete() { var starts = StringInfo.ParseCombiningCharacters(_text); if (starts.Length > 0) _text = _text[..starts[^1]]; }
        private void Cancel() => Close(null);
        private void Close(string? value) { if (_closed) return; if (value is not null && _rule?.Accepts(value) == false) { _ = PadMenu.ShowAsync(_host, "Game text", "The current text exceeds the game's limit or cannot be encoded.", "OK"); return; } _closed = true; PKForge.App.App.Suspended -= Cancel; _router.Remove(this); _host.Remove(_view); _result.TrySetResult(value); }
    }
}

public sealed class NativeFileDialogs(NativeDialogs dialogs, string dataDirectory) : IFileDialogService, IFolderPicker
{
    public async Task<IReadOnlyList<PickedDocument>> PickAsync(FileDialogRequest request, CancellationToken cancellationToken = default)
    {
        var directory = request.InitialDirectory ?? await StartingDirectory(request.Title);
        if (directory is null) return [];
        var selected = new HashSet<string>(StringComparer.Ordinal); var cursor = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var entries = Directory.EnumerateDirectories(directory).Order(StringComparer.OrdinalIgnoreCase)
                    .Concat(Directory.EnumerateFiles(directory).Where(p => FileKinds.Accepts(request.Purpose, p)).Order(StringComparer.OrdinalIgnoreCase)).ToArray();
                var labels = new[] { "[..] Parent folder", "Done (" + selected.Count + " selected)" }.Concat(entries.Select((p, i) => $"{i + 1}. {(Directory.Exists(p) ? "[Folder] " : selected.Contains(p) ? "[✓] " : "")}{Path.GetFileName(p)}")).ToArray();
                var choice = await PadMenu.ShowAtAsync(dialogs.Host, request.Title, directory, Math.Clamp(cursor, 0, labels.Length - 1), labels.Select(s => new PadOption(s)).ToArray());
                if (choice is null) return [];
                cursor = Array.IndexOf(labels, choice);
                if (cursor == 0) { directory = Directory.GetParent(directory)?.FullName ?? directory; cursor = 0; continue; }
                if (cursor == 1) return selected.Select(p => new PickedDocument(LocalPaths.Canonical(p), Path.GetFileName(p))).ToArray();
                var path = entries[cursor - 2];
                if (Directory.Exists(path)) { directory = path; cursor = 0; continue; }
                if (!request.Multiple) return [new(LocalPaths.Canonical(path), Path.GetFileName(path))];
                if (!selected.Remove(path)) selected.Add(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { await dialogs.MessageAsync("Folder unavailable", error.Message); return []; }
        }
    }
    public async Task<PickedFolder?> PickFolderAsync(string title, CancellationToken cancellationToken = default)
    {
        var directory = await StartingDirectory(title);
        while (directory is not null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var children = Directory.GetDirectories(directory).Order(StringComparer.OrdinalIgnoreCase).ToArray();
                var choices = new[] { "Use this folder", "[..] Parent folder" }.Concat(children.Select(Path.GetFileName)).OfType<string>().ToArray();
                var choice = await dialogs.MenuAsync(title, directory, choices);
                if (choice is null) return null;
                if (choice == choices[0]) return new(LocalPaths.Canonical(directory), Path.GetFileName(directory));
                if (choice == choices[1]) directory = Directory.GetParent(directory)?.FullName ?? directory;
                else directory = children[Array.IndexOf(choices, choice) - 2];
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { await dialogs.MessageAsync(title, error.Message); return null; }
        }
        return null;
    }
    public async ValueTask<PickedFolder?> PickFolderAsync(CancellationToken cancellationToken = default) => await PickFolderAsync("Choose folder", cancellationToken);
    public async Task<string?> ExportPathAsync(string suggestedName, CancellationToken cancellationToken = default)
    {
        var folder = await PickFolderAsync("Export to folder", cancellationToken);
        if (folder is null) return null;
        var name = await dialogs.TextAsync("File name", Path.GetFileName(suggestedName));
        if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name)) return null;
        var path = Path.Combine(folder.TreeId, name);
        if (File.Exists(path) && !await dialogs.ConfirmAsync("Replace file?", path, "Replace")) return null;
        return path;
    }
    private async Task<string?> StartingDirectory(string title)
    {
        var imports = Path.Combine(dataDirectory, "imports"); Directory.CreateDirectory(imports);
        var roots = new[] { imports, Path.Combine(dataDirectory, "exports"), "/storage/roms", "/storage/.config", "/userdata/saves", "/userdata/roms", "/userdata/system/configs", Path.GetPathRoot(dataDirectory)! }.Where(Directory.Exists).Distinct().ToArray();
        var index = await dialogs.PickAsync(title, roots);
        return index is { } i ? roots[i] : null;
    }
}
