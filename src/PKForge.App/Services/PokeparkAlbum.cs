namespace PKForge.App.Services;

/// <summary>The Poképark photo album: photos the player takes of the park, each with what was
/// happening, kept in the app's own data folder (newest first, the last 200).</summary>
public static class PokeparkAlbum
{
    public sealed record Photo(string Path, string Caption, DateTime TakenAt);

    private const int Keep = 200;
    private static string Folder => System.IO.Path.Combine(FileSystem.AppDataDirectory, "pokepark-album");

    /// <summary>Writes an encoded JPEG and its caption.</summary>
    public static void Save(byte[] jpeg, string caption, DateTime takenAt)
    {
        Directory.CreateDirectory(Folder);
        var name = takenAt.ToString("yyyyMMdd-HHmmss-fff");
        File.WriteAllBytes(System.IO.Path.Combine(Folder, name + ".jpg"), jpeg);
        File.WriteAllText(System.IO.Path.Combine(Folder, name + ".txt"), caption);
        foreach (var old in List().Skip(Keep))
        {
            File.Delete(old.Path);
            File.Delete(System.IO.Path.ChangeExtension(old.Path, ".txt"));
        }
    }

    public static IReadOnlyList<Photo> List()
    {
        if (!Directory.Exists(Folder)) return [];
        return Directory.GetFiles(Folder, "*.jpg").OrderByDescending(p => p).Select(path =>
        {
            var caption = System.IO.Path.ChangeExtension(path, ".txt");
            var stamp = System.IO.Path.GetFileNameWithoutExtension(path);
            var taken = DateTime.TryParseExact(stamp, "yyyyMMdd-HHmmss-fff", null, System.Globalization.DateTimeStyles.None, out var at) ? at : File.GetCreationTime(path);
            return new Photo(path, File.Exists(caption) ? File.ReadAllText(caption) : "", taken);
        }).ToArray();
    }
}
