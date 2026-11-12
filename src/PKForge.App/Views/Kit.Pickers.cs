using Microsoft.Maui.Controls.Shapes;
using PKForge.App.Theme;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace PKForge.App.Views;

public static partial class Kit
{
    // ---------- Text ----------

    private static readonly HashSet<string> KeepCaps = new(StringComparer.Ordinal)
    {
        "PK", "PKM", "PKX", "QR", "IV", "EV", "OT", "TID", "SID", "RNG", "HP", "PP", "TM", "TR", "HM", "EXP",
        "ID", "GB", "GBC", "GBA", "NDS", "DS", "3DS", "NS", "SAV", "PKSM", "HOME", "OK", "ROM", "PC", "NPC",
        "SAE", "RTC", "USB", "SD", "BST", "JP", "EN", "FR", "DE", "IT", "ES", "KO", "CHS", "CHT", "HGSS", "BW",
        "B2W2", "XY", "ORAS", "SM", "USUM", "LGPE", "SWSH", "BDSP", "PLA", "SV", "FRLG", "RSE", "CFRU", "DPP",
        "UI", "GTS", "PID", "EC", "II", "III", "VI", "XD", "URL", "CSV", "JSON", "HEX",
    };

    private static readonly Dictionary<string, string> Proper = new(StringComparer.Ordinal)
    {
        ["PKFORGE"] = "PKForge", ["SHOWDOWN"] = "Showdown", ["POKÉMON"] = "Pokémon", ["POKéMON"] = "Pokémon",
        ["POKEMON"] = "Pokémon", ["POKÉPARK"] = "Poképark", ["POKéPARK"] = "Poképark", ["POKÉDEX"] = "Pokédex",
        ["POKéDEX"] = "Pokédex", ["PKHEX"] = "PKHeX", ["NUZLOCKE"] = "Nuzlocke", ["POKÉRUS"] = "Pokérus", ["POKéRUS"] = "Pokérus",
    };

    /// <summary>
    /// Normal case for labels that arrive SHOUTED ("SAVE CHANGES" → "Save changes"), keeping
    /// real acronyms (QR, IVs, .PK, OT, TID...). Text with any lowercase is left untouched.
    /// Display only: callers keep matching on their original caption strings.
    /// </summary>
    public static string Tidy(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text ?? "";
        var letters = 0;
        foreach (var ch in text)
        {
            if (char.IsLower(ch)) return text;
            if (char.IsLetter(ch)) letters++;
        }
        if (letters < 3) return text;
        var sb = new System.Text.StringBuilder(text.Length);
        var first = true;
        var i = 0;
        while (i < text.Length)
        {
            if (!char.IsLetterOrDigit(text[i])) { sb.Append(text[i]); i++; continue; }
            var start = i;
            while (i < text.Length && (char.IsLetterOrDigit(text[i]) || text[i] == '\'')) i++;
            var word = text[start..i];
            string outWord;
            // "PASTE A SET": the article follows a word; "A USE · B BACK": the button letter follows a separator.
            var prev = sb.ToString();
            var trimmed = prev.TrimEnd();
            var afterWord = trimmed.Length > 0 && trimmed.Length < prev.Length && char.IsLetter(trimmed[^1]);
            if (word == "A" && afterWord) outWord = "a";
            else if (KeepCaps.Contains(word) || word.Length == 1) outWord = word;
            else if (Proper.TryGetValue(word, out var proper)) outWord = proper;
            else if (word.Length > 2 && word.EndsWith('S') && KeepCaps.Contains(word[..^1])) outWord = word[..^1] + "s";
            else if (word.Any(char.IsDigit) && word.Length <= 4) outWord = word;
            else
            {
                var lower = word.ToLowerInvariant();
                outWord = first ? char.ToUpperInvariant(lower[0]) + lower[1..] : lower;
            }
            if (word.Any(char.IsLetter)) first = false;
            sb.Append(outWord);
        }
        return sb.ToString();
    }

    // The designer's drawn keys, by footer glyph; the others keep the drawn disc.
    private static readonly Dictionary<string, string> KeyArt = new()
    {
        ["A"] = "key_a", ["B"] = "key_b", ["X"] = "key_x", ["Y"] = "key_y",
        ["L"] = "key_l", ["R"] = "key_r", ["LR"] = "key_lr",
        ["+"] = "key_plus", ["-"] = "key_minus", ["−"] = "key_minus",
    };

    private static readonly Dictionary<string, SKImage?> KeyImages = new();

    private static SKImage? KeyImage(string name)
    {
        if (KeyImages.TryGetValue(name, out var image)) return image;
        try
        {
            using var stream = FileSystem.OpenAppPackageFileAsync($"ui/design/{name}.png").GetAwaiter().GetResult();
            using var bitmap = SKBitmap.Decode(stream);
            image = bitmap is null ? null : SKImage.FromBitmap(bitmap);
        }
        catch (Exception error) when (error is IOException or FileNotFoundException) { image = null; }
        KeyImages[name] = image;
        return image;
    }

