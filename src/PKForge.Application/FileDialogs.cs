using PKForge.Domain;

namespace PKForge.Application;

public enum FilePurpose { Save, Pokemon, Music, Text, Image, Archive, Any, Pksm }
public sealed record FileDialogRequest(FilePurpose Purpose, string Title, bool Multiple = false, string? InitialDirectory = null);
public interface IFileDialogService
{
    Task<IReadOnlyList<PickedDocument>> PickAsync(FileDialogRequest request, CancellationToken cancellationToken = default);
    Task<PickedFolder?> PickFolderAsync(string title, CancellationToken cancellationToken = default);
    Task<string?> ExportPathAsync(string suggestedName, CancellationToken cancellationToken = default);
}

public static class FileKinds
{
    public static bool Accepts(FilePurpose purpose, string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return purpose switch
        {
            FilePurpose.Save => new[] { ".sav", ".srm", ".dsv", ".dat", ".bin", ".gci", ".raw", ".fla", ".eep" }.Contains(extension) || Path.GetFileName(path).Equals("main", StringComparison.OrdinalIgnoreCase),
            FilePurpose.Pokemon => new[] { ".pkm", ".pk1", ".pk2", ".pk3", ".pk4", ".pk5", ".pk6", ".pk7", ".pk8", ".pk9", ".pb7", ".pb8", ".pa8", ".pa9", ".ck3", ".xk3", ".bk4", ".rk4", ".sk2", ".pk3rr", ".pk3gsc", ".pk3ub" }.Contains(extension),
            FilePurpose.Music => new[] { ".mp3", ".ogg", ".wav", ".flac", ".opus" }.Contains(extension),
            FilePurpose.Text => new[] { ".txt", ".json", ".csv" }.Contains(extension),
            FilePurpose.Image => new[] { ".png", ".jpg", ".jpeg", ".bmp", ".webp" }.Contains(extension),
            FilePurpose.Archive => extension == ".zip",
            FilePurpose.Pksm => extension is ".bnk" or ".bin" or ".json" or ".zip" || Accepts(FilePurpose.Pokemon, path),
            _ => true
        };
    }
}
