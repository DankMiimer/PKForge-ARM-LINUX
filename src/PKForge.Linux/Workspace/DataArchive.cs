using PKForge.Application;
using PKForge.Domain;
using PKForge.Infrastructure;
using PKForge.Park;

namespace PKForge.Linux.Workspace;

public sealed partial class WorkspaceHost
{
    private async Task DataArchiveAsync()
    {
        var transfer = Get<LinuxDataTransfer>();
        var choice = await Dialogs.MenuAsync("PKForge data archive", "The Bank, snapshots, Item Bank, album, presets and history. Game saves stay in their own folders.", "Export all PKForge data", "Import a PKForge archive");
        if (choice == "Export all PKForge data")
        {
            var bytes = await Task.Run(() => transfer.Export(Arguments.UpstreamVersion));
            await ExportAsync(bytes, "PKForge-data-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".zip"); return;
        }
        if (choice != "Import a PKForge archive") return;
        var file = (await Files.PickAsync(new(FilePurpose.Archive, "Import PKForge data"))).FirstOrDefault();
        if (file is null) return;
        DataImportPreview preview;
        using (var input = File.OpenRead(file.DocumentId)) preview = await Task.Run(() => transfer.Preview(input));
        if (!await Dialogs.ConfirmAsync("Import PKForge data", $"Add {preview.BankNew} Bank Pokémon ({preview.BankDuplicates} duplicates skipped), {preview.ItemBankNew} Item Bank entries and {preview.RestorePointsNew} restore points. Existing data is merged; portable preferences are replaced. A safety archive is kept first. Device paths stay local.", "Import")) return;
        using var archive = File.OpenRead(file.DocumentId);
        var imported = await transfer.ImportAsync(archive, Arguments.UpstreamVersion);
        PKForge.App.Services.BankBoxDecor.Names.Reload(); PKForge.App.Services.BankBoxDecor.Wallpapers.Reload();
        Get<SaveCatalogue>().Refresh();
        await Dialogs.MessageAsync("Import complete", $"{imported.Result.BankImported} Bank Pokémon imported. Restart PKForge to reload all imported preferences. Safety archive: {imported.SafetyArchive}");
    }
}
