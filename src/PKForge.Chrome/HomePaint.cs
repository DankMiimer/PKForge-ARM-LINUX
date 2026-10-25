using PKForge.Domain;
using SkiaSharp;

namespace PKForge.Chrome;

/// <summary>The cartridge shelf's original Android labels, shared with native renderers.</summary>
public static class HomeCardText
{
    public static string Title(DetectedSave save)
    {
        var label = save.GameLabel.Normalize(System.Text.NormalizationForm.FormC);
        var typed = save.Identity is { } identity && identity.DisplayName != identity.GameLabel;
        return !typed && label.StartsWith("Pokémon ", StringComparison.Ordinal) ? label[8..] : label;
    }
    public static string Trainer(IReadOnlyList<DetectedSave> saves)
    {
        if (saves.Count > 1)
        {
            var trainers = saves.Select(s => s.TrainerName).Where(t => !string.IsNullOrEmpty(t)).Distinct().ToArray();
            return trainers.Length switch
            {
                0 => $"{saves.Count} saves", 1 => $"{trainers[0]} · {saves.Count} saves",
                2 => $"{trainers[0]}, {trainers[1]}", _ => $"{trainers[0]}, {trainers[1]} +{trainers.Length - 2}",
            };
        }
        var save = saves[0];
        return string.IsNullOrEmpty(save.TrainerName) ? save.FileName : string.IsNullOrEmpty(save.PlayTime) ? save.TrainerName : $"{save.TrainerName} · {save.PlayTime}";
    }
    public static string Detail(IReadOnlyList<DetectedSave> saves)
    {
        var emulators = saves.Select(s => Emulator(s.Emulator)).Distinct().ToArray();
        var parts = new List<string> { emulators.Length <= 2 ? string.Join(", ", emulators) : $"{emulators.Length} emulators" };
        if (saves.Max(s => s.LastModified) is { } latest) parts.Add(latest.ToLocalTime().ToString("MMM d, yyyy"));
        return string.Join(" · ", parts);
    }
    public static string Emulator(EmulatorKind kind) => kind switch
    {
        EmulatorKind.MelonDS => "melonDS", EmulatorKind.DraStic => "DraStic", EmulatorKind.PizzaBoyGba => "Pizza Boy GBA",
        EmulatorKind.PizzaBoyGbc => "Pizza Boy GBC", EmulatorKind.CitraMmj => "Citra MMJ", _ => kind.ToString(),
    };
}

/// <summary>Android Home's design-unit geometry: 144×166 cartridges and five 88-high destinations.</summary>
public sealed record HomeLayout(float Unit, SKRect Header, SKRect Filter, SKRect Shelf, SKRect Destinations, SKRect Footer)
{
    public static HomeLayout Create(SKRect bounds)
    {
        var unit = Math.Clamp(bounds.Height / 480f, .75f, 2f);
        var footer = SKRect.Create(bounds.Left, bounds.Bottom - 42 * unit, bounds.Width, 42 * unit);
        var tiles = new SKRect(bounds.Left + 14 * unit, footer.Top - 98 * unit, bounds.Right - 14 * unit, footer.Top - 10 * unit);
        var header = SKRect.Create(bounds.Left + 14 * unit, bounds.Top + 64 * unit, bounds.Width - 28 * unit, 26 * unit);
        return new(unit, header, SKRect.Create(header.Right - 100 * unit, header.Top, 100 * unit, header.Height),
            new(header.Left, header.Bottom + 6 * unit, header.Right, tiles.Top - 10 * unit), tiles, footer);
    }
    public SKRect Destination(int index) => SKRect.Create(Destinations.Left + index * (Destinations.Width + 10 * Unit) / 5,
        Destinations.Top, (Destinations.Width - 40 * Unit) / 5, Destinations.Height);
    public SKRect Cartridge(int index, float offset) => SKRect.Create(Shelf.Left + index * 156 * Unit - offset,
        Shelf.MidY - 83 * Unit, 144 * Unit, 166 * Unit);
    public float MaxScroll(int count) => Math.Max(0, (count * 156 - 12) * Unit - Shelf.Width);
    public float CenteredScroll(int index, int count) => Math.Clamp((index * 156 + 72) * Unit - Shelf.Width / 2, 0, MaxScroll(count));
}

public static class HomePaint
{
    public static SKColor SaveColor(string? key, int generation) => key switch
    {
        "navy" => SKColor.Parse("#34509A"), "sky" => SKColor.Parse("#4FB6DB"), "teal" => SKColor.Parse("#2FA39A"),
        "leaf" => SKColor.Parse("#5DAE4A"), "gold" => SKColor.Parse("#E2B53A"), "amber" => SKColor.Parse("#EE9A4A"),
        "coral" => SKColor.Parse("#E4675A"), "berry" => SKColor.Parse("#C0457A"), "violet" => SKColor.Parse("#8B6AD8"),
        "slate" => SKColor.Parse("#5E6B7A"), _ => SKColor.Parse(generation switch
        {
            1 or 2 => "#9AA6B2", 3 => "#8B7BD8", 4 => "#5E86D4", 5 => "#4E5A68", 6 => "#4FB6DB",
            7 => "#EE9A4A", 8 => "#D45C6D", 9 => "#9A6AD8", _ => Pksm.InkSoft.ToString(),
        }),
    };

