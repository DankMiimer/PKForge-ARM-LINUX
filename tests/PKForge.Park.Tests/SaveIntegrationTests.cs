using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using PKForge.App.Services;
using PKForge.Domain;
using PKForge.Engine;
using PKForge.Infrastructure;
using PKForge.Park;
using PKHeX.Core;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
namespace PKForge.Park.Tests;

public sealed class SaveIntegrationTests
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pkforge-park-tests", Guid.NewGuid().ToString("N"));
    private readonly SaveEngine _engine = new();
    private readonly LinuxSaveFileAccess _access = new();
    private readonly FileBackupService _backups;
    private readonly SaveSessionService _sessions;
    private readonly SaveMutationService _mutations;
    private readonly string _save;
    public SaveIntegrationTests()
    {
        Directory.CreateDirectory(_directory);
        ParkPlatform.Configure(_directory, _directory);
        _save = Path.Combine(_directory, "test.sav");
        File.WriteAllBytes(_save, Fixture());
        _backups = new(Path.Combine(_directory, "backups"));
        _sessions = new(_access, _engine);
        _mutations = new(_sessions, new SafeSaveWriter(_engine, _backups, _access));
    }
    private Task Open() => _sessions.OpenAsync(new(_save, "test.sav")).AsTask();
    internal static byte[] Fixture()
    {
        var bytes = new byte[0x20000];
        for (var sector = 0; sector < 14; sector++)
        {
            var offset = sector * 0x1000;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 0xFF4), (ushort)sector);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 0xFF8), 0x08012025);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset + 0xFFC), 1);
        }
        bytes.AsSpan(0xE000).Fill(0xFF);
        if (!SaveUtil.TryGetSaveFile(bytes, out var save) || save is null) throw new InvalidDataException("Fixture did not parse.");
        for (var i = 0; i < 2; i++)
        {
            var mon = new PK3 { Species = (ushort)(i == 0 ? 25 : 150), PID = 0x12345678u + (uint)i, TID16 = 1, EXP = 1000, Language = 2, OriginalTrainerName = "PKF", Nickname = "MON" };
            mon.RefreshChecksum(); save.SetBoxSlotAtIndex(mon, 0, i, EntityImportSettings.None);
        }
        return save.Write().ToArray();
    }
    [Fact]
    public async Task Scoped_release_keeps_other_slots_and_backs_up_original()
    {
        await Open();
        var original = await File.ReadAllBytesAsync(_save);
        var other = _sessions.CurrentSession!.ExportSlot(0, 1).Data;
        Assert.True(await _mutations.RunSlotMutationAsync(s => { s.ReleaseSlot(0, 0); return new(true, "Release"); }, new(0, 0), action: SaveAction.Release), _mutations.Status);
        Assert.True(_sessions.CurrentSession!.ReadEntity(0, 0).IsEmpty);
        Assert.Equal(other, _sessions.CurrentSession.ExportSlot(0, 1).Data);
        var point = Assert.Single(await _backups.ListAsync());
        Assert.Equal(original, (await _backups.ReadAsync(point.BackupId)).ToArray());
        using var reopened = _engine.OpenSession(await File.ReadAllBytesAsync(_save));
        Assert.True(reopened.ReadEntity(0, 0).IsEmpty);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }
    [Fact]
    public async Task Out_of_scope_mutation_is_refused_and_live_session_is_rolled_back()
    {
        await Open();
        var before = await File.ReadAllBytesAsync(_save);
        Assert.False(await _mutations.RunSlotMutationAsync(s => { s.ReleaseSlot(0, 1); return new(true, "Wrong slot"); }, new(0, 0)));
        Assert.Equal(before, await File.ReadAllBytesAsync(_save));
        Assert.False(_sessions.CurrentSession!.ReadEntity(0, 1).IsEmpty);
        Assert.Empty(await _backups.ListAsync());
    }
    [Fact]
    public async Task External_change_cannot_be_overwritten()
    {
        await Open();
        var external = await File.ReadAllBytesAsync(_save); external[123] ^= 0x10;
        await File.WriteAllBytesAsync(_save, external);
        Assert.False(await _mutations.RunSlotMutationAsync(s => { s.ReleaseSlot(0, 0); return new(true, "Release"); }, new(0, 0)));
        Assert.Equal(external, await File.ReadAllBytesAsync(_save));
        Assert.Contains("changed", _mutations.Status, StringComparison.OrdinalIgnoreCase);
        Assert.False(_sessions.CurrentSession!.ReadEntity(0, 0).IsEmpty);
    }
    [Fact]
    public async Task No_op_does_not_create_restore_point()
    {
        await Open();
        var before = await File.ReadAllBytesAsync(_save);
        Assert.True(await _mutations.RunSlotMutationAsync(_ => new(true, "Nothing"), new(0, 0)));
        Assert.Empty(await _backups.ListAsync());
        Assert.Equal(before, await File.ReadAllBytesAsync(_save));
    }
    [Fact]
    public async Task Hardcore_blocks_operation_before_it_runs()
    {
        await Open(); HardcoreMode.Set(true);
        var ran = false;
        Assert.False(await _mutations.RunSlotMutationAsync(_ => { ran = true; return new(true, "Edit"); }, new(0, 0)));
        Assert.False(ran); Assert.Empty(await _backups.ListAsync());
    }
    [Fact]
    public async Task Daycare_writes_growth_through_shared_transaction()
    {
        await Open();
        var bank = new FileBankService(Path.Combine(_directory, "bank"));
        var park = new PokeparkService(bank, _sessions, new GameDataService());
        park.AddSaveVisitor(0, 0);
        var mon = Assert.Single(park.LoadRoster());
        var daycare = new PokeparkDaycareService(park, bank, _engine, _mutations);
        daycare.CheckIn(mon);
        var state = JsonNode.Parse(ParkPlatform.Settings.Get("pkforge.pokepark.daycare.v1", ""))!;
        state["Stays"]![0]!["Since"] = DateTimeOffset.Now.AddHours(-2);
        ParkPlatform.Settings.Set("pkforge.pokepark.daycare.v1", state.ToJsonString());
        var before = _sessions.CurrentSession!.ReadEntity(0, 0).Level;
        var other = _sessions.CurrentSession.ExportSlot(0, 1).Data;
        var result = await daycare.PickUpAsync(mon);
        Assert.Contains("save is updated", result);
        Assert.True(_sessions.CurrentSession!.ReadEntity(0, 0).Level > before);
        Assert.Equal(other, _sessions.CurrentSession.ExportSlot(0, 1).Data);
        Assert.Single(await _backups.ListAsync());
    }
    [Fact]
    public void Settings_round_trip_preserves_upstream_keys_and_types()
    {
        var path = Path.Combine(_directory, "preferences.json");
        var store = new JsonSettingsStore(path);
        store.Set("pkforge.pokepark.captions.v1", false);
        store.Set("pkforge.pokepark.residents.v1", "[{\"Id\":\"example\"}]");
        var reopened = new JsonSettingsStore(path);
        Assert.False(reopened.Get("pkforge.pokepark.captions.v1", true));
        Assert.Equal("[{\"Id\":\"example\"}]", reopened.Get("pkforge.pokepark.residents.v1", ""));
        Assert.False(File.Exists(path + ".tmp"));
    }
}
