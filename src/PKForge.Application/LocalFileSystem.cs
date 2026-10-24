using System.Runtime.InteropServices;
using PKForge.Domain;

namespace PKForge.Application;

public static class LocalPaths
{
    public static string Canonical(string path)
    {
        path = Path.GetFullPath(path);
        if (OperatingSystem.IsLinux())
        {
            var resolved = realpath(path, IntPtr.Zero);
            if (resolved == IntPtr.Zero) throw new IOException("File or folder unavailable: " + path);
            try { return Marshal.PtrToStringUTF8(resolved)!; } finally { free(resolved); }
        }
        var info = File.Exists(path) ? (FileSystemInfo)new FileInfo(path) : new DirectoryInfo(path);
        return info.ResolveLinkTarget(true)?.FullName ?? info.FullName;
    }
    [DllImport("libc", SetLastError = true)] private static extern IntPtr realpath([MarshalAs(UnmanagedType.LPUTF8Str)] string path, IntPtr buffer);
    [DllImport("libc")] private static extern void free(IntPtr pointer);
}

public sealed class LocalFolderFileAccess : IFolderFileAccess
{
    public ValueTask<IReadOnlyList<PickedDocument>> ListFilesAsync(string treeId, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyList<PickedDocument>>(Directory.EnumerateFiles(LocalPaths.Canonical(treeId)).Select(p => new PickedDocument(LocalPaths.Canonical(p), Path.GetFileName(p))).ToArray());
    public async ValueTask<ReadOnlyMemory<byte>> ReadFileAsync(string documentId, CancellationToken cancellationToken = default) => await File.ReadAllBytesAsync(LocalPaths.Canonical(documentId), cancellationToken);
    public async ValueTask WriteFileAsync(string treeId, string fileName, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
    {
        if (fileName != Path.GetFileName(fileName)) throw new IOException("Choose a file name, not a path.");
        var path = Path.Combine(LocalPaths.Canonical(treeId), fileName);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { await stream.WriteAsync(bytes, cancellationToken); stream.Flush(true); }
            File.Move(temporary, path, true);
            DurableFiles.SyncDirectory(Path.GetDirectoryName(path)!);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
