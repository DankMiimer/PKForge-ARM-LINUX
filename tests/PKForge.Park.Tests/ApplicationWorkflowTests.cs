using PKForge.Application;
using PKForge.Domain;
using PKForge.Engine;
using PKForge.Infrastructure;
using PKForge.Park;
using PKHeX.Core;
using Xunit;

namespace PKForge.Park.Tests;

public sealed class ApplicationWorkflowTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pkforge-application-tests", Guid.NewGuid().ToString("N"));
    private readonly SaveEngine _engine = new();
    private readonly LinuxSaveFileAccess _access = new();
    private readonly FileBackupService _backups;
    private readonly SafeSaveWriter _writer;
    public ApplicationWorkflowTests()
    {
        Directory.CreateDirectory(_root); ParkPlatform.Configure(_root, _root);
        _backups = new(Path.Combine(_root, "backups")); _writer = new(_engine, _backups, _access);
    }
    private string PathOf(string name) => Path.Combine(_root, name);
    private RecoveryJournal Journal() => new(PathOf("transactions"), _engine, _access, _writer);
    [Theory] [InlineData(3)] [InlineData(6)]
    public void Shared_audit_uses_format_correct_fingerprints_and_preserves_the_save(int generation)
    {
        using var session = generation == 3 ? _engine.OpenSession(SaveIntegrationTests.Fixture()) : _engine.OpenBlankSession(generation);
        if (generation == 3) session.ReleaseSlot(0, 1);
        PKM mon = generation == 3 ? new PK3() : new PK6();
        mon.Species = 25; mon.Language = 2; mon.Nickname = "CLONE"; mon.OriginalTrainerName = "PKF"; mon.PID = 123; mon.EncryptionConstant = 456; mon.RefreshChecksum();
        var bytes = new byte[mon.SIZE_PARTY]; mon.WriteDecryptedDataParty(bytes);
        Assert.True(session.ImportSlot(0, 0, bytes, "clone.pk" + generation)); Assert.True(session.ImportSlot(0, 2, bytes, "clone.pk" + generation));
        var before = session.Serialize().ToArray();
        var report = AuditWorkflow.Analyze(session, new LegalityService());
        Assert.Equal(2, report.TotalChecked); var clones = Assert.Single(report.CloneGroups);
        Assert.Equal(generation >= 6 ? "EC + PID" : "PID + OT + TID", clones.KeyKind);
        Assert.Equal(new[] { "B1-1", "B1-3" }, clones.Members.Select(member => member.SlotLabel));
        Assert.Contains(report.Lines, line => line.Contains("match ×2")); Assert.Equal(before, session.Serialize().ToArray());
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => AuditWorkflow.Analyze(session, new LegalityService(), cancellationToken: cancellation.Token));
        Assert.Equal(before, session.Serialize().ToArray());
    }
    [Theory] [InlineData("PK6")] [InlineData("PK7")] [InlineData("PK8")] [InlineData("PB8")] [InlineData("PK9")] [InlineData("PA9")]
    public void Legacy_QR_candidates_preserve_exact_formats_without_changing_the_envelope(string format)
    {
        PKM mon = format switch { "PK6" => new PK6(), "PK7" => new PK7(), "PK8" => new PK8(), "PB8" => new PB8(), "PK9" => new PK9(), "PA9" => new PA9(), _ => throw new ArgumentException(format) };
        mon.Species = 25; mon.Language = 2; mon.Nickname = "QR"; mon.OriginalTrainerName = "PKF"; mon.RefreshChecksum();
        var bytes = new byte[mon.SIZE_PARTY]; mon.WriteDecryptedDataParty(bytes);
        var envelope = QrEntityCodec.MakePayload(bytes, mon.Format, "Pikachu");
        Assert.Equal(1, envelope[4]);
        var payload = QrEntityCodec.TryParse(envelope)!;
        var candidates = QrImportWorkflow.DescribeCandidates(_engine, payload);
        var selected = Assert.Single(candidates, candidate => candidate.Format == format);
        Assert.All(candidates, candidate => Assert.Equal(mon.Format, candidate.Generation));
        var bank = new FileBankService(PathOf("bank")); var deposited = bank.Add(payload.EntityBytes, selected);
        Assert.Equal(format, deposited.Info.Format); Assert.Equal(bytes, bank.GetData(deposited.Id));
        if (mon.Format is 8 or 9) Assert.True(candidates.Count >= 2, "Same-size formats must require an explicit choice.");
    }
    [Fact]
    public async Task Collection_sources_preserve_exact_bank_ids_and_save_slot_locations()
    {
        var folder = PathOf("saves"); Directory.CreateDirectory(folder);
        var openPath = Path.Combine(folder, "open.sav"); var otherPath = Path.Combine(folder, "other.srm");
        File.WriteAllBytes(openPath, SaveIntegrationTests.Fixture()); File.WriteAllBytes(otherPath, SaveIntegrationTests.Fixture());
        var roots = new LocalWatchedRoots(PathOf("roots.json")); roots.AddRoot(new(EmulatorKind.RetroArch, folder, "Saves"));
        var catalogue = new SaveCatalogue(roots, new LocalEmulatorScanner(_engine), new JsonSaveIdentityStore(PathOf("identities.json"))); await catalogue.ScanAsync();
        var sessions = new SaveSessionService(_access, _engine); await sessions.OpenAsync(new(LocalPaths.Canonical(openPath), "Open cartridge"));
        var bank = new FileBankService(PathOf("bank")); var entity = sessions.CurrentSession!.ExportSlot(0, 0);
        var entry = bank.AddMany([new(entity.Data, _engine.TryDescribeEntity(entity.Data, "fixture", entity.Format)!, 1, 3)]).Single();
        var snapshot = await new CollectionWorkflow(bank, sessions, catalogue, _engine, _access).ReadAsync();
        var bankMember = Assert.Single(snapshot.Members, m => m.BankId is not null);
        Assert.Equal(entry.Id, bankMember.BankId); Assert.Equal("Bank · Box 2, Slot 4", bankMember.Location);
        var open = snapshot.Members.Where(m => m.DocumentId == LocalPaths.Canonical(openPath)).ToArray();
        Assert.Equal(sessions.CurrentSession.Snapshot.Slots.Count(s => s.Species is > 0 && !s.IsEgg), open.Length);
        Assert.All(open, m => Assert.Equal("Open cartridge", m.Source));
        Assert.Contains(open, m => m.Box == 0 && m.Slot == 0 && m.Location == "Open cartridge · Box 1, Slot 1");
        Assert.Contains(snapshot.Members, m => m.DocumentId == LocalPaths.Canonical(otherPath) && m.Source.Contains("other.srm"));
        Assert.Empty(snapshot.Diagnostics);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Shared_workflows_refuse_removing_the_last_usable_party_mon(bool journaled)
    {
        SaveUtil.TryGetSaveFile(SaveIntegrationTests.Fixture(), out var save);
        save!.SetPartySlotAtIndex(new PK3 { Species = 25, Language = 2, Nickname = "LAST", OriginalTrainerName = "PKF", PID = 123 }, 0, EntityImportSettings.None);
        var path = PathOf("last-party.sav"); var original = save.Write().ToArray(); File.WriteAllBytes(path, original);
        var sessions = new SaveSessionService(_access, _engine); await sessions.OpenAsync(new(path, "last-party.sav"));
        if (journaled)
        {
            using var edited = _engine.OpenSession(original); edited.ReleaseSlot(-1, 0);
            await Assert.ThrowsAsync<InvalidOperationException>(() => Journal().PrepareAsync("Unsafe move", [new(path, original, edited.Serialize().ToArray(), IsSave: true, Scope: WriteScope.Only(new SlotRef(-1, 0)))]));
            Assert.Empty(Journal().Pending());
        }
        else
        {
            var operations = new SaveOperations(sessions, _writer, new(_ => null), _engine);
            Assert.False(await operations.ExecuteAsync(s => { s.ReleaseSlot(-1, 0); return new(true, "Release"); }, WriteScope.Only(new SlotRef(-1, 0)), SaveAction.Release));
            Assert.Contains("usable", operations.Status);
        }
        Assert.Equal(original, File.ReadAllBytes(path)); Assert.Empty(await _backups.ListAsync());
    }
    [Fact]
    public void Staged_entity_replacement_never_modifies_live_bank_and_failed_index_write_restores_payload()
    {
        var directory = PathOf("bank"); var raw = new FileBankService(directory);
        using var save = _engine.OpenSession(SaveIntegrationTests.Fixture()); var exported = save.ExportSlot(0, 0);
        var info = _engine.TryDescribeEntity(exported.Data, "fixture", exported.Format)!; var entry = raw.Add(exported.Data, info);
        var changed = exported.Data.ToArray(); changed[^1] ^= 1;
        using (var stage = new BankStaging(directory, PathOf("staging")))
        {
            stage.Bank.Replace(entry.Id, changed, info);
            Assert.Equal(exported.Data, raw.GetData(entry.Id)); Assert.Equal(changed, stage.Bank.GetData(entry.Id));
            Assert.Contains(stage.Changes(), change => Path.GetFileName(change.Path) == entry.Id.ToString("N") + ".bin" && change.Before!.AsSpan().SequenceEqual(exported.Data) && change.After!.AsSpan().SequenceEqual(changed));
        }
        Assert.Equal(exported.Data, raw.GetData(entry.Id));
        Directory.CreateDirectory(Path.Combine(directory, "index.json.tmp"));
        Assert.ThrowsAny<Exception>(() => raw.Replace(entry.Id, changed, info with { Nickname = "CHANGED" }));
        Assert.Equal(exported.Data, raw.GetData(entry.Id)); Assert.Equal(info, Assert.Single(raw.GetAll()).Info);
    }
    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)]
    public async Task Failed_destination_or_bank_write_before_publication_is_recoverable(int failureAt, bool restore)
    {
        var source = PathOf("source.bin"); var target = PathOf("destination.bin"); var index = PathOf("index.json");
        File.WriteAllBytes(source, [1, 2, 3]); File.WriteAllBytes(index, [7]);
        var journal = Journal(); var record = await journal.PrepareAsync("move", [new(target, null, [1, 2, 3], 0), new(index, [7], [8], 1), new(source, [1, 2, 3], null, 2)]);
        journal.BeforePublish = step => { if (step == failureAt) throw new IOException("Injected unavailable destination / full storage / failed Bank publication"); };
        await Assert.ThrowsAsync<IOException>(() => journal.CommitAsync(record.Id));
        Assert.True(File.Exists(source)); Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(source));
        await Journal().RecoverAsync(record.Id, restore);
        Assert.Equal(restore, File.Exists(source)); Assert.Equal(!restore, File.Exists(target)); Assert.Empty(Journal().Pending());
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(restore ? source : target));
        Assert.Equal(restore ? new byte[] { 7 } : [8], File.ReadAllBytes(index));
    }
    [Fact]
    public async Task Preview_aliases_preserve_resident_ids_locks_identity_and_restore_points_without_rewriting_data()
    {
        var canonical = PathOf("canonical.sav"); var legacy = PathOf("legacy-alias.sav"); var bytes = SaveIntegrationTests.Fixture();
        File.WriteAllBytes(canonical, bytes); File.WriteAllBytes(legacy, bytes);
        var oldSessions = new SaveSessionService(_access, _engine); await oldSessions.OpenAsync(new(legacy, "legacy.sav"));
        var bank = new FileBankService(PathOf("bank")); var oldPark = new PKForge.App.Services.PokeparkService(bank, oldSessions, new GameDataService());
        oldPark.AddSaveVisitor(0, 0); var resident = Assert.Single(oldPark.LoadRoster());
        var oldProtection = new PKForge.App.Services.ProtectionStore(); oldProtection.ToggleBox(legacy, 0);
        var identities = new JsonSaveIdentityStore(PathOf("save-identities.json")); identities.Set(new(legacy, "My Emerald", GameChoiceId: "emerald"));
        await _backups.CreateAsync(oldSessions.Current!.Snapshot, "Preview restore", legacy);
        var settings = File.ReadAllBytes(PathOf("settings.json")); var photo = PathOf("photo.png"); File.WriteAllBytes(photo, [1, 2, 3]);
        var aliases = new DocumentAliases(_root, path => path == legacy ? canonical : path);
        var sessions = new SaveSessionService(_access, _engine); await sessions.OpenAsync(new(canonical, "canonical.sav"));
        var park = new PKForge.App.Services.PokeparkService(bank, sessions, new GameDataService(), aliases);
        Assert.NotNull(park.SourceOf(resident)); Assert.Equal(resident.Id, Assert.Single(park.LoadRoster()).Id);
        Assert.Contains(park.GetCandidates(), candidate => resident.Id.StartsWith(candidate.Id + ":", StringComparison.Ordinal));
        var protections = new PKForge.App.Services.ProtectionStore(aliases); Assert.True(protections.IsBoxLocked(canonical, 0));
        var adapted = new AliasedSaveIdentityStore(identities, aliases); Assert.Equal("My Emerald", adapted.Get(canonical)!.DisplayName);
        Assert.Contains(await _backups.ListAsync(), backup => aliases.For(canonical).Any(id => backup.BelongsTo(id, sessions.Current!.Snapshot)));
        Assert.Equal(settings, File.ReadAllBytes(PathOf("settings.json"))); Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(photo));
        protections.ToggleBox(canonical, 0); Assert.False(new PKForge.App.Services.ProtectionStore().IsBoxLocked(legacy, 0));
        adapted.Reset(canonical); Assert.Null(identities.Get(legacy));
    }
    [Fact]
    public async Task Party_release_compacts_only_the_party_and_preserves_box_entities()
    {
        SaveUtil.TryGetSaveFile(SaveIntegrationTests.Fixture(), out var save);
        for (var i = 0; i < 3; i++) save!.SetPartySlotAtIndex(new PK3 { Species = (ushort)(25 + i), Language = 2, Nickname = "PARTY" + i, OriginalTrainerName = "PKF", PID = (uint)(123 + i) }, i, EntityImportSettings.None);
        var path = PathOf("party.sav"); File.WriteAllBytes(path, save!.Write().ToArray());
        var sessions = new SaveSessionService(_access, _engine); await sessions.OpenAsync(new(path, "party.sav"));
        var before = sessions.Current!.Snapshot.OriginalBytes.ToArray(); var expected = sessions.CurrentSession!.ExportSlot(-1, 1).Data;
        var operations = new SaveOperations(sessions, _writer, new(_ => null), _engine);
        Assert.True(await operations.ExecuteAsync(s => { s.ReleaseSlot(-1, 0); return new(true, "Release party slot"); }, WriteScope.Only(new SlotRef(-1, 0)), SaveAction.Release), operations.Status);
        Assert.Equal(expected, sessions.CurrentSession!.ExportSlot(-1, 0).Data);
        using var original = _engine.OpenSession(before);
        foreach (var at in original.Snapshot.Slots.Where(s => s.Box >= 0)) Assert.Equal(original.ExportSlot(at.Box, at.Slot).Data, sessions.CurrentSession.ExportSlot(at.Box, at.Slot).Data);
        Assert.Single(await _backups.ListAsync());
    }
    [Fact]
    public async Task Raw_everything_scope_cannot_bypass_read_only_identity()
    {
        var path = PathOf("readonly.sav"); var original = SaveIntegrationTests.Fixture(); File.WriteAllBytes(path, original);
        var identities = new JsonSaveIdentityStore(PathOf("identities.json")); identities.Set(new(path, GameChoiceId: "hack-cfru"));
        var sessions = new SaveSessionService(_access, _engine); await sessions.OpenAsync(new(path, "readonly.sav"));
        var operations = new SaveOperations(sessions, new SafeSaveWriter(_engine, _backups, _access, identities), new(_ => null), _engine, identities);
        var candidate = original.ToArray(); candidate[100] ^= 1;
        Assert.False(await operations.WriteBytesAsync(candidate, WriteScope.Everything, SaveAction.WriteRawBytes, "Raw edit"));
        Assert.Contains("refused", operations.Status, StringComparison.OrdinalIgnoreCase); Assert.Equal(original, File.ReadAllBytes(path)); Assert.Empty(await _backups.ListAsync());
    }
    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)]
    public async Task Process_restart_completes_or_restores_each_partial_commit(int failureAt, bool restore)
    {
        var source = PathOf("source.bin"); var target = PathOf("destination.bin"); var index = PathOf("index.json");
        File.WriteAllBytes(source, [1, 2, 3]); File.WriteAllBytes(index, [7]);
        var journal = Journal(); var record = await journal.PrepareAsync("move", [new(target, null, [1, 2, 3], 0), new(index, [7], [8], 1), new(source, [1, 2, 3], null, 2)]);
        journal.AfterPublish = step => { if (step == failureAt) throw new IOException("Simulated power loss"); };
        await Assert.ThrowsAsync<IOException>(() => journal.CommitAsync(record.Id));
        var restarted = Journal(); Assert.Single(restarted.Pending()); await restarted.RecoverAsync(record.Id, restore);
        Assert.Empty(restarted.Pending());
        Assert.Equal(restore, File.Exists(source)); Assert.Equal(!restore, File.Exists(target));
        Assert.Equal(restore ? new byte[] { 7 } : [8], File.ReadAllBytes(index));
        Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(restore ? source : target));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Recovery_refuses_external_edit_before_touching_any_participant(bool restore)
    {
        var source = PathOf("source.bin"); var target = PathOf("target.bin"); File.WriteAllBytes(source, [1]);
        var journal = Journal(); var record = await journal.PrepareAsync("move", [new(target, null, [1]), new(source, [1], null, 2)]);
        File.WriteAllBytes(source, [9]);
        await Assert.ThrowsAsync<IOException>(() => Journal().RecoverAsync(record.Id, restore));
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(source)); Assert.False(File.Exists(target)); Assert.Single(journal.Pending());
    }
    [Fact]
    public async Task Staging_refuses_stale_source_and_invalid_save_before_journaling()
    {
        var path = PathOf("save.sav"); var original = SaveIntegrationTests.Fixture(); File.WriteAllBytes(path, original);
        await Assert.ThrowsAsync<IOException>(() => Journal().PrepareAsync("stale", [new(path, [3], [4])]));
        await Assert.ThrowsAsync<UnsafeSaveWriteException>(() => Journal().PrepareAsync("invalid", [new(path, original, [0], IsSave: true)]));
        Assert.Empty(Journal().Pending()); Assert.Equal(original, File.ReadAllBytes(path));
    }
    [Fact]
    public async Task Scratch_edits_leave_live_session_unchanged_until_validated_commit()
    {
        var path = PathOf("save.sav"); File.WriteAllBytes(path, SaveIntegrationTests.Fixture());
        var sessions = new SaveSessionService(_access, _engine); await sessions.OpenAsync(new(path, "test.sav"));
        var operations = new SaveOperations(sessions, _writer, new(_ => null), _engine);
        var unrelated = sessions.CurrentSession!.ExportSlot(0, 1).Data; var original = File.ReadAllBytes(path);
        Assert.True(await operations.ExecuteAsync(s =>
        {
            s.ApplyEdit(0, 0, new(Nickname: "NATIVE"));
            Assert.Equal("MON", sessions.CurrentSession!.ReadEntity(0, 0).Nickname);
            return new(true, "Edited nickname");
        }, WriteScope.Only(new SlotRef(0, 0)), SaveAction.EditMon), operations.Status);
        Assert.Equal("NATIVE", sessions.CurrentSession!.ReadEntity(0, 0).Nickname);
        Assert.Equal(unrelated, sessions.CurrentSession.ExportSlot(0, 1).Data);
        var backup = Assert.Single(await _backups.ListAsync()); Assert.Equal(original, (await _backups.ReadAsync(backup.BackupId)).ToArray());
        Assert.True(await operations.WriteBytesAsync(original, WriteScope.Everything, SaveAction.Backup, "Restore"));
        Assert.Equal(original, File.ReadAllBytes(path));
    }
    [Fact]
    public async Task Failed_scratch_operation_cannot_leak_into_the_next_write()
    {
        var path = PathOf("save.sav"); File.WriteAllBytes(path, SaveIntegrationTests.Fixture());
        var sessions = new SaveSessionService(_access, _engine); await sessions.OpenAsync(new(path, "test.sav"));
        var original = File.ReadAllBytes(path); var operations = new SaveOperations(sessions, _writer, new(_ => null), _engine);
        Assert.False(await operations.ExecuteAsync(s => { s.ReleaseSlot(0, 1); return new(false, "Refused"); }, WriteScope.Only(new SlotRef(0, 0)), SaveAction.EditMon));
        Assert.False(sessions.CurrentSession!.ReadEntity(0, 1).IsEmpty); Assert.Equal(original, File.ReadAllBytes(path)); Assert.Empty(await _backups.ListAsync());
        Assert.False(await operations.ExecuteAsync(s => { s.ReleaseSlot(0, 1); return new(true, "Wrong scope"); }, WriteScope.Only(new SlotRef(0, 0)), SaveAction.Release));
        Assert.Equal(original, File.ReadAllBytes(path));
    }
    [Fact]
    public void Journaled_Bank_preserves_exact_format_id_and_original_bytes_on_edit()
    {
        var directory = PathOf("bank"); var raw = new FileBankService(directory); var journal = Journal();
        var bank = new JournaledBankService(raw, directory, PathOf("staging"), journal, new(_ => null));
        var mon = new PB8 { Species = 25, Nickname = "BEFORE", OriginalTrainerName = "TEST", Language = 2 }; mon.RefreshChecksum();
        var bytes = new byte[mon.SIZE_PARTY]; mon.WriteDecryptedDataParty(bytes); var info = _engine.TryDescribeEntity(bytes, "test.pb8", "PB8")!;
        var entry = bank.Add(bytes, info); var before = bank.GetData(entry.Id); mon.Nickname = "AFTER"; mon.RefreshChecksum();
        var changed = new byte[mon.SIZE_PARTY]; mon.WriteDecryptedDataParty(changed); bank.Replace(entry.Id, changed, _engine.TryDescribeEntity(changed, "test.pb8", "PB8")!);
        var saved = Assert.Single(bank.GetAll()); Assert.Equal(entry.Id, saved.Id); Assert.Equal("PB8", saved.Info.Format); Assert.Equal(changed, bank.GetData(saved.Id));
        Assert.Contains(Directory.GetFiles(PathOf("transactions"), "*.before", SearchOption.AllDirectories), p => File.ReadAllBytes(p).AsSpan().SequenceEqual(before));
        Assert.Empty(journal.Pending());
    }
    [Fact]
    public async Task Scanner_resolves_aliases_ignores_states_and_retains_identity()
    {
        var saves = PathOf("saves"); Directory.CreateDirectory(saves); File.WriteAllBytes(Path.Combine(saves, "game.srm"), SaveIntegrationTests.Fixture());
        File.WriteAllBytes(Path.Combine(saves, "game.state"), SaveIntegrationTests.Fixture());
        var identities = new JsonSaveIdentityStore(PathOf("identities.json")); var id = LocalPaths.Canonical(Path.Combine(saves, "game.srm"));
        identities.Set(new(id, DisplayName: "My cartridge", GameChoiceId: "ruby"));
        var roots = new LocalWatchedRoots(PathOf("roots.json")); roots.AddRoot(new(EmulatorKind.RetroArch, saves, "Saves"));
        var catalogue = new SaveCatalogue(roots, new LocalEmulatorScanner(_engine), identities); await catalogue.ScanAsync();
        var detected = Assert.Single(catalogue.Saves); Assert.Equal("My cartridge", detected.GameLabel); Assert.Equal(SaveFormat.Standard, detected.Format);
        Assert.Equal("Pokémon Ruby", detected.EngineHint);
    }
    [Theory] [InlineData("pokemon.pb8", true, false)] [InlineData("song.mp3", false, true)] [InlineData("pokemon.pa9", true, false)]
    [InlineData("pokemon.pk3rr", true, false)] [InlineData("pokemon.pk3gsc", true, false)] [InlineData("pokemon.pk3ub", true, false)] [InlineData("pokemon.rk4", true, false)] [InlineData("pokemon.sk2", true, false)]
    public void Typed_file_dialogs_do_not_mix_Pokemon_and_music(string file, bool pokemon, bool music)
    { Assert.Equal(pokemon, FileKinds.Accepts(FilePurpose.Pokemon, file)); Assert.Equal(music, FileKinds.Accepts(FilePurpose.Music, file)); }
    [Fact]
    public async Task Bank_box_decorations_and_entry_positions_commit_and_restore_together()
    {
        var directory = PathOf("bank"); var raw = new FileBankService(directory); var journal = Journal();
        var bank = new JournaledBankService(raw, directory, PathOf("staging"), journal, new(_ => null));
        using var save = _engine.OpenSession(SaveIntegrationTests.Fixture()); var export = save.ExportSlot(0, 0);
        var entry = bank.Add(export.Data, _engine.TryDescribeEntity(export.Data, "fixture", export.Format)!); bank.AddBox();
        new BankBoxNames(directory).SetMany([(0, "FIRST")]); new BankBoxWallpapers(directory).Set(0, BankWallpaper.Art("box_wp01rs"));
        await bank.ExecuteAsync(SaveAction.Move, "Swap Bank boxes", stage =>
        { var map = BankBoxRemap.Swap(stage.Bank.BoxCount, 0, 1); stage.Bank.RemapBoxes(map); stage.Names.Remap(map); stage.Wallpapers.Remap(map); return true; });
        Assert.Equal(1, Assert.Single(bank.GetAll()).Box); Assert.Equal("FIRST", new BankBoxNames(directory).Get(1));
        Assert.Equal("art:box_wp01rs", new BankBoxWallpapers(directory).Get(1)!.Id);
        var operation = journal.History().Single(r => r.Description == "Swap Bank boxes"); await journal.RecoverAsync(operation.Id, true); raw.Reload();
        Assert.Equal(entry.Id, Assert.Single(bank.GetAll()).Id); Assert.Equal(0, Assert.Single(bank.GetAll()).Box); Assert.Equal("FIRST", new BankBoxNames(directory).Get(0));
    }
    [Fact]
    public void Bank_edit_refuses_an_entry_changed_after_it_was_read()
    {
        var directory = PathOf("bank"); var raw = new FileBankService(directory); var journal = Journal();
        var bank = new JournaledBankService(raw, directory, PathOf("staging"), journal, new(_ => null));
        using var save = _engine.OpenSession(SaveIntegrationTests.Fixture()); var export = save.ExportSlot(0, 0); var info = _engine.TryDescribeEntity(export.Data, "fixture", export.Format)!;
        var entry = bank.Add(export.Data, info); var read = bank.GetData(entry.Id);
        var path = Directory.GetFiles(directory, "*.bin").Single(); var external = read.ToArray(); external[0] ^= 1; File.WriteAllBytes(path, external);
        Assert.Throws<IOException>(() => bank.Replace(entry.Id, read, info)); Assert.Equal(external, File.ReadAllBytes(path)); Assert.Empty(journal.Pending());
    }
    [Fact]
    public async Task Bulk_adapter_records_only_the_changed_slots()
    {
        var path = PathOf("save.sav"); File.WriteAllBytes(path, SaveIntegrationTests.Fixture());
        var sessions = new SaveSessionService(_access, _engine); await sessions.OpenAsync(new(path, "fixture.sav"));
        WriteScope? recorded = null; var policy = new MutationPolicy(_ => null) { SlotGuard = (_, _, scope, _) => recorded = scope };
        var operations = new SaveOperations(sessions, _writer, policy, _engine);
        Assert.True(await operations.ExecuteChangedAsync(s => { s.ReleaseSlot(0, 1); return new(true, "Bulk action"); }, SaveAction.Release), operations.Status);
        Assert.NotNull(recorded); Assert.Equal(new SlotRef(0, 1), Assert.Single(recorded!.Slots)); Assert.False(recorded.Unrestricted);
    }
    [Fact]
    public async Task Bank_commit_completes_with_a_non_pumping_UI_context()
    {
        var raw = new FileBankService(PathOf("bank")); var bank = new JournaledBankService(raw, PathOf("bank"), PathOf("staging"), Journal(), new(_ => null));
        using var save = _engine.OpenSession(SaveIntegrationTests.Fixture()); var export = save.ExportSlot(0, 0); var info = _engine.TryDescribeEntity(export.Data, "fixture", export.Format)!;
        await Task.Run(() =>
        {
            var previous = SynchronizationContext.Current; SynchronizationContext.SetSynchronizationContext(new NonPumpingContext());
            try { bank.Add(export.Data, info); }
            finally { SynchronizationContext.SetSynchronizationContext(previous); }
        }).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(bank.GetAll());
    }
    private sealed class NonPumpingContext : SynchronizationContext
    { public override void Post(SendOrPostCallback callback, object? state) => throw new InvalidOperationException("UI continuation posted while the UI was blocked."); }
    [Fact]
    public async Task A_catalogue_read_cannot_authorize_overwriting_a_changed_live_save()
    {
        var path = PathOf("save.sav"); File.WriteAllBytes(path, SaveIntegrationTests.Fixture());
        var sessions = new SaveSessionService(_access, _engine); await sessions.OpenAsync(new(path, "fixture.sav"));
        using var changed = _engine.OpenSession(SaveIntegrationTests.Fixture()); changed.ApplyEdit(0, 1, new(Nickname: "EXTERNAL"));
        var external = changed.Serialize().ToArray(); await File.WriteAllBytesAsync(path, external);
        await _access.ReadAsync(path); // A scanner/preview updates its own most-recent read.
        var operations = new SaveOperations(sessions, _writer, new(_ => null), _engine);
        Assert.False(await operations.ExecuteAsync(s => { s.ApplyEdit(0, 0, new(Nickname: "EDIT")); return new(true, "Edit"); }, WriteScope.Only(new SlotRef(0, 0)), SaveAction.EditMon));
        Assert.Equal(external, File.ReadAllBytes(path)); Assert.Contains("changed", operations.Status, StringComparison.OrdinalIgnoreCase);
    }
    [Theory] [InlineData("A", true)] [InlineData("é", true)] [InlineData("🙂", false)] [InlineData("AAAAAAAAAAA", false)]
    public void Game_text_rules_use_the_actual_Gen3_encoder_and_storage_limit(string text, bool accepted)
    {
        using var save = _engine.OpenSession(SaveIntegrationTests.Fixture());
        var rule = GameTextRules.ForPokemon(save, 0, 0, PokemonNameField.Nickname)!;
        Assert.Equal(10, rule.MaximumLength); Assert.Equal(accepted, rule.Accepts(text));
        Assert.Equal("MON", save.ReadEntity(0, 0).Nickname);
    }
    public void Dispose()
    {
        // The directory was created by this fixture under the dedicated test root.
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
