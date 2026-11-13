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
    string StatusText => "";
    Task<bool> ToggleBoxLockAsync(int box) => Task.FromResult(false);
    Task<bool> ChangeGroupsAsync(Func<IReadOnlyList<BankBoxGroup>, IReadOnlyList<BankBoxGroup>> change, string? done) => Task.FromResult(false);
    async Task<bool> ApplyBoxRemapAsync(BankBoxRemap remap, Func<IReadOnlyList<BankBoxGroup>, IReadOnlyList<BankBoxGroup>>? changeGroups)
    {
        if (!await ApplyBoxRemapAsync(remap)) return false;
        return changeGroups is null || await ChangeGroupsAsync(changeGroups, null);
    }
}
