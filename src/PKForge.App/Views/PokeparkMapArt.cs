using PKForge.Chrome;
using PKForge.Domain;
using SkiaSharp;

namespace PKForge.App.Views;

/// <summary>Loads a park map's layers from the app package: ground, objects and the objects' depth.</summary>
public static class PokeparkMapArt
{
    public static async Task<ParkArt> LoadAsync(ParkMap map)
    {
        var ground = await DecodeAsync(map, "ground.png").ConfigureAwait(false);
        var objects = await DecodeAsync(map, "objects.png").ConfigureAwait(false);
        var depth = await DecodeAsync(map, "depth.png").ConfigureAwait(false);
        return await Task.Run(() => ParkArt.Cut(ground, objects, depth)).ConfigureAwait(false);
    }

    private static async Task<SKBitmap> DecodeAsync(ParkMap map, string file)
    {
        var path = $"pokepark/{map.Id}/{file}";
        await using var stream = await FileSystem.OpenAppPackageFileAsync(path).ConfigureAwait(false);
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer).ConfigureAwait(false);
        var bitmap = SKBitmap.Decode(buffer.ToArray()) ?? throw new InvalidDataException($"Invalid Poképark art: {path}");
        if (bitmap.Width == map.Width && bitmap.Height == map.Height) return bitmap;
        bitmap.Dispose();
        throw new InvalidDataException($"{path} does not match the {map.Width}×{map.Height} map.");
    }
}
