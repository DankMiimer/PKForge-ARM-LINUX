using System.Reflection;
using PKForge.Park.Tests;

// Runs the same synthetic-save integration tests on a handheld without installing an SDK.
// Every test creates a new temporary save and isolated Bank/preferences directory.
var failed = 0; var count = 0;
if (args is ["--verify-park-animations", var animationDirectory])
    return await ParkAnimationCheck.Run(animationDirectory);
if (args is ["--seed-home-fixture", var homeDirectory])
{
    var root = Path.GetFullPath(homeDirectory);
    if (!root.Split(Path.DirectorySeparatorChar).Contains("pkforge-full-test") || Directory.Exists(root)) throw new ArgumentException("Choose a new directory below pkforge-full-test.");
    Directory.CreateDirectory(root); var saves = Path.Combine(root, "saves"); Directory.CreateDirectory(saves);
    var file = Path.Combine(saves, "fixture.srm");
    File.WriteAllBytes(file, (byte[])typeof(SaveIntegrationTests).GetMethod("Fixture", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!);
    PKForge.Park.ParkPlatform.Configure(root, root); PKForge.Park.PlatformPreferences.Default.Set("linux.root-discovery.v1", true);
    var roots = new PKForge.Application.LocalWatchedRoots(Path.Combine(root, "watched-roots.json")); roots.AddRoot(new(PKForge.Domain.EmulatorKind.RetroArch, saves, "Fixture saves"));
    var identities = new PKForge.Infrastructure.JsonSaveIdentityStore(Path.Combine(root, "save-identities.json")); identities.Set(new(file, DisplayName: "QA cartridge", GameChoiceId: "ruby"));
    using var catalogue = new PKForge.Application.SaveCatalogue(roots, new PKForge.Application.LocalEmulatorScanner(new PKForge.Engine.SaveEngine()), identities, Path.Combine(root, "cache", "save-catalogue-v1.json"));
    await catalogue.ScanAsync(); if (catalogue.Saves.Count != 1) throw new InvalidDataException("The real scanner did not find the fixture.");
    Console.WriteLine("HOME FIXTURE READY: one real scanned save, persisted catalogue and edition/name override."); return 0;
}
if (args is ["--verify-linux-links"])
{
    if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("This check exercises Linux realpath and symbolic links.");
    var root = Path.Combine(Path.GetTempPath(), "pkforge-links-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
    try
    {
        var actual = Path.Combine(root, "actual"); Directory.CreateDirectory(actual);
        var file = Path.Combine(actual, "game.srm");
        var fixture = (byte[])typeof(SaveIntegrationTests).GetMethod("Fixture", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, null)!;
        File.WriteAllBytes(file, fixture); File.WriteAllBytes(Path.Combine(actual, "game.state"), fixture);
        Directory.CreateSymbolicLink(Path.Combine(root, "alias"), actual);
        File.CreateSymbolicLink(Path.Combine(actual, "alias.srm"), file);
        Directory.CreateSymbolicLink(Path.Combine(actual, "loop"), root);
        var roots = new PKForge.Application.LocalWatchedRoots(Path.Combine(root, "roots.json"));
        roots.AddRoot(new(PKForge.Domain.EmulatorKind.RetroArch, actual, "Actual")); roots.AddRoot(new(PKForge.Domain.EmulatorKind.RetroArch, Path.Combine(root, "alias"), "Alias"));
        var engine = new PKForge.Engine.SaveEngine(); var identities = new PKForge.Infrastructure.JsonSaveIdentityStore(null); var cache = Path.Combine(root, "cache.json");
        using (var catalogue = new PKForge.Application.SaveCatalogue(roots, new PKForge.Application.LocalEmulatorScanner(engine), identities, cache))
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)); await catalogue.ScanAsync(timeout.Token);
            if (catalogue.Saves.Count != 1 || catalogue.Saves[0].DocumentId != PKForge.Application.LocalPaths.Canonical(file)) throw new InvalidDataException("Linux aliases produced duplicate or noncanonical saves.");
        }
        using var restored = new PKForge.Application.SaveCatalogue(roots, new PKForge.Application.LocalEmulatorScanner(engine), identities, cache);
        if (restored.Saves.Count != 1 || !fixture.AsSpan().SequenceEqual(File.ReadAllBytes(file))) throw new InvalidDataException("Cached discovery changed a save or lost canonical identity.");
        Console.WriteLine("LINUX DISCOVERY PASS: folder/file aliases deduplicated, symlink loop terminated, save state excluded, cached canonical identity and source bytes preserved.");
    }
    finally { Directory.Delete(root, true); }
    return 0;
}
if (args is ["--kill-journal" or "--recover-killed-journal", var crashDirectory, var phase])
{
    await CrashRecoveryCheck.RunAsync(args[0], crashDirectory, phase); return 0;
}
if (args is ["--verify-preview-upgrade", var beforeDirectory, var afterDirectory])
{
    var beforeRoot = Path.GetFullPath(beforeDirectory); var afterRoot = Path.GetFullPath(afterDirectory);
    var checkedFiles = 0; var checkedKeys = 0;
    foreach (var path in Directory.EnumerateFiles(beforeRoot, "*", SearchOption.AllDirectories))
    {
        var relative = Path.GetRelativePath(beforeRoot, path); var afterPath = Path.Combine(afterRoot, relative);
        if (!File.Exists(afterPath)) throw new InvalidDataException("Upgrade removed preview data: " + relative);
        if (relative == "settings.json")
        {
            using var before = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(path)); using var after = System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(afterPath));
            foreach (var property in before.RootElement.EnumerateObject())
            {
                if (!after.RootElement.TryGetProperty(property.Name, out var value) || !System.Text.Json.JsonElement.DeepEquals(property.Value, value)) throw new InvalidDataException("Upgrade changed an existing preview preference: " + property.Name);
                checkedKeys++;
            }
        }
        else if (!System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)).AsSpan().SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(afterPath)))) throw new InvalidDataException("Upgrade changed preview data: " + relative);
        checkedFiles++;
    }
    Console.WriteLine($"PREVIEW UPGRADE PASS: {checkedFiles} existing files and {checkedKeys} preference keys preserved; additional Linux settings/files allowed."); return 0;
}
if (args is ["--edit-disposable-save", var copiedSave])
{
    var full = Path.GetFullPath(copiedSave);
    if (!full.Split(Path.DirectorySeparatorChar).Contains("pkforge-full-test")) throw new ArgumentException("Use an explicitly disposable copy below pkforge-full-test.");
    var root = Path.GetDirectoryName(full)!; PKForge.Park.ParkPlatform.Configure(Path.Combine(root, "test-data"), root);
    var engine = new PKForge.Engine.SaveEngine(); var access = new PKForge.Park.LinuxSaveFileAccess();
    var sessions = new PKForge.Infrastructure.SaveSessionService(access, engine); await sessions.OpenAsync(new(full, Path.GetFileName(full)));
    var session = sessions.CurrentSession!; var at = session.Snapshot.Slots.FirstOrDefault(s => s.Species is > 0) ?? throw new InvalidDataException("This disposable save has no occupied Pokémon slots.");
    var operations = new PKForge.Application.SaveOperations(sessions, new PKForge.Infrastructure.SafeSaveWriter(engine, new PKForge.Infrastructure.FileBackupService(Path.Combine(root, "test-backups"), syncDirectory: PKForge.Application.DurableFiles.SyncDirectory), access), new(_ => null), engine);
    if (!await operations.ExecuteAsync(s => { s.ApplyEdit(at.Box, at.Slot, new(Nickname: "PORTTEST")); return new(true, "Disposable emulator nickname test"); }, PKForge.Domain.WriteScope.Only(new PKForge.Domain.SlotRef(at.Box, at.Slot)), PKForge.Domain.SaveAction.EditMon)) throw new IOException(operations.Status);
    Console.WriteLine($"DISPOSABLE EDIT PASS: Gen {sessions.CurrentSession!.Generation}; slot {at.Box}:{at.Slot}; validated write and verified backup. Original source was not opened for writing."); return 0;
}
if (args is ["--create-fixture", var fixturePath])
{
    var fixture = typeof(SaveIntegrationTests).GetMethod("Fixture", BindingFlags.NonPublic | BindingFlags.Static)!;
    File.WriteAllBytes(fixturePath, (byte[])fixture.Invoke(null, null)!); return 0;
}
if (args is ["--verify-ui-restoration", var originalPath, var restoredPath, var backupsPath])
{
    var original = File.ReadAllBytes(originalPath); var restored = File.ReadAllBytes(restoredPath);
    if (!original.AsSpan().SequenceEqual(restored)) throw new InvalidDataException("The native restore did not reproduce the original save bytes.");
    var backups = new PKForge.Infrastructure.FileBackupService(backupsPath); var points = await backups.ListAsync();
    var editedPoint = points.Where(p => p.ChangeDescription == "Restore point").OrderByDescending(p => p.CreatedUtc).First();
    var edited = (await backups.ReadAsync(editedPoint.BackupId)).ToArray();
    var engine = new PKForge.Engine.SaveEngine(); using var before = engine.OpenSession(original); using var after = engine.OpenSession(edited);
    if (before.ReadEntity(0, 0).Nickname != "MON" || after.ReadEntity(0, 0).Nickname != "A") throw new InvalidDataException("The on-screen keyboard edit was not preserved.");
    foreach (var slot in before.Snapshot.Slots.Where(s => s.Box != 0 || s.Slot != 0))
        if (!before.ExportSlot(slot.Box, slot.Slot).Data.AsSpan().SequenceEqual(after.ExportSlot(slot.Box, slot.Slot).Data)) throw new InvalidDataException($"Unrelated slot changed: {slot.Box}:{slot.Slot}");
    var savedOriginal = false;
    foreach (var point in points.Where(p => p.ChangeDescription == "Edit Pokémon"))
        if ((await backups.ReadAsync(point.BackupId)).Span.SequenceEqual(original)) savedOriginal = true;
    if (!savedOriginal) throw new InvalidDataException("The pre-edit backup does not contain the original bytes.");
    Console.WriteLine("NATIVE UI PASS: keyboard nickname MON → A, every unrelated slot unchanged, verified original and edited backups, byte-exact restoration."); return 0;
}
foreach (var type in new[] { typeof(SaveIntegrationTests), typeof(ApplicationWorkflowTests), typeof(CataloguePersistenceTests), typeof(ParkAnimationTests) })
foreach (var test in type.GetMethods().Where(method => method.GetCustomAttributesData().Any(attribute => attribute.AttributeType.FullName is "Xunit.FactAttribute" or "Xunit.TheoryAttribute")))
{
    var inline = test.GetCustomAttributesData().Where(a => a.AttributeType.FullName == "Xunit.InlineDataAttribute").ToArray();
    var rows = inline.Length == 0 ? new[] { Array.Empty<object?>() } : inline.Select(a => ((IEnumerable<CustomAttributeTypedArgument>)a.ConstructorArguments[0].Value!).Select(v => v.Value).ToArray()).ToArray();
    foreach (var row in rows)
    {
        count++; object? instance = null;
        try
        {
            instance = Activator.CreateInstance(type);
            if (test.Invoke(instance, row) is Task task) await task;
            Console.WriteLine("PASS " + test.Name + " " + string.Join(',', row));
        }
        catch (Exception error) { failed++; Console.Error.WriteLine("FAIL " + test.Name + ": " + (error.InnerException ?? error)); }
        finally { (instance as IDisposable)?.Dispose(); }
    }
}
Console.WriteLine($"DEVICE SAVE TESTS: {count - failed}/{count} passed on {System.Runtime.InteropServices.RuntimeInformation.OSDescription}.");
return failed == 0 ? 0 : 1;