    public static void Cartridge(SKCanvas c, SKRect rect, IReadOnlyList<DetectedSave> saves, SKBitmap? art, bool selected, SKTypeface face, float unit, SKTypeface? fallback = null)
    {
        var save = saves[0];
        PksmPaint.Panel(c, rect, selected ? Pksm.SelectFill : Pksm.Paper, 6 * unit);
        if (selected)
        {
            using var edge = new SKPaint { Color = Pksm.Ink.WithAlpha(217), Style = SKPaintStyle.Stroke, StrokeWidth = 2 * unit, IsAntialias = true };
            c.DrawRoundRect(SKRect.Inflate(rect, -unit, -unit), 6 * unit, 6 * unit, edge);
        }
        var cart = SKRect.Create(rect.MidX - 38 * unit, rect.Top + 12 * unit, 76 * unit, 84 * unit);
        var plastic = SaveColor(save.Identity?.ColorKey, save.Generation);
        void Body(float rotation, float dx, float dy)
        {
            c.Save(); c.Translate(dx * unit, dy * unit); c.RotateDegrees(rotation, cart.MidX, cart.MidY);
            using var fill = new SKPaint { Color = plastic, IsAntialias = true }; c.DrawRoundRect(cart, 7 * unit, 7 * unit, fill);
            plastic.ToHsl(out var h, out var s, out var l);
            using var edge = new SKPaint { Color = SKColor.FromHsl(h, s, Math.Max(0, l - 15)), IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f * unit };
            c.DrawRoundRect(cart, 7 * unit, 7 * unit, edge);
            c.ClipRoundRect(new SKRoundRect(cart, 7 * unit, 7 * unit), antialias: true);
            fill.Color = Pksm.ButtonBlueDeep; c.DrawRect(cart.Left, cart.Top, cart.Width, 9 * unit, fill); c.Restore();
        }
        if (saves.Count > 2) Body(-9, -13, 0);
        if (saves.Count > 1) Body(8, 13, -1);
        Body(0, 0, 0);
        var sticker = new SKRect(cart.Left + 7 * unit, cart.Top + 14 * unit, cart.Right - 7 * unit, cart.Bottom - 7 * unit);
        using (var fill = new SKPaint { Color = Pksm.PaperShade, IsAntialias = true }) c.DrawRoundRect(sticker, 4 * unit, 4 * unit, fill);
        using (var edge = new SKPaint { Color = Pksm.ButtonBlueDeep, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = unit }) c.DrawRoundRect(sticker, 4 * unit, 4 * unit, edge);
        var image = SKRect.Inflate(sticker, -3 * unit, -3 * unit);
        if (art is not null)
        {
            var scale = Math.Min(image.Width / art.Width, image.Height / art.Height);
            var target = SKRect.Create(image.MidX - art.Width * scale / 2, image.MidY - art.Height * scale / 2, art.Width * scale, art.Height * scale);
            c.DrawBitmap(art, target);
        }
        else
        {
            using var ink = new SKPaint { Color = Pksm.LogoCyan, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 3 * unit };
            c.DrawCircle(image.MidX, image.MidY, image.Width * .27f, ink);
            c.DrawLine(image.Left + image.Width * .23f, image.MidY, image.Right - image.Width * .23f, image.MidY, ink);
            Text(c, "G" + save.Generation, image, image.Bottom - 2 * unit, face, 11 * unit, Pksm.Ink);
        }
        if (saves.Count > 1)
        {
            var badge = SKRect.Create(rect.Right - 39 * unit, cart.Bottom - 23 * unit, 30 * unit, 22 * unit);
            PksmPaint.BlackButton(c, badge, 3 * unit); Text(c, "×" + saves.Count, badge, badge.MidY + 4 * unit, face, 12.5f * unit, Pksm.Ink);
        }
        var text = new SKRect(rect.Left + 11 * unit, rect.Top, rect.Right - 11 * unit, rect.Bottom);
        Text(c, HomeCardText.Title(save), text, rect.Top + 117 * unit, face, 14 * unit, Pksm.Ink, fallback);
        Text(c, HomeCardText.Trainer(saves), text, rect.Top + 136 * unit, face, 12.5f * unit, Pksm.Ink, fallback);
        Text(c, HomeCardText.Detail(saves), text, rect.Top + 155 * unit, face, 12 * unit, Pksm.InkSoft, fallback);
    }

    public static void Text(SKCanvas c, string text, SKRect rect, float baseline, SKTypeface face, float size, SKColor color, SKTypeface? fallback = null)
    {
        var actualFace = fallback is not null && text.Any(c => face.GetGlyph(c) == 0) ? fallback : face;
        using var font = new SKFont(actualFace, size); using var ink = new SKPaint { Color = color, IsAntialias = true };
        var fitted = text;
        if (font.MeasureText(text) > rect.Width)
        {
            while (fitted.Length > 0 && font.MeasureText(fitted + "…") > rect.Width) fitted = fitted[..^1];
            fitted += "…";
        }
        c.DrawText(fitted, rect.MidX, baseline, SKTextAlign.Center, font, ink);
    }
}
