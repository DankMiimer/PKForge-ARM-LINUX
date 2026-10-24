using System.Diagnostics;
using System.Reflection;
using PKForge.Application;
using PKForge.Domain;
using PKForge.Engine;
using PKForge.Infrastructure;
using PKForge.Park;
using PKForge.Park.Tests;

internal static class CrashRecoveryCheck
{
    public static async Task RunAsync(string mode, string directory, string phase)
    {
        directory = Path.GetFullPath(directory);
        if (!directory.Split(Path.DirectorySeparatorChar).Contains("pkforge-full-test")) throw new ArgumentException("Use isolated pkforge-full-test data.");
        var engine = new SaveEngine(); var access = new LinuxSaveFileAccess();
        var writer = new SafeSaveWriter(engine, new FileBackupService(Path.Combine(directory, "backups"), syncDirectory: DurableFiles.SyncDirectory), access);
        var journal = new RecoveryJournal(Path.Combine(directory, "transactions"), engine, access, writer);
        var sourcePath = Path.Combine(directory, "source.sav"); var destinationPath = Path.Combine(directory, "destination.sav");
        var bankDirectory = Path.Combine(directory, "bank");
        if (mode == "--kill-journal")
        {
            if (Directory.Exists(directory)) throw new InvalidOperationException("Choose a new crash fixture directory.");
            Directory.CreateDirectory(directory); ParkPlatform.Configure(Path.Combine(directory, "settings"), directory);
            var original = (byte[])typeof(SaveIntegrationTests).GetMethod("Fixture", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
            File.WriteAllBytes(sourcePath, original); File.WriteAllBytes(destinationPath, original);
            Directory.CreateDirectory(bankDirectory);
            using var source = engine.OpenSession(original); using var destination = engine.OpenSession(original);
            var exported = source.ExportSlot(0, 0); source.ReleaseSlot(0, 0);
            if (!destination.ImportSlot(0, 2, exported.Data, "transfer." + exported.Format!.ToLowerInvariant())) throw new InvalidDataException("Fixture import failed.");
            using var stage = new BankStaging(bankDirectory, Path.Combine(directory, "staging"));
            var entry = stage.Bank.Add(exported.Data, engine.TryDescribeEntity(exported.Data, "crash fixture", exported.Format)!);
            File.WriteAllText(Path.Combine(directory, "bank-id.txt"), entry.Id.ToString());
            var changes = new[] { new PreparedFileChange(destinationPath, original, destination.Serialize().ToArray(), 0, true, WriteScope.Only(new SlotRef(0, 2))) }
                .Concat(stage.Changes().Select(change => change with { Order = 1 }))
                .Append(new(sourcePath, original, source.Serialize().ToArray(), 2, true, WriteScope.Only(new SlotRef(0, 0)))).ToArray();
            var record = await journal.PrepareAsync("Actual process termination during two-save/Bank publication", changes);
            var parts = phase.Split(':'); var step = int.Parse(parts[1]);
            if (step >= record.Files.Count) throw new ArgumentOutOfRangeException(nameof(phase));
            void Kill(int current) { if (current == step) Process.GetCurrentProcess().Kill(); }
            if (parts[0] == "before") journal.BeforePublish = Kill; else if (parts[0] == "after") journal.AfterPublish = Kill; else throw new ArgumentException(phase);
            await journal.CommitAsync(record.Id);
            throw new InvalidOperationException("The requested kill point did not execute.");
        }
        var pending = journal.Pending().Single();
        var restore = phase == "restore";
        var originals = pending.Files.ToDictionary(file => file.Path, file => file.BeforeFile is null ? null : File.ReadAllBytes(Path.Combine(directory, "transactions", pending.Id, file.BeforeFile)));
        await journal.RecoverAsync(pending.Id, restore);
        if (journal.Pending().Count != 0) throw new InvalidDataException("Recovery is still pending.");
        var bank = new FileBankService(bankDirectory);
        using var recoveredSource = engine.OpenSession(File.ReadAllBytes(sourcePath)); using var recoveredDestination = engine.OpenSession(File.ReadAllBytes(destinationPath));
        if (restore)
        {
            foreach (var original in originals)
                if (original.Value is null ? File.Exists(original.Key) : !original.Value.AsSpan().SequenceEqual(File.ReadAllBytes(original.Key))) throw new InvalidDataException("Restoration changed an original participant.");
            if (bank.GetAll().Count != 0) throw new InvalidDataException("Restored Bank should be empty.");
        }
        else
        {
            var entry = bank.GetAll().Single();
            if (entry.Id != Guid.Parse(File.ReadAllText(Path.Combine(directory, "bank-id.txt")))) throw new InvalidDataException("Recovery changed the Bank ID.");
            if (!bank.GetData(entry.Id).AsSpan().SequenceEqual(recoveredDestination.ExportSlot(0, 2).Data) || !recoveredSource.ReadEntity(0, 0).IsEmpty) throw new InvalidDataException("Recovery lost or changed a transferred Pokémon.");
        }
        Console.WriteLine("ACTUAL SIGKILL RECOVERY PASS: " + phase + "; exact two-save/Bank states and stable Bank identity.");
    }
}
