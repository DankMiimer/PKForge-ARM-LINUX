using SkiaSharp;

namespace PKForge.App.Services;

public sealed partial class PokeparkSpriteService
{
    /// <summary>Above this, decoded animations nobody has shown for a while are released.</summary>
    public const long TrimAboveBytes = 48L * 1048576;
    /// <summary>Releasing stops once the decoded animations are down to this.</summary>
    public const long TrimToBytes = 32L * 1048576;
    /// <summary>An animation shown within this many milliseconds is kept.</summary>
    public const long KeepIfShownWithinMs = 20_000;
    private long _nextTrim;

    /// <summary>Released animations so far (count, bytes), for the log.</summary>
    public (int Count, long Bytes) Released { get; private set; }

    /// <summary>What the decoded animation sheets hold in memory: species, animations, bytes.</summary>
    public (int Species, int Animations, long Bytes) Decoded()
    {
        lock (_gate)
        {
            int species = 0, animations = 0; long bytes = 0;
            foreach (var sheet in _sheets.Values)
            {
                if (sheet is null) continue;
                species++;
                foreach (var animation in sheet.Animations.Values)
                {
                    if (animation is null) continue;
                    animations++;
                    bytes += animation.Bitmap.ByteCount;
                }
            }
            return (species, animations, bytes);
        }
    }

    /// <summary>
    /// Every pose a resident has played stayed decoded: about 20 MiB more a minute on an RG DS,
    /// 124 MiB after six minutes. Walk and Idle stay; any other animation not shown for 20 s is
    /// released, oldest first, once the total passes 48 MiB, down to 32 MiB. A released pose
    /// decodes again from the disk cache the next time a resident needs it.
    /// </summary>
    partial void AfterFrame()
    {
        var now = Environment.TickCount64;
        if (now < _nextTrim) return;
        _nextTrim = now + 2000;
        var released = new List<SKBitmap>();
        lock (_gate)
        {
            var held = _sheets.Values.Where(s => s is not null)
                .SelectMany(s => s!.Animations.Where(a => a.Value is not null).Select(a => (Sheet: s!, Name: a.Key, Animation: a.Value!)))
                .ToArray();
            var total = held.Sum(h => (long)h.Animation.Bitmap.ByteCount);
            var choice = ChooseReleases(held.Select(h => (h.Name, h.Animation.LastUsed, (long)h.Animation.Bitmap.ByteCount)).ToArray(), total, now);
            foreach (var index in choice)
            {
                var (sheet, name, animation) = held[index];
                sheet.Animations.Remove(name);
                sheet.Loading.Remove(name);   // so the next request decodes it again
                released.Add(animation.Bitmap);
            }
        }
        if (released.Count == 0) return;
        var bytes = released.Sum(b => (long)b.ByteCount);
        foreach (var bitmap in released) bitmap.Dispose();
        Released = (Released.Count + released.Count, Released.Bytes + bytes);
        Console.WriteLine($"SPRITES released {released.Count} animations not shown for {KeepIfShownWithinMs / 1000} s ({bytes / 1048576.0:F1} MiB)");
    }

    /// <summary>Which of the held animations to release (indexes into <paramref name="held"/>), oldest first.</summary>
    public static IReadOnlyList<int> ChooseReleases(IReadOnlyList<(string Name, long LastUsed, long Bytes)> held, long total, long now)
    {
        if (total <= TrimAboveBytes) return [];
        var result = new List<int>();
        foreach (var i in Enumerable.Range(0, held.Count).OrderBy(i => held[i].LastUsed))
        {
            if (total <= TrimToBytes) break;
            var (name, lastUsed, bytes) = held[i];
            if (name is "Walk" or "Idle" || now - lastUsed < KeepIfShownWithinMs) continue;
            result.Add(i); total -= bytes;
        }
        return result;
    }
}
