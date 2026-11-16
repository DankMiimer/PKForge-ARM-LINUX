namespace PKForge.Linux.Native;

/// <summary>
/// A graphics driver that fails while PKForge sets up GPU drawing ends the app before its first
/// frame, every time. Each start notes that it began; the first frame on screen clears the note.
/// After two starts in a row that never showed a frame, this version starts with software
/// rendering from then on, and the log says why. A new PKForge version tries the GPU again.
/// </summary>
internal sealed class StartupGuard(string directory, string revision)
{
    private string Pending => Path.Combine(directory, "start-pending.txt");
    private string Software => Path.Combine(directory, "software-rendering.txt");
    private bool _cleared;

    /// <summary>True when this start should use software rendering.</summary>
    public bool Begin()
    {
        try
        {
            if (File.Exists(Software) && File.ReadAllText(Software).StartsWith(revision + "\n", StringComparison.Ordinal))
            {
                Console.WriteLine($"RENDER software: GPU drawing failed to start twice with {revision} (see {Software}; delete it to try the GPU again)");
                Write(Pending, "1");
                return true;
            }
            var unfinished = File.Exists(Pending) && int.TryParse(File.ReadAllText(Pending).Trim(), out var count) ? count : 0;
            if (unfinished >= 2)
            {
                Write(Software, revision + "\n" + $"{unfinished} starts in a row ended before their first frame\n");
                Console.WriteLine($"RENDER software: the last {unfinished} starts ended before their first frame; using software rendering for {revision}");
                Write(Pending, "1");
                return true;
            }
            Write(Pending, (unfinished + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine("RENDER start record: " + error.Message);
        }
        return false;
    }

    /// <summary>The first frame is on screen: this start worked.</summary>
    public void FirstFrame()
    {
        if (_cleared) return;
        _cleared = true;
        try { File.Delete(Pending); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Console.Error.WriteLine("RENDER start record: " + error.Message); }
    }

    private static void Write(string path, string text)
    {
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, text);
        File.Move(temporary, path, true);
    }
}
