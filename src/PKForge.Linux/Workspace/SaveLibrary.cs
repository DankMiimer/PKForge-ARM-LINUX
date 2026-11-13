using PKForge.App.ViewModels;
using PKForge.App.Views;
using PKForge.Application;
using PKForge.Domain;

namespace PKForge.Linux.Workspace;

public sealed class LinuxSaveLibraryShelf(ISaveCatalogue catalogue, ISaveIdentityStore identities) : ISaveLibraryShelf
{
    public IReadOnlyList<DetectedSave> Saves => catalogue.Saves;
    public IReadOnlyList<DetectedSave> HiddenSaves => catalogue.HiddenSaves;
    public ISaveIdentityStore Identities => identities;
    public Task RescanAsync() => catalogue.ScanAsync();
}

public sealed partial class WorkspaceHost
{
    private async Task SaveLibraryAsync()
    {
        var library = Get<SaveLibraryViewModel>();
        await library.LoadAsync();
        while (!PKForge.App.App.IsSuspended)
        {
            var rows = library.Rows.ToArray();
            var options = new[] { "Filter", "Hall of Fame museum" }.Concat(rows.Select(r => r.Title + " · " + r.Who + " · " + r.Tag)).ToArray();
            var chosen = await Dialogs.PickAsync("Saves", options);
            if (chosen is null) return;
            if (chosen == 0)
            {
                if (await Dialogs.PickAsync("Show saves", library.Chips.Select(c => $"{c.Label} ({c.Count})").ToArray()) is { } filter)
                    library.SelectChip(filter);
                continue;
            }
            if (chosen == 1)
            {
                if (await MuseumScreen.ShowAsync(Screens.ControlRoot, library, Get<PKForge.App.Services.ISpriteService>()) is { } hall)
                { await OpenAsync(new(hall.Entry.Save.DocumentId, hall.Entry.Save.FileName)); return; }
                continue;
            }
            var row = rows[chosen.Value - 2];
            var action = await Dialogs.MenuAsync(row.Title, row.Where, "Open", "Details", "Take a snapshot", "Restore a snapshot", "Compare", "Game journal", "Convert for another emulator", "Copy to a folder", row.Entry.OnShelf ? "Hide from Home" : "Show on Home");
            switch (action)
            {
                case "Open": await OpenAsync(new(row.Entry.Save.DocumentId, row.Entry.Save.FileName)); return;
                case "Details": await InspectAsync(row.Title, await library.InsightAsync(row)); break;
                case "Take a snapshot":
                    if (await Dialogs.TextAsync("Snapshot name", DateTime.Now.ToString("yyyy-MM-dd HH-mm")) is { } name)
                        await library.SnapshotAsync(row, name);
                    break;
                case "Restore a snapshot":
                    var points = await library.SnapshotsAsync(row);
                    var point = await Dialogs.PickAsync("Restore snapshot", points.Select(p => SaveLibrary.SnapshotName(p.ChangeDescription) + " · " + p.CreatedUtc.ToLocalTime()).ToArray());
                    if (point is { } at && await Dialogs.ConfirmAsync("Restore snapshot", "Replace this save with the snapshot? A safety copy of its current state is kept first.", "Restore"))
                        await library.RestoreSnapshotAsync(row, points[at]);
                    break;
                case "Compare":
                    var others = library.SameGame(row);
                    if (await Dialogs.PickAsync("Compare with", others.Select(r => r.Who + " · " + r.Where).ToArray()) is { } other)
                        await InspectAsync("Save comparison", await library.CompareAsync(row, others[other]));
                    break;
                case "Game journal": await InspectAsync("Game journal", await library.JournalAsync(row)); break;
                case "Convert for another emulator":
                    var conversions = await library.ConversionsAsync(row);
                    var conversion = await Dialogs.PickAsync("Conversion", conversions.Select(c => c.Target.ToString() + " · " + NativeForms.Describe(c.Result)).ToArray());
                    if (conversion is { } ci && await Dialogs.ConfirmAsync("Write converted copy", NativeForms.Describe(conversions[ci].Result), "Write new file"))
                        await library.WriteConversionAsync(row, conversions[ci].Result);
                    break;
                case "Copy to a folder": await library.ExportAsync(row); break;
                case "Hide from Home": await library.SetOnShelfAsync(row, false); break;
                case "Show on Home": await library.SetOnShelfAsync(row, true); break;
            }
            Status = library.Status; Screens.Invalidate();
        }
    }
}
