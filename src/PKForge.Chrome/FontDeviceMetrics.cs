using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using SkiaSharp;

namespace PKForge.Chrome;

/// <summary>Device advances authored in a TrueType font, as used by Android's native labels.</summary>
public static class FontDeviceMetrics
{
    private sealed record Records(Dictionary<int, byte[]> Widths);
    private static readonly ConditionalWeakTable<SKTypeface, Records> Cache = new();
    private const uint Hdmx = 0x68646D78;
    private const uint Maxp = 0x6D617870;

    public static bool TryAdvance(SKFont font, ushort glyph, out float advance)
    {
        var widths = Advances(font).Span;
        advance = glyph < widths.Length ? widths[glyph] : 0;
        return glyph < widths.Length;
    }

    public static ReadOnlyMemory<byte> Advances(SKFont font)
    {
        if (font.Typeface is not { } face || font.LinearMetrics || font.Subpixel || font.ScaleX != 1 || font.SkewX != 0) return ReadOnlyMemory<byte>.Empty;
        // Native Android labels select the integer ppem (12.5 uses the 12px record).
        var size = (int)font.Size;
        var records = Cache.GetValue(face, Read);
        return records.Widths.TryGetValue(size, out var widths) ? widths : ReadOnlyMemory<byte>.Empty;
    }

    private static Records Read(SKTypeface face)
    {
        if (!face.TryGetTableData(Hdmx, out var table) || !face.TryGetTableData(Maxp, out var maxp) || maxp.Length < 6)
            return new([]);
        return new(Parse(table, BinaryPrimitives.ReadUInt16BigEndian(maxp.AsSpan(4))));
    }

    // https://learn.microsoft.com/en-us/typography/opentype/spec/hdmx
    internal static Dictionary<int, byte[]> Parse(ReadOnlySpan<byte> table, int glyphs)
    {
        Dictionary<int, byte[]> result = [];
        if (table.Length < 8 || glyphs <= 0 || BinaryPrimitives.ReadUInt16BigEndian(table) != 0) return result;
        var count = BinaryPrimitives.ReadUInt16BigEndian(table[2..]);
        var size = BinaryPrimitives.ReadUInt32BigEndian(table[4..]);
        if (size < glyphs + 2L || size % 4 != 0 || 8L + count * (long)size > table.Length) return result;
        for (var i = 0; i < count; i++)
        {
            var record = table.Slice(checked(8 + (int)(i * (long)size)), (int)size);
            result[record[0]] = record.Slice(2, glyphs).ToArray();
        }
        return result;
    }
}
