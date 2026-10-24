using System.Text.Json;
using PKForge.Domain;

namespace PKForge.Application;

/// <summary>Read-only compatibility adapter over existing preview data. It does not rewrite settings, residents, photographs, identities or backup metadata.</summary>
public sealed class DocumentAliases : IDocumentAliases
{
    private readonly Dictionary<string, List<string>> _aliases = new(StringComparer.Ordinal);
    private readonly Func<string, string> _canonical;
    public DocumentAliases(string dataDirectory, Func<string, string>? canonical = null)
    {
        _canonical = canonical ?? LocalPaths.Canonical;
        var settings = Path.Combine(dataDirectory, "settings.json");
        if (File.Exists(settings))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(settings));
            foreach (var property in json.RootElement.EnumerateObject())
            {
                if (property.Name.StartsWith("pkforge.protection.", StringComparison.Ordinal)) Remember(property.Name["pkforge.protection.".Length..]);
                if (property.Name == "linux.last-save" && property.Value.ValueKind == JsonValueKind.String) Remember(property.Value.GetString());
            }
        }
        var identities = Path.Combine(dataDirectory, "save-identities.json");
        if (File.Exists(identities)) ReadDocuments(identities);
        var backups = Path.Combine(dataDirectory, "backups");
        if (Directory.Exists(backups)) foreach (var path in Directory.EnumerateFiles(backups, "*.json", SearchOption.AllDirectories)) ReadDocuments(path);
    }
    private void ReadDocuments(string path)
    {
        try { using var json = JsonDocument.Parse(File.ReadAllText(path)); Visit(json.RootElement); }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        { Console.Error.WriteLine("Alias metadata unavailable: " + Path.GetFileName(path) + " · " + error.Message); }
        void Visit(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array) { foreach (var item in element.EnumerateArray()) Visit(item); }
            if (element.ValueKind != JsonValueKind.Object) return;
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals("DocumentId", StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind == JsonValueKind.String) Remember(property.Value.GetString());
                else if (property.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object) Visit(property.Value);
            }
        }
    }
    private void Remember(string? path)
    {
        if (string.IsNullOrEmpty(path) || !Path.IsPathRooted(path)) return;
        try { var key = _canonical(path); if (!_aliases.TryGetValue(key, out var aliases)) _aliases[key] = aliases = []; if (!aliases.Contains(path)) aliases.Add(path); }
        catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    public IReadOnlyList<string> For(string documentId)
    {
        if (documentId == "bank") return [documentId];
        try { var key = _canonical(documentId); return new[] { documentId, key }.Concat(_aliases.GetValueOrDefault(key) ?? []).Distinct(StringComparer.Ordinal).ToArray(); }
        catch (IOException) { return [documentId]; } catch (UnauthorizedAccessException) { return [documentId]; }
    }
}

public sealed class AliasedSaveIdentityStore(ISaveIdentityStore inner, IDocumentAliases aliases) : ISaveIdentityStore
{
    public event Action<string>? Changed { add => inner.Changed += value; remove => inner.Changed -= value; }
    public SaveIdentity? Get(string documentId) => aliases.For(documentId).Select(inner.Get).FirstOrDefault(identity => identity is not null) is { } found ? found with { DocumentId = documentId } : null;
    public void Set(SaveIdentity identity)
    {
        foreach (var alias in aliases.For(identity.DocumentId)) inner.Set(identity with { DocumentId = alias });
    }
    public void Reset(string documentId) { foreach (var alias in aliases.For(documentId)) inner.Reset(alias); }
}