    /// <summary>
    /// A footer key: the designer's pixel button tinted cyan for the console's buttons, and a
    /// round cyan disc with the glyph for the rest (TAP, ↑↓ ...).
    /// </summary>
    public static Border GlyphKey(string glyph, Action? onTap = null)
    {
        View face;
        if (KeyArt.TryGetValue(glyph, out var art) && KeyImage(art) is { } image)
        {
            var canvas = new SKCanvasView { InputTransparent = true };
            canvas.PaintSurface += (_, args) =>
            {
                var c = args.Surface.Canvas;
                c.Clear(SKColors.Transparent);
                using var tint = new SKPaint { ColorFilter = SKColorFilter.CreateBlendMode(UiTokens.BagCyanEdge.ToSKColor(), SKBlendMode.SrcIn) };
                var side = Math.Min(args.Info.Width, args.Info.Height);
                var dest = SKRect.Create((args.Info.Width - side) / 2f, (args.Info.Height - side) / 2f, side, side);
                c.DrawImage(image, dest, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None), tint);
            };
            face = canvas;
        }
        else face = new Label
        {
            Text = glyph, FontFamily = DsChrome.PixelFont, TextColor = UiTokens.OnAccent, FontSize = UiTokens.TextLabel,
            VerticalTextAlignment = TextAlignment.Center, HorizontalTextAlignment = TextAlignment.Center,
        };
        var drawn = face is SKCanvasView;
        var key = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 11 },
            BackgroundColor = drawn ? Colors.Transparent : UiTokens.BagCyanEdge,
            Padding = new Thickness(!drawn && glyph.Length > 1 ? 7 : 0, 0),
            MinimumWidthRequest = 22,
            WidthRequest = drawn ? 22 : -1,
            HeightRequest = 22,
            VerticalOptions = LayoutOptions.Center,
            Content = face,
        };
        if (onTap is not null)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => onTap();
            key.GestureRecognizers.Add(tap);
        }
        return key;
    }

    /// <summary>
    /// A tab / segment in the menu-button language: resting navy + cobalt edge, active =
    /// the context accent with the pale rim. Re-point with <see cref="SetTab"/>.
    /// </summary>
    public static Border Tab(string text, bool active = false, Color? accent = null)
    {
        var tab = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = UiTokens.ControlRadius },
            Padding = new Thickness(12, 5),
            Content = new Label
            {
                Text = Tidy(text), FontFamily = DsChrome.PixelFont, FontSize = UiTokens.TextLabel,
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center,
                LineBreakMode = LineBreakMode.NoWrap,
            },
        };
        SetTab(tab, active, accent);
        return tab;
    }

    public static void SetTab(Border tab, bool active, Color? accent = null)
    {
        var body = accent ?? UiTokens.AccentNeutral;
        tab.Background = active
            ? new LinearGradientBrush([new GradientStop(body.WithLuminosity(Math.Min(1, body.GetLuminosity() + 0.08f)), 0), new GradientStop(body, 1)], new Point(0, 0), new Point(0, 1))
            : new LinearGradientBrush([new GradientStop(UiTokens.ButtonTop, 0), new GradientStop(UiTokens.ButtonBottom, 1)], new Point(0, 0), new Point(0, 1));
        tab.Stroke = active ? UiTokens.Rim : UiTokens.ButtonEdge;
        tab.StrokeThickness = active ? 1.2 : UiTokens.ControlEdge;
        if (tab.Content is Label label) label.TextColor = active ? UiTokens.Bright : UiTokens.InkSoft;
    }


    /// <summary>A 1 dp cobalt hairline between groups inside a panel.</summary>
    public static BoxView Divider(double verticalMargin = 4) => new()
    {
        Color = UiTokens.Divider,
        HeightRequest = 1,
        Margin = new Thickness(0, verticalMargin),
        InputTransparent = true,
    };


    /// <summary>
    /// The hint row inside a window: the same key discs on the window's own panel, set off
    /// by a hairline - never a second bordered panel nested in the first.
    /// </summary>
    public static Border WindowHints(params (string Glyph, string Label, Action? OnTap)[] hints) => new()
    {
        BackgroundColor = Colors.Transparent,
        StrokeThickness = 0,
        Padding = new Thickness(0, 0, 0, 2),
        Content = new VerticalStackLayout { Spacing = 7, Children = { Divider(0), HintRow(hints) } },
    };

    private static HorizontalStackLayout HintRow((string Glyph, string Label, Action? OnTap)[] hints)
    {
        var row = new HorizontalStackLayout { Spacing = 22, HorizontalOptions = LayoutOptions.Center };
        foreach (var (glyph, label, onTap) in hints)
        {
            var item = new HorizontalStackLayout
            {
                Spacing = 7,
                Children =
                {
                    GlyphKey(glyph),
                    new Label { Text = Tidy(label), FontFamily = DsChrome.PixelFont, TextColor = UiTokens.Ink0, FontSize = UiTokens.TextTitle, VerticalTextAlignment = TextAlignment.Center },
                },
            };
            if (onTap is not null)
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += (_, _) => onTap();
                item.GestureRecognizers.Add(tap);
            }
            row.Children.Add(item);
        }
        return row;
    }

}
