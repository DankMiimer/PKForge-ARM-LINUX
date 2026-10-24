using PKForge.Domain;

namespace PKForge.Application;

public static class SaveLibraryWorkflow
{
    public static IReadOnlyList<IReadOnlyList<DetectedSave>> Group(IEnumerable<DetectedSave> saves, string filter, string query = "")
    {
        IEnumerable<IReadOnlyList<DetectedSave>> groups = SaveShelf.Group(saves.Where(s => query.Length == 0 || (s.GameLabel + " " + s.TrainerName + " " + s.FileName).Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray());
        var generation = filter.StartsWith("gen", StringComparison.Ordinal) && int.TryParse(filter[3..], out var value) ? value : 0;
        if (generation > 0) groups = groups.Where(g => g[0].Generation == generation);
        else if (filter is "gb" or "gba" or "ds" or "3ds" or "switch") groups = groups.Where(g => ConsoleOf(GameLabel(g[0]), g[0].Generation) == filter);
        if (filter == "az") groups = groups.OrderBy(g => g[0].GameLabel, StringComparer.OrdinalIgnoreCase);
        if (filter == "release") groups = groups.OrderBy(g => ReleaseRank(GameLabel(g[0]), g[0].Generation)).ThenBy(g => g[0].GameLabel, StringComparer.OrdinalIgnoreCase);
        return groups.ToArray();
    }
    private static string GameLabel(DetectedSave save) => save.Identity?.GameLabel ?? save.Guess?.Label ?? save.GameLabel;
    public static string ConsoleOf(string label, int generation) => label.Contains("Let's Go", StringComparison.Ordinal) ? "switch" : generation switch { 1 or 2 => "gb", 3 => "gba", 4 or 5 => "ds", 6 or 7 => "3ds", >= 8 => "switch", _ => "unknown" };
    public static int ReleaseRank(string label, int generation) => label switch
    {
        _ when label.Contains("FireRed", StringComparison.Ordinal) || label.Contains("LeafGreen", StringComparison.Ordinal) || label.Contains("Unbound", StringComparison.Ordinal) || label.Contains("Radical Red", StringComparison.Ordinal) || label.Contains("GS Chronicles", StringComparison.Ordinal) => 2004,
        _ when label.Contains("HeartGold", StringComparison.Ordinal) || label.Contains("SoulSilver", StringComparison.Ordinal) => 2009,
        _ when label.Contains("Black 2", StringComparison.Ordinal) || label.Contains("White 2", StringComparison.Ordinal) => 2012,
        _ when label.Contains("Omega", StringComparison.Ordinal) || label.Contains("Alpha", StringComparison.Ordinal) => 2014,
        _ when label.Contains("Ultra", StringComparison.Ordinal) => 2017,
        _ when label.Contains("Let's Go", StringComparison.Ordinal) => 2018,
        _ when label.Contains("Brilliant", StringComparison.Ordinal) || label.Contains("Shining", StringComparison.Ordinal) || label.Contains("Luminescent", StringComparison.Ordinal) => 2021,
        _ when label.Contains("Legends: Arceus", StringComparison.Ordinal) => 2022,
        _ when label.Contains("Legends: Z-A", StringComparison.Ordinal) => 2025,
        _ when label.Contains("Colosseum", StringComparison.Ordinal) => 2003,
        _ when label.Contains("XD", StringComparison.Ordinal) => 2005,
        _ when label.Contains("Box", StringComparison.Ordinal) => 2002,
        _ when label.Contains("Red", StringComparison.Ordinal) || label.Contains("Blue", StringComparison.Ordinal) || label.Contains("Green", StringComparison.Ordinal) => 1996,
        _ when label.Contains("Yellow", StringComparison.Ordinal) => 1998,
        _ when label.Contains("Gold", StringComparison.Ordinal) || label.Contains("Silver", StringComparison.Ordinal) => 1999,
        _ when label.Contains("Crystal", StringComparison.Ordinal) => 2000,
        _ when label.Contains("Ruby", StringComparison.Ordinal) || label.Contains("Sapphire", StringComparison.Ordinal) => 2002,
        _ when label.Contains("Emerald", StringComparison.Ordinal) => 2004,
        _ when label.Contains("Diamond", StringComparison.Ordinal) || label.Contains("Pearl", StringComparison.Ordinal) => 2006,
        _ when label.Contains("Platinum", StringComparison.Ordinal) => 2008,
        _ when label.Contains("Black", StringComparison.Ordinal) || label.Contains("White", StringComparison.Ordinal) => 2010,
        _ when label.Contains("X", StringComparison.Ordinal) || label.Contains("Y", StringComparison.Ordinal) => 2013,
        _ when label.Contains("Sun", StringComparison.Ordinal) || label.Contains("Moon", StringComparison.Ordinal) => 2016,
        _ when label.Contains("Scarlet", StringComparison.Ordinal) || label.Contains("Violet", StringComparison.Ordinal) || label.Contains("Compass", StringComparison.Ordinal) => 2022,
        _ => generation * 1000
    };
}
