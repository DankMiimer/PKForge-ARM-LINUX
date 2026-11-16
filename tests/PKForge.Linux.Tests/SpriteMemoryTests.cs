using PKForge.App.Services;
using Xunit;

namespace PKForge.Linux.Tests;

/// <summary>The park's decoded animations stay bounded on 1 GB handhelds.</summary>
public sealed class SpriteMemoryTests
{
    private const long MiB = 1048576;

    [Fact]
    public void Nothing_is_released_below_the_limit()
    {
        var held = new[] { ("Sleep", 0L, 10 * MiB), ("Eat", 0L, 10 * MiB) };
        Assert.Empty(PokeparkSpriteService.ChooseReleases(held, 20 * MiB, 60_000));
    }

    [Fact]
    public void The_oldest_unused_poses_go_first_until_back_under_the_target()
    {
        var now = 100_000L;
        var held = new[]
        {
            ("Walk", 0L, 10 * MiB),          // never released
            ("Idle", 0L, 10 * MiB),          // never released
            ("Sleep", 10_000L, 10 * MiB),    // oldest
            ("Eat", 20_000L, 10 * MiB),
            ("Attack", 90_000L, 10 * MiB),   // shown 10 s ago: kept
            ("Hop", 30_000L, 10 * MiB),
        };
        var released = PokeparkSpriteService.ChooseReleases(held, 60 * MiB, now);
        // 60 MiB is over the 48 MiB limit; release down to 32 MiB: Sleep, Eat and Hop (oldest first).
        Assert.Equal([2, 3, 5], released);
    }

    [Fact]
    public void Poses_in_use_are_kept_even_over_the_limit()
    {
        var now = 100_000L;
        var held = new[] { ("Sleep", 95_000L, 30 * MiB), ("Eat", 99_000L, 30 * MiB) };
        Assert.Empty(PokeparkSpriteService.ChooseReleases(held, 60 * MiB, now));
    }
}
