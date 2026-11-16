using PKForge.Domain;
using PKForge.Engine.SoulGold;
using Xunit;
using static PKForge.Engine.SoulGold.SoulGoldFormat;

namespace PKForge.Engine.Tests;

/// <summary>
/// Soul Gold natures, EVs and stats (upstream issue #80: "every time I hit save it adds more
/// EVs"). Three errors made a party edit write the wrong stats: the hidden-nature modifier was
/// read as the nature itself (every Pokémon without a mint showed as Hardy), natures raised
/// Sp. Atk where they meant Speed, and EVs were read with the write order (Spe's showed as
/// SpD). The first three tests need no save; the last runs on any real Soul Gold save in
/// .local-testdata/romhacks (gitignored) and skips without one.
/// </summary>
public sealed class SoulGoldStatTests
{
    private static SoulGoldMon Mon(StorageEra era = StorageEra.LegacyV19) =>
        new(new byte[PartyMonSize], 0, true, era, SoulGoldData.Instance);

    [Fact]
    public void The_nature_is_the_pids_unless_a_mint_changed_it()
    {
        var mon = Mon();
        mon.Pid = 0x1234_5678 - 0x1234_5678 % 25 + 17;   // PID nature 17 (Quiet)
        mon.Language = 2;
        Assert.Equal(17, mon.Nature);                    // modifier 0: the PID's nature

        var pid = mon.Pid;
        mon.Nature = 10;                                 // a Timid mint
        Assert.Equal(10, mon.Nature);
        Assert.Equal(pid, mon.Pid);                      // shininess and identity untouched
        Assert.Equal(2, mon.Language);
        Assert.Equal((10 ^ 17) << 3 | 2, mon.Buffer[0x14]);

        mon.Nature = 17;                                 // back to the PID's own nature
        Assert.Equal(2, mon.Buffer[0x14]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Evs_read_back_in_the_order_they_were_written(bool bx21)
    {
        var era = bx21 ? StorageEra.Bx21 : StorageEra.LegacyV19;
        var mon = Mon(era);
        int[] app = [1, 2, 3, 4, 5, 6];                  // HP, Atk, Def, SpA, SpD, Spe
        mon.Evs = app;
        Assert.Equal(app, mon.Evs);
        if (era == StorageEra.LegacyV19)
            // The game's order: HP, Atk, Def, Spe, SpA, SpD.
            Assert.Equal(new byte[] { 1, 2, 3, 6, 4, 5 }, mon.Buffer.AsSpan(0x20 + 0x18, 6).ToArray());
    }

    [Fact]
    public void A_speed_nature_raises_speed_and_lowers_its_own_stat()
    {
        var data = SoulGoldData.Instance;
        var species = data.SpeciesFromNational(1);       // Bulbasaur
        int[] ivs = [31, 31, 31, 31, 31, 31], evs = [0, 0, 0, 0, 0, 0];
        var neutral = data.ComputeStats(species, 50, 0, ivs, evs);
        var timid = data.ComputeStats(species, 50, 10, ivs, evs);   // + Spe, - Atk
        var modest = data.ComputeStats(species, 50, 15, ivs, evs);  // + SpA, - Atk
        Assert.Equal(neutral[5] * 110 / 100, timid[5]);
        Assert.Equal(neutral[1] * 90 / 100, timid[1]);
        Assert.Equal(neutral[3], timid[3]);
        Assert.Equal(neutral[3] * 110 / 100, modest[3]);
        Assert.Equal(neutral[5], modest[5]);
    }

    [Fact]
    public void Stats_recomputed_after_an_edit_are_the_games_own()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "PKForge.sln")))
            directory = directory.Parent;
        var folder = directory is null ? null : Path.Combine(directory.FullName, ".local-testdata", "romhacks");
        var saves = folder is not null && Directory.Exists(folder) ? Directory.GetFiles(folder, "soulgold*.sav") : [];
        foreach (var path in saves)
        {
            var sample = File.ReadAllBytes(path);
            if (!LooksLike(sample)) continue;
            using var session = new SoulGoldEngineSession(sample);
            for (var slot = 0; slot < 6; slot++)
            {
                var before = session.ReadEntity(-1, slot);
                if (before.IsEmpty) continue;
                // A change that leaves stats alone still makes the engine recompute them.
                session.ApplyEdit(-1, slot, new EntityEdit(Friendship: before.Friendship == 255 ? 254 : before.Friendship + 1));
                Assert.Equal(before.Stats, session.ReadEntity(-1, slot).Stats);
            }
        }
    }
}
