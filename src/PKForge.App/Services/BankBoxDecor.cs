using PKForge.Infrastructure;

namespace PKForge.App.Services;

/// <summary>The Bank's box names and wallpapers, stored beside its index.</summary>
public static class BankBoxDecor
{
    private static readonly object Gate = new();
    private static string? _root;
    private static BankBoxNames? _names;
    private static BankBoxWallpapers? _wallpapers;

    private static string Root => Path.Combine(FileSystem.AppDataDirectory, "bank");

    private static void EnsureRoot()
    {
        var root = Root;
        if (_root == root) return;
        _names = new(root); _wallpapers = new(root); _root = root;
    }
    public static BankBoxNames Names { get { lock (Gate) { EnsureRoot(); return _names!; } } }

    public static BankBoxWallpapers Wallpapers { get { lock (Gate) { EnsureRoot(); return _wallpapers!; } } }

    /// <summary>Original Android preference key, shared by both platform hosts.</summary>
    public static int? LivingDexStartBox
    {
        get => Preferences.Default.Get("livingdex_bank_start_box", -1) is var box and >= 0 ? box : null;
        set
        {
            if (value is { } box) Preferences.Default.Set("livingdex_bank_start_box", box);
            else Preferences.Default.Remove("livingdex_bank_start_box");
        }
    }

    /// <summary>The box's name, or "Box 07" when it has none.</summary>
    public static string Label(int box) => Names.Get(box) ?? $"Box {box + 1:00}";
}
