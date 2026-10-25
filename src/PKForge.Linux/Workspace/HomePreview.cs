using PKForge.Domain;
using SkiaSharp;

namespace PKForge.Linux.Workspace;

public sealed partial class WorkspaceHost
{
    private readonly Dictionary<string, SKBitmap?> _homeArt = [];
    private void ClearHomeArt() { foreach (var art in _homeArt.Values) art?.Dispose(); _homeArt.Clear(); }
    private SKBitmap? HomeArt(string folder, string label)
    {
        var name = label.StartsWith("Pokémon ", StringComparison.Ordinal) ? label[8..] : label;
        foreach (var variant in new[] { name, name.Split(" / ")[0] }.Distinct())
        {
            var slug = string.Concat(variant.ToLowerInvariant().Select(c => char.IsAscii(c) && char.IsLetterOrDigit(c) ? c : '-'));
            var path = Path.Combine(ParkPlatform.AssetDirectory, folder, slug + ".png");
            if (!_homeArt.TryGetValue(path, out var art))
            {
                if (_homeArt.Count > 12) ClearHomeArt();
                _homeArt[path] = art = File.Exists(path) ? SKBitmap.Decode(path) : null;
            }
            if (art is not null) return art;
        }
        return null;
    }
    private void PaintGamePreview(SKCanvas canvas, SKRect bounds, DetectedSave? save)
    {
        NativePaint.Background(canvas, bounds, save?.GameLabel ?? "PKForge · Library");
        if (save is null) { NativePaint.Paragraph(canvas, bounds, Status); return; }
        var label = save.ArtLabel ?? save.GameLabel;
        var art = HomeArt("gamehero", label) ?? HomeArt("gameart", label);
        if (art is not null)
        {
            var height = bounds.Height * .48f; var scale = Math.Min((bounds.Width - 24) / art.Width, height / art.Height);
            canvas.DrawBitmap(art, SKRect.Create(bounds.MidX - art.Width * scale / 2, 58, art.Width * scale, art.Height * scale));
        }
        NativePaint.Paragraph(canvas, new(0, bounds.Height * .5f, bounds.Width, bounds.Height), $"{save.TrainerName} · Gen {save.Generation}\nPlay time: {save.PlayTime}\n{save.FileName}\n{save.DocumentId}\n{save.Emulator}" );
    }
}
