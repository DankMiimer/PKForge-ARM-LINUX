using PKForge.Domain;

namespace PKForge.App.Views;

/// <summary>Portable transaction boundary for save-wide editors.</summary>
internal static class SaveEditorWrites
{
    internal static Task<bool> WriteAsync(ISaveMutationService mutations, Func<ISaveEngineSession, string> edit,
        SaveAction action = SaveAction.EditWorld) => mutations.RunMutationAsync(session =>
        {
            try { return new(true, edit(session)); }
            catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or ArgumentException)
            { return new(false, ErrorText.Plain(ex)); }
        }, 0, refreshSlot: false, action: action);
}
