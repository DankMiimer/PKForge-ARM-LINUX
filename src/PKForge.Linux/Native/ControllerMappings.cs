namespace PKForge.Linux.Native;

internal static class ControllerMappings
{
    public static string? Prepare(string? firmware, string? layout, IEnumerable<string> inherited,
        IEnumerable<string> userMappings, Func<string, string, string?> frontendMapping)
    {
        var custom = userMappings.Where(IsMapping).ToArray();
        var customGuids = custom.Select(line => line.Split(',')[0]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var changed = custom.Length > 0;
        var lines = inherited.Where(IsMapping).Select(line =>
        {
            if (!string.Equals(firmware, "knulli", StringComparison.OrdinalIgnoreCase)) return line;
            var fields = line.Split(',');
            if (customGuids.Contains(fields[0]) || frontendMapping(fields[0], fields[1]) is not { } frontend) return line;
            var normalized = KnulliLabels(firmware, layout, false, line, frontend);
            changed |= normalized is not null;
            return normalized ?? line;
        }).ToArray();
        // SDL's environment hint has USER priority, above AddMapping's API
        // priority. Install the complete merged hint before controller init.
        // Custom app mappings come last, preserving their explicit precedence.
        return changed ? string.Join('\n', lines.Concat(custom)) : null;
    }

    private static bool IsMapping(string line) =>
        !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith('#') && line.Split(',').Length >= 3;

    // KNULLI's default Ports generator translates frontend a/b/x/y to SDL
    // b/a/y/x. PKForge's prompts use the frontend's button names. Correct only
    // that measured convention, retaining every other generated binding.
    public static string? KnulliLabels(string? firmware, string? layout, bool userOverride,
        string active, string frontend)
    {
        if (!string.Equals(firmware, "knulli", StringComparison.OrdinalIgnoreCase) ||
            userOverride || (!string.IsNullOrEmpty(layout) && layout != "auto")) return null;
        var parts = active.Split(',');
        var expected = frontend.Split(',');
        if (parts.Length < 3 || expected.Length < 3 ||
            !string.Equals(parts[0], expected[0], StringComparison.OrdinalIgnoreCase)) return null;
        var actualFaces = Faces(parts); var expectedFaces = Faces(expected);
        if (actualFaces.Count != 4 || expectedFaces.Count != 4 ||
            expectedFaces.Values.Distinct().Count() != 4) return null;
        foreach (var (left, right) in new[] { ("a", "b"), ("b", "a"), ("x", "y"), ("y", "x") })
            if (actualFaces[left] != expectedFaces[right]) return null;
        for (var i = 2; i < parts.Length; i++)
        {
            var split = parts[i].IndexOf(':');
            if (split > 0 && expectedFaces.TryGetValue(parts[i][..split], out var value))
                parts[i] = parts[i][..(split + 1)] + value;
        }
        return string.Join(',', parts);
    }

    private static Dictionary<string, string> Faces(IEnumerable<string> fields)
    {
        Dictionary<string, string> result = [];
        foreach (var field in fields.Skip(2))
        {
            var split = field.IndexOf(':'); if (split < 1) continue;
            var name = field[..split];
            if (name is not ("a" or "b" or "x" or "y")) continue;
            if (split == field.Length - 1 || !result.TryAdd(name, field[(split + 1)..])) return [];
        }
        return result;
    }
}
