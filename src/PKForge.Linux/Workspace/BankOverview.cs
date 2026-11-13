using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Application;
using PKForge.Domain;

namespace PKForge.Linux.Workspace;

public sealed partial class WorkspaceHost
{
    private async Task BankOverviewAsync(StorageScreen screen)
    {
        var chosen = await BankBoxOverview.ShowAsync(new BankOverviewActions(this), Screens.ControlRoot, Bank,
            Get<ISpriteService>(), screen.Box, screen.BankGroup);
        if (chosen is { } result) { screen.BankGroup = result.Group; screen.SelectBox(result.Box); }
    }

    private sealed class BankOverviewActions(WorkspaceHost host) : IBankBoxOverviewActions
    {
        public int? LivingDexStartBox => BankBoxDecor.LivingDexStartBox;
        public async Task<bool> ApplyBoxRemapAsync(BankBoxRemap remap) { await host.RemapBankAsync(remap); return true; }
        public async Task<bool> ApplyBoxRemapAsync(BankBoxRemap remap, Func<IReadOnlyList<BankBoxGroup>, IReadOnlyList<BankBoxGroup>>? changeGroups)
        { await host.RemapBankAsync(remap, changeGroups); return true; }
        public Task<bool> ToggleBoxLockAsync(int box) => host.ToggleBankBoxLockAsync(box);
        public async Task<bool> ChangeGroupsAsync(Func<IReadOnlyList<BankBoxGroup>, IReadOnlyList<BankBoxGroup>> change, string? done)
        {
            await host.Get<JournaledBankService>().ExecuteAsync(SaveAction.Move, done ?? "Change Bank groups", stage =>
            { stage.Bank.SetBoxGroups(change(stage.Bank.BoxGroups)); return true; });
            host.Screens.Invalidate(); return true;
        }
        public Task<bool> RenameBoxAsync(int box) => host.RenameBankBoxAsync(box);
        public Task<bool> PickWallpaperAsync(int box) => host.PickBankWallpaperAsync(box);
        public Task<bool> DeleteBoxAsync(int box) => host.DeleteBankBoxAsync(box);
    }

    private void DemandUnlockedBankBox(int box)
    {
        if (Get<ProtectionStore>().IsBoxLocked("bank", box)) throw new InvalidOperationException("Unlock this box before modifying it.");
    }

    private async Task<bool> ToggleBankBoxLockAsync(int box)
    {
        var protection = Get<ProtectionStore>();
        var legacyLocked = protection.IsBoxLocked("bank", box);
        var locked = legacyLocked || Bank.LockedBoxes.Contains(box);
        await Get<JournaledBankService>().ExecuteAsync(SaveAction.Move, "Change Bank box lock", stage =>
        { stage.Bank.SetBoxLocked(box, !locked); return true; });
        // An interrupted legacy migration remains locked; retrying can safely
        // clear it after the Bank index has committed the requested state.
        if (legacyLocked) protection.ToggleBox("bank", box);
        Screens.Invalidate(); return true;
    }

    private async Task<bool> RenameBankBoxAsync(int box)
    {
        DemandUnlockedBankBox(box);
        var name = await Dialogs.TextAsync("Box name", BankBoxDecor.Names.Get(box) ?? "");
        if (name is null) return false;
        await Get<JournaledBankService>().ExecuteAsync(SaveAction.Move, "Rename Bank box", stage => { stage.Names.SetMany([(box, name)]); return true; });
        BankBoxDecor.Names.Reload(); Screens.Invalidate(); return true;
    }

    private async Task<bool> PickBankWallpaperAsync(int box)
    {
        DemandUnlockedBankBox(box);
        var wallpaper = await BankWallpaperPicker.ShowAsync(Screens.ControlRoot, "Wallpaper · " + BankBoxDecor.Label(box), BankBoxArt.Of(box));
        if (wallpaper is null) return false;
        await Get<JournaledBankService>().ExecuteAsync(SaveAction.Move, "Bank wallpaper", stage => { stage.Wallpapers.Set(box, wallpaper); return true; });
        BankBoxDecor.Wallpapers.Reload(); Screens.Invalidate(); return true;
    }

    private async Task<bool> DeleteBankBoxAsync(int box)
    {
        if (Bank.LockedBoxes.Contains(box) || Get<ProtectionStore>().LockedBoxes("bank").Any(b => b >= box))
            throw new InvalidOperationException("Unlock boxes affected by deletion before reordering them.");
        if (!await Dialogs.ConfirmAsync("Delete Bank box", "Every Pokémon will be rescued into unlocked space, adding a box when needed. Names and wallpapers move with their boxes.")) return false;
        BankBoxRemap? applied = null;
        await Get<JournaledBankService>().ExecuteAsync(SaveAction.Move, "Rescue and delete Bank box", stage =>
        {
            var rescued = stage.Bank.GetAll().Where(e => e.Box == box).ToArray();
            var used = stage.Bank.GetAll().Select(e => (e.Box, e.Slot)).ToHashSet();
            var free = Enumerable.Range(0, stage.Bank.BoxCount).Where(b => b != box && !IsBoxLocked("bank", b))
                .SelectMany(b => Enumerable.Range(0, 30).Where(slot => !used.Contains((b, slot))).Select(slot => new SlotRef(b, slot))).ToList();
            while (free.Count < rescued.Length || stage.Bank.BoxCount == 1)
            {
                var added = stage.Bank.BoxCount; stage.Bank.AddBox(); free.AddRange(Enumerable.Range(0, 30).Select(slot => new SlotRef(added, slot)));
            }
            stage.Bank.Place(rescued.Select((entry, i) => (entry.Id, free[i].Box, free[i].Slot)).ToArray());
            applied = BankBoxRemap.Remove(stage.Bank.BoxCount, box);
            stage.Bank.RemapBoxes(applied); stage.Names.Remap(applied); stage.Wallpapers.Remap(applied); return true;
        });
        BankBoxDecor.Names.Reload(); BankBoxDecor.Wallpapers.Reload();
        RemapBankPresentation(applied!); return true;
    }

    private void RemapBankPresentation(BankBoxRemap remap)
    {
        if (BankBoxDecor.LivingDexStartBox is { } start) BankBoxDecor.LivingDexStartBox = remap.Map(start);
        if (Screens.Current is StorageScreen { IsBank: true } screen) screen.RemapBoxes(remap);
        Screens.Invalidate();
    }
}
