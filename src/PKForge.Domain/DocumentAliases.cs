namespace PKForge.Domain;

/// <summary>Historical names of one physical save; persistent resident IDs and protection keys retain their original spelling.</summary>
public interface IDocumentAliases
{
    IReadOnlyList<string> For(string documentId);
}
