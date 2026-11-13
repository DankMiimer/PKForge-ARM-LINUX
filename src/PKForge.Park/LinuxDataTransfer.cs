using PKForge.Application;
using PKForge.Domain;
using PKForge.Infrastructure;

namespace PKForge.Park;

/// <summary>Linux host for upstream archives. Bank changes retain the recovery journal.</summary>
public sealed class LinuxDataTransfer(JournaledBankService bank, ISaveIdentityStore identities,
    InjectedGiftHistory gifts, ItemBankStore items)
{
    private static JsonSettingsStore Settings => ParkPlatform.Settings as JsonSettingsStore
        ?? throw new InvalidOperationException("This settings provider cannot export its data.");
    public byte[] Export(string version)
    {
        using var output = new MemoryStream();
        DataArchive.Export(output, ParkPlatform.DataDirectory, Settings.ExportPreferences(), version, DateTimeOffset.UtcNow);
        return output.ToArray();
    }
    public DataImportPreview Preview(Stream input)
    {
        using var archive = DataArchive.Open(input);
        JsonSettingsStore.ValidatePreferences(archive.Preferences);
        return DataArchive.Preview(archive, ParkPlatform.DataDirectory, bank, []);
    }
    public async Task<(DataImportResult Result, string SafetyArchive)> ImportAsync(Stream input, string version)
    {
        using var archive = DataArchive.Open(input);
        JsonSettingsStore.ValidatePreferences(archive.Preferences);
        // Validate before creating the safety copy or changing any data.
        _ = DataArchive.Preview(archive, ParkPlatform.DataDirectory, bank, []);
        var safetyRoot = Path.Combine(ParkPlatform.DataDirectory, "data-safety");
        Directory.CreateDirectory(safetyRoot);
        var safetyPath = Path.Combine(safetyRoot, $"before-import-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.zip");
        using (var safety = new FileStream(safetyPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
        {
            DataArchive.Export(safety, ParkPlatform.DataDirectory, Settings.ExportPreferences(), version, DateTimeOffset.UtcNow);
            safety.Flush(true);
        }
        DurableFiles.SyncDirectory(safetyRoot);
        try
        {
            var result = await bank.ExecuteAsync(SaveAction.Duplicate, "Import PKForge data archive", stage =>
                DataArchive.Restore(archive, ParkPlatform.DataDirectory, stage.Bank, identities, gifts, stage.Names, stage.Wallpapers, items));
            Settings.ImportPreferences(archive.Preferences);
            return (result, safetyPath);
        }
        catch (Exception error)
        { throw new IOException($"The import did not finish. Its safety archive is kept at {safetyPath}. Recover any unfinished Bank operation before retrying.", error); }
    }
}
