namespace PKForge.Domain;

/// <summary>The platform's known saves, identity metadata and rescan action.</summary>
public interface ISaveLibraryShelf
{
    IReadOnlyList<DetectedSave> Saves { get; }
    IReadOnlyList<DetectedSave> HiddenSaves { get; }
    ISaveIdentityStore Identities { get; }
    Task RescanAsync();
}
