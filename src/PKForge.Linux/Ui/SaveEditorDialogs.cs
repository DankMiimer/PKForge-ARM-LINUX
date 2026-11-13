namespace PKForge.App.Views;

// Save editors use the shared painted menu; numeric entry uses the host's keyboard.
internal static class EditorMenu
{
    public static Task<string?> ShowAsync(Grid host, string title, string? message, params string[] options)
        => PadMenu.ShowAsync(host, title, message, options.Select(o => new PadOption(o)).ToArray());
    public static Task<string?> ShowAsync(Grid host, string title, string? message, params PadOption[] options)
        => PadMenu.ShowAsync(host, title, message, options);
    public static Task<bool> ConfirmAsync(Grid host, string title, string message, string confirmLabel = "OK")
        => PadMenu.ConfirmAsync(host, title, message, confirmLabel);
}
public static class StatsPopup
{
    public static async Task<int?> ShowSingleAsync(Grid host, string title, int current, int max, string? hint = null)
    {
        while (true)
        {
            var value = await TextPopup.ShowLineAsync(host, title, hint ?? $"0–{max}", current.ToString(), "Apply", numeric: true);
            if (value is null) return null;
            if (int.TryParse(value.Trim(), out var number) && number >= 0 && number <= max) return number;
            await EditorMenu.ShowAsync(host, title, $"Enter a whole number from 0 to {max}.", "OK");
        }
    }
}
