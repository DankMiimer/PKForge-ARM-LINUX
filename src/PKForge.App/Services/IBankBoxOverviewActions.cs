using PKForge.Domain;

namespace PKForge.App.Services;

/// <summary>Platform-owned durable mutations used by the shared Bank overview.</summary>
public interface IBankBoxOverviewActions
{
    int? LivingDexStartBox { get; }
    Task<bool> ApplyBoxRemapAsync(BankBoxRemap remap);
    Task<bool> RenameBoxAsync(int box);
    Task<bool> PickWallpaperAsync(int box);
    Task<bool> DeleteBoxAsync(int box);
}
