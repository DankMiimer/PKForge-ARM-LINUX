using PKForge.App.Services;
using PKForge.Application;
using PKForge.Domain;
using PKForge.Infrastructure;

namespace PKForge.Linux.Workspace;

public sealed partial class WorkspaceHost
{
    private async Task PksmAsync(IReadOnlyList<BankEntry> entries)
    {
        var choice = await Dialogs.MenuAsync("PKSM bank transfer", PksmBankTransfer.SdCardHelp, "Import bank / dumps / Pokémon", "Export selected or whole Bank");
        if (choice == "Import bank / dumps / Pokémon")
        {
            var files = await Files.PickAsync(new(FilePurpose.Pksm, "Select PKSM files", true)); if (files.Count == 0) return;
            List<(string, byte[])> input = []; foreach (var file in files) input.Add((file.DisplayName, await File.ReadAllBytesAsync(file.DocumentId)));
            var result = await Task.Run(() => PksmWorkflow.Plan(input));
            var plans = result.Plans.Where(p => !HardcoreMode.IsOn || p.IsBank).ToArray();
            await InspectAsync("PKSM preview", plans);
            if (plans.Sum(p => p.ReadyCount) == 0) { await InspectAsync("PKSM notes", result.Notes); return; }
            var mode = await Dialogs.MenuAsync("Import PKSM", $"{plans.Sum(p => p.ReadyCount)} ready. Exact duplicates are skipped.", "New boxes with names", "Merge into free slots"); if (mode is null) return;
            var action = plans.All(p => p.IsBank) ? SaveAction.Move : SaveAction.Duplicate;
            Get<MutationPolicy>().Demand(action);
            using var staged = await Task.Run(() => new BankStaging(Path.Combine(ParkPlatform.DataDirectory, "bank"), Path.Combine(ParkPlatform.DataDirectory, "cache", "staging")));
            Get<JournaledBankService>().ValidateSnapshot(staged.Bank);
            var imported = await Task.Run(() => PksmBankTransfer.Apply(staged.Bank, staged.Names, plans, mode == "New boxes with names" ? PksmImportMode.NewBoxes : PksmImportMode.Merge));
            Get<JournaledBankService>().Guard?.Invoke(staged.Bank.GetAll().Where(e => !Bank.GetAll().Any(old => old.Id == e.Id)).ToArray(), action);
            var changes = staged.Changes(); if (changes.Count == 0) { await InspectAsync("PKSM result", imported); return; }
            var journal = Get<RecoveryJournal>(); var record = await journal.PrepareAsync("PKSM Bank import", changes); await journal.CommitAsync(record.Id);
            await RefreshStoresAsync(); await InspectAsync("PKSM result", imported);
        }
        else if (choice == "Export selected or whole Bank")
        {
            var folder = await Files.PickFolderAsync("Export PKSM bank"); if (folder is null) return;
            var name = await Dialogs.TextAsync("PKSM bank name", "pkforge"); if (name is null) return;
            var layout = await Dialogs.PickAsync("Layout", Enum.GetNames<PksmExportLayout>()); if (layout is null) return;
            var result = await Task.Run(() => PksmBankTransfer.Export(Bank, entries.Count == 0 ? Bank.GetAll() : entries, PksmWorkflow.Encode, BankBoxDecor.Names.Get, (PksmExportLayout)layout.Value));
            if (result.Written > 0)
            {
                name = PksmBankTransfer.SanitizeBankName(name);
                var paths = new[] { Path.Combine(folder.TreeId, name + ".bnk"), Path.Combine(folder.TreeId, name + ".json") };
                if (paths.Any(File.Exists) && !await Dialogs.ConfirmAsync("Replace PKSM export", string.Join('\n', paths))) return;
                await Get<IFolderFileAccess>().WriteFileAsync(folder.TreeId, name + ".bnk", result.Bank);
                await Get<IFolderFileAccess>().WriteFileAsync(folder.TreeId, name + ".json", result.BoxNames);
            }
            await InspectAsync("PKSM export result", new { result.Written, result.Boxes, result.Skipped });
        }
    }
}
