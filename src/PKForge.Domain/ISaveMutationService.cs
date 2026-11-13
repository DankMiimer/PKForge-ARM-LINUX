namespace PKForge.Domain;

/// <summary>The transaction boundary used by park actions, independent of an editor UI.</summary>
public interface ISaveMutationService
{
    string Status { get; }
    /// <summary>Hosts that replace the session after writes expose the current instance here.</summary>
    ISaveEngineSession? CurrentSession => null;
    Task<bool> RunMutationAsync(Func<ISaveEngineSession, GenerationOutcome> operation, int slot,
        bool refreshSlot = true, string? changeDescription = null, SaveAction action = SaveAction.EditMon);
    Task<bool> RunSlotMutationAsync(Func<ISaveEngineSession, GenerationOutcome> operation, SlotRef target,
        string? changeDescription = null, SaveAction action = SaveAction.EditMon)
        => RunMutationAsync(operation, target.Slot, false, changeDescription, action);
}
