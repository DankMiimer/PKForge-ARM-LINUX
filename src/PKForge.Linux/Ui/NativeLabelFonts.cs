using PKForge.App.Views;
using SkiaSharp;

namespace Microsoft.Maui.Controls;

// Android's native labels use system Roboto for symbols missing from NDS12.
// Bundle that exact reference face so firmware font installations cannot change
// wrapping or baselines. The shared Skia painters keep their original M PLUS face.
internal static class NativeLabelFonts
{
    private static readonly Lazy<SKTypeface> Roboto = new(() =>
        SKTypeface.FromFile(Path.Combine(AppContext.BaseDirectory, "assets", "Roboto-Regular.ttf"))
        ?? PixelFont.FallbackFace);

    public static SKTypeface AndroidFallback => Roboto.Value;
    public static SKTypeface ForMissingGlyph(int codepoint) =>
        AndroidFallback.GetGlyph(codepoint) != 0 ? AndroidFallback : PixelFont.FallbackFace;
}
