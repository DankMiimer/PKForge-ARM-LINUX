using System.Buffers.Binary;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
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
        ParkPlatform.Configure(_directory, Path.Combine(AppContext.BaseDirectory, "assets"));
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
        collection.AddSingleton<ProtectionStore>();
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
    public async Task Opening_a_menu_puts_the_Pokemon_in_hand_back_first()
    {
        // Reported on the RGDS: A (grab), then + and a transfer from the menu, left the
        // transferred Pokémon drawn in the hand until the next grab.
        var screen = Open(bank: false);
        Press(PadButton.A);
        Assert.True(screen.Carrying);
        // Every menu button (+, X, Y, −) goes through RunMenu; a stand-in keeps real overlays out of the test.
        var ran = false;
        screen.RunMenu(() => { ran = true; Assert.False(screen.Carrying); return Task.CompletedTask; });
        Assert.True(ran);
        Assert.False(screen.Carrying);
        // Nothing was moved or written by putting it back.
        await screen.PendingWrite;
        Assert.Equal(25, SpeciesOnDisk(0, 0));
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

    [Fact]
    public async Task Bank_reorder_keeps_names_wallpapers_marks_and_living_dex_anchor_with_their_boxes()
    {
        var screen = Open(bank: true); var bank = _services.GetRequiredService<IBankService>();
        BankBoxDecor.Names.SetMany([(0, "First"), (1, "Second")]);
        BankBoxDecor.Wallpapers.Set(0, BankWallpaper.Color(3));
        BankBoxDecor.LivingDexStartBox = 0; screen.Marks.Add(new(0, 4));
        await _workspace.RemapBankAsync(BankBoxRemap.Swap(bank.BoxCount, 0, 1));
        Assert.Equal("First", BankBoxDecor.Names.Get(1)); Assert.Equal("Second", BankBoxDecor.Names.Get(0));
        Assert.Equal(BankWallpaper.Color(3).Id, BankBoxDecor.Wallpapers.Get(1)?.Id);
        Assert.Equal(1, BankBoxDecor.LivingDexStartBox); Assert.Equal(1, screen.Box);
        Assert.Equal(new SlotRef(1, 4), Assert.Single(screen.Marks));
        Assert.Empty(_services.GetRequiredService<RecoveryJournal>().Pending());
    }

    [Fact]
    public async Task Locked_bank_box_refuses_reorder_before_metadata_or_selection_changes()
    {
        var screen = Open(bank: true); var bank = _services.GetRequiredService<IBankService>();
        BankBoxDecor.Names.SetMany([(0, "Protected")]); BankBoxDecor.LivingDexStartBox = 0;
        _services.GetRequiredService<ProtectionStore>().ToggleBox("bank", 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => _workspace.RemapBankAsync(BankBoxRemap.Swap(bank.BoxCount, 0, 1)));
        Assert.Equal("Protected", BankBoxDecor.Names.Get(0)); Assert.Equal(0, BankBoxDecor.LivingDexStartBox); Assert.Equal(0, screen.Box);
    }

    [Fact]
    public async Task Upstream_box_lock_follows_a_whole_box_move_with_its_group()
    {
        var bank = _services.GetRequiredService<IBankService>();
        bank.SetBoxLocked(0, true); bank.SetBoxGroups([new("Keep", [0])]);
        await _workspace.RemapBankAsync(BankBoxRemap.Move(bank.BoxCount, 0, 1));
        Assert.True(bank.LockedBoxes.SetEquals([1])); Assert.Equal(new[] { 1 }, bank.BoxGroups.Single().Boxes);
        Assert.Empty(_services.GetRequiredService<RecoveryJournal>().Pending());
    }

    [Fact]
    public async Task Shared_page_navigation_hides_and_restores_pages_and_notifies_removal()
    {
        var first = new TrackedPage(); var second = new TrackedPage();
        await _workspace.PushAsync(first); await _workspace.PushAsync(second);
        Assert.False(first.Content!.IsVisible); Assert.Equal(1, first.Disappeared);
        Assert.Same(second, await _workspace.PopAsync()); Assert.True(first.Content.IsVisible);
        Assert.Equal(2, first.Appeared); Assert.Equal(NavigationType.Pop, second.Removal);
        _workspace.OpenHome(); Assert.Equal(NavigationType.Remove, first.Removal);
        Assert.DoesNotContain(first.Content, _workspace.Screens.ControlRoot.Children);
    }
    private sealed class TrackedPage : ContentPage
    {
        public int Appeared, Disappeared;
        public NavigationType? Removal;
        public TrackedPage() => Content = new Grid();
        protected override void OnAppearing() => Appeared++;
        protected override void OnDisappearing() => Disappeared++;
        protected override void OnNavigatedFrom(NavigatedFromEventArgs args) => Removal = args.NavigationType;
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
