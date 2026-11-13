namespace PKForge.Domain;

/// <summary>The platform host schedules optional synchronization with the user's PKForge folder.</summary>
public interface IAppFolderSync
{
    void RequestSync(TimeSpan delay);
}
