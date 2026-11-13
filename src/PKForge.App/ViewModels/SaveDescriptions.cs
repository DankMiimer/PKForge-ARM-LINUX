using PKForge.Domain;

namespace PKForge.App.ViewModels;

/// <summary>Shared one-line descriptions of a save for sheets and pickers.</summary>
public static class SaveDescriptions
{
    public static string Detail(DetectedSave save)
    {
        var parts = new List<string> { EmulatorName(save.Emulator) };
        if (!string.IsNullOrEmpty(save.FolderHint)) parts.Add(save.FolderHint!);
        if (save.LastModified is { } modified) parts.Add(modified.ToLocalTime().ToString("MMM d, yyyy"));
        return string.Join(" · ", parts);
    }

    /// <summary>A tile: its emulators and the latest play date ("melonDS, DraStic · Sep 3, 2026").</summary>
    public static string GroupDetail(IReadOnlyList<DetectedSave> saves) => PKForge.Chrome.HomeCardText.Detail(saves);

    public static string EmulatorName(EmulatorKind kind) => PKForge.Chrome.HomeCardText.Emulator(kind);
}
