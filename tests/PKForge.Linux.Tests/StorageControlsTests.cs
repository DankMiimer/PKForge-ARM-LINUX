using System.Buffers.Binary;
using Microsoft.Extensions.DependencyInjection;
using PKForge.App.Services;
using PKForge.Application;
using PKForge.Domain;
using PKForge.Engine;
using PKForge.Infrastructure;
using PKForge.Linux.Workspace;
using PKForge.Park;
using PKHeX.Core;
using Xunit;

namespace PKForge.Linux.Tests;

/// <summary>The storage screen's original controls, on a real workspace over a temporary save and Bank.</summary>
public sealed class StorageControlsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "pkforge-linux-tests", Guid.NewGuid().ToString("N"));
    private readonly string _save;
    private readonly ServiceProvider _services;
    private readonly WorkspaceHost _workspace;

    public StorageControlsTests()
    {
        Directory.CreateDirectory(_directory);
        ParkPlatform.Configure(_directory, _directory);
        _save = Path.Combine(_directory, "test.sav");
        File.WriteAllBytes(_save, Fixture());
        var collection = new ServiceCollection();
        collection.AddSingleton<ISaveEngine, SaveEngine>();
        collection.AddSingleton<ISaveFileAccess, LinuxSaveFileAccess>();
        collection.AddSingleton<ISaveIdentityStore>(_ => new JsonSaveIdentityStore(Path.Combine(_directory, "save-identities.json")));
        collection.AddSingleton<ISaveSessionService>(sp => new SaveSessionService(sp.GetRequiredService<ISaveFileAccess>(), sp.GetRequiredService<ISaveEngine>()));
        collection.AddSingleton<IBackupService>(_ => new FileBackupService(Path.Combine(_directory, "backups")));
        collection.AddSingleton<ISafeSaveWriter, SafeSaveWriter>();
        collection.AddSingleton(_ => new MutationPolicy(_ => null));
        collection.AddSingleton<SaveOperations>();
        collection.AddSingleton<FileBankService>(_ => new FileBankService(Path.Combine(_directory, "bank")));
        collection.AddSingleton(sp => new RecoveryJournal(Path.Combine(_directory, "transactions"), sp.GetRequiredService<ISaveEngine>(), sp.GetRequiredService<ISaveFileAccess>(), sp.GetRequiredService<ISafeSaveWriter>()));
        collection.AddSingleton(sp => new JournaledBankService(sp.GetRequiredService<FileBankService>(), Path.Combine(_directory, "bank"), Path.Combine(_directory, "staging"),
            sp.GetRequiredService<RecoveryJournal>(), sp.GetRequiredService<MutationPolicy>()));
        collection.AddSingleton<IBankService>(sp => sp.GetRequiredService<JournaledBankService>());
        collection.AddSingleton<IGameDataService, GameDataService>();
        collection.AddSingleton<ISaveCatalogue>(new EmptyCatalogue());
        collection.AddSingleton<GamepadRouter>();
        collection.AddSingleton<SecondScreenState>();
        _services = collection.BuildServiceProvider();
        _workspace = new WorkspaceHost(_services, dual: false);
        _services.GetRequiredService<ISaveSessionService>().OpenAsync(new(_save, "test.sav")).AsTask().GetAwaiter().GetResult();
        _workspace.OpenHome();
    }

    public void Dispose()
    {
        _services.Dispose();
        try { Directory.Delete(_directory, recursive: true); } catch (IOException) { }
    }

    private StorageScreen Open(bool bank)
    {
        _workspace.OpenStorage(bank);
        return Assert.IsType<StorageScreen>(_workspace.Screens.Current);
    }

    private void Press(params PadButton[] buttons)
    {
        foreach (var button in buttons) _workspace.Screens.OnPadButton(button);
    }

    private ushort SpeciesOnDisk(int box, int slot) =>
        SaveUtil.GetSaveFile(File.ReadAllBytes(_save))!.GetBoxSlotAtIndex(box, slot).Species;

    [Fact]
    public async Task A_picks_up_and_a_second_A_swaps_on_disk_with_a_restore_point()
    {
        var screen = Open(bank: false);
        Press(PadButton.A, PadButton.Right, PadButton.A);
        await screen.PendingWrite;
        Assert.Equal(150, SpeciesOnDisk(0, 0));
        Assert.Equal(25, SpeciesOnDisk(0, 1));
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(_directory, "backups"), "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task A_carries_across_a_box_turn_onto_the_first_slot()
    {
        var screen = Open(bank: false);
        Press(PadButton.A, PadButton.R, PadButton.A);
        await screen.PendingWrite;
        Assert.Equal(0, SpeciesOnDisk(0, 0));
        Assert.Equal(25, SpeciesOnDisk(1, 0));
    }

    [Fact]
    public async Task B_cancels_the_carry_without_writing_then_goes_back()
    {
        var original = await File.ReadAllBytesAsync(_save);
        var screen = Open(bank: false);
        Press(PadButton.A, PadButton.Right, PadButton.B);
        await screen.PendingWrite;
        Assert.Equal(original, await File.ReadAllBytesAsync(_save));
        Assert.Same(screen, _workspace.Screens.Current);
        Press(PadButton.B);
        Assert.NotSame(screen, _workspace.Screens.Current);
    }

    [Fact]
    public void Minus_is_the_green_hand_where_held_A_marks_the_swept_area()
    {
        var screen = Open(bank: false);
        Press(PadButton.Select, PadButton.A, PadButton.Right);
        _workspace.Screens.OnPadReleased(PadButton.A);
        Assert.Equal([new SlotRef(0, 0), new SlotRef(0, 1)], screen.Marks.OrderBy(m => m.Box).ThenBy(m => m.Slot));
        Press(PadButton.B);
        Assert.Empty(screen.Marks);
        Press(PadButton.Y);
        Assert.Equal(2, screen.Marks.Count); // Y marks the whole box
        Press(PadButton.Select);
        Assert.Empty(screen.Marks); // leaving multi-select drops its marks, as the original
        Assert.Same(screen, _workspace.Screens.Current);
    }

    [Fact]
    public void Bank_A_moves_an_entry_and_Y_marks_only_occupied_slots()
    {
        var bank = _services.GetRequiredService<IBankService>();
        var engine = _services.GetRequiredService<ISaveEngine>();
        var session = _services.GetRequiredService<ISaveSessionService>().CurrentSession!;
        var export = session.ExportSlot(0, 0);
        var entry = bank.Add(export.Data, engine.TryDescribeEntity(export.Data, "Pokémon Ruby", export.Format)!);
        var screen = Open(bank: true);
        Press(PadButton.Y, PadButton.Right, PadButton.Y);
        Assert.Equal([new SlotRef(entry.Box, entry.Slot)], screen.Marks);
        Press(PadButton.B); // B clears the marks before it would go back
        Assert.Empty(screen.Marks);
        Press(PadButton.Left, PadButton.A, PadButton.Right, PadButton.Right, PadButton.A);
        var moved = Assert.Single(bank.GetAll());
        Assert.Equal((entry.Box, entry.Slot + 2), (moved.Box, moved.Slot));
    }

    private sealed class EmptyCatalogue : ISaveCatalogue
    {
        public IReadOnlyList<DetectedSave> Saves => [];
        public Task ScanAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    /// <summary>A Gen 3 save with Pikachu in box 1 slot 1 and Mewtwo in slot 2, as the Park tests use.</summary>
    private static byte[] Fixture()
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
}
