namespace PKForge.App.Views;

/// <summary>Linux owns text entry while the shared picker owns filtering and selection.</summary>
public static class TextPopup
{
    public static async Task<string?> ShowLineAsync(Grid host, string title, string hint, string current = "", string confirm = "Save", bool numeric = false,
        Action<string>? changed = null)
    {
        if (Entry.RequestText is not { } request) return null;
        var text = await request(title + " · " + hint, current);
        if (PKForge.App.App.IsSuspended) return null;
        if (text is not null) changed?.Invoke(text);
        return text?.Trim();
    }
}
