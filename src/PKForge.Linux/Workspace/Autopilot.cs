using PKForge.App.Services;
using PKForge.Application;
using PKForge.Domain;
using PKForge.Engine;
using SkiaSharp;

namespace PKForge.Linux.Workspace;

public sealed partial class WorkspaceHost
{
    private async Task AutopilotAsync()
    {
        Get<MutationPolicy>().Demand(SaveAction.BatchEdit);
        var saves = Get<ISaveCatalogue>().Saves.ToDictionary(s => s.DocumentId, StringComparer.Ordinal);
        if (Sessions.Current is { } current && !saves.ContainsKey(current.Document.DocumentId))
            saves.Add(current.Document.DocumentId, new(current.Document.DocumentId, current.Document.DisplayName, current.Snapshot.DisplayName ?? current.Document.DisplayName, EmulatorKind.RetroArch, false, null));
        List<LivingDexSource> sources = [LivingDexSources.FromBank(Bank)];
        foreach (var save in saves.Values.ToArray())
        {
            var bytes = await Get<ISaveFileAccess>().ReadAsync(save.DocumentId);
            using var session = Get<ISaveEngine>().OpenSession(bytes, save.EngineHint, save.Format);
            var refusal = Get<ISafeSaveWriter>().WhyWritesAreRefused(save.DocumentId, session.Snapshot);
            var exclusion = refusal is not null ? LivingDexExclusion.ReadOnly : session is not SaveEngineSession || Get<ISafeSaveWriter>().LayoutRiskOf(save.DocumentId, session.Snapshot) is not null ? LivingDexExclusion.RomHack : LivingDexExclusion.None;
            var source = LivingDexSources.FromSession(save.DocumentId, save.GameLabel, session, bytes.Span, exclusion, refusal);
            var protection = Get<ProtectionStore>();
            sources.Add(source with
            {
                Holdings = source.Holdings.Where(h => !protection.IsBoxLocked(save.DocumentId, h.Box) && protection.CanRelease(save.DocumentId, h.Box, h.Slot, session.GetRngInfo(h.Box, h.Slot).Pid)).ToArray(),
                FreeSlots = source.FreeSlots?.Where(at => !protection.IsBoxLocked(save.DocumentId, at.Box)).ToArray()
            });
        }
        var destinations = sources.Where(s => !s.IsExcluded).ToArray();
        var chosen = await Dialogs.PickAsync("Living Dex destination", destinations.Select(s => s.Label).ToArray()); if (chosen is null) return;
        var options = (LivingDexOptions?)await Forms.RecordAsync("Living Dex options", typeof(LivingDexOptions), new LivingDexOptions(destinations[chosen.Value].Id), enabled: f => !f.Equals("DestinationId", StringComparison.OrdinalIgnoreCase));
        if (options is null) return;
        var plan = await Task.Run(() => LivingDexPlanner.Plan(LivingDexCatalogBuilder.Build(), sources, options));
        var protections = Get<ProtectionStore>(); var bankEntries = Bank.GetAll().ToDictionary(e => e.Id);
        var bankProtected = bankEntries.Values.Where(e => protections.IsBoxLocked("bank", e.Box) || !Pokemon.Read(new(e.Box, e.Slot, e.Id, true), (s, b, i) => protections.CanRelease("bank", e.Box, e.Slot, s.GetRngInfo(b, i).Pid))).Select(e => e.Id).ToHashSet();
        plan = plan with { Steps = plan.Steps.Select(step => step.Blocked is not null ? step : step.Holding?.BankId is { } bankId && bankProtected.Contains(bankId)
            ? step with { Blocked = "Source Bank Pokémon or box is protected." }
            : plan.DestinationKind == LivingDexSourceKind.Bank && step.Destination is { } at && protections.IsBoxLocked("bank", at.Box)
                ? step with { Blocked = "Destination Bank box is locked." } : step).ToArray() };
        Screens.Push(new AutopilotRouteScreen(plan));
        try
        {
            var action = await Dialogs.MenuAsync("Living Dex dry run", $"{plan.CoveredBefore}/{plan.TargetCount} covered · {plan.Steps.Count} planned steps", "Stage plan", "Inspect routes", "Cancel");
            if (action == "Inspect routes") { await InspectAsync("Planned routes", plan.Steps); action = await Dialogs.MenuAsync("Living Dex", "Review complete.", "Stage plan", "Cancel"); }
            if (action != "Stage plan") return;
            var executor = new LivingDexExecutor(Get<ISaveEngine>(), Get<ISafeSaveWriter>(), Get<ISaveFileAccess>(), Bank, Get<IEvolutionService>(), Get<ILegalityService>(), Sessions);
            using var staged = await executor.StageAsync(plan, saves);
            if (!await Dialogs.ConfirmAsync("Apply Living Dex plan", $"{staged.Staged} staged · {staged.Failed} failed · {staged.IllegalResults} imperfect legality. Originals will be preserved in the recovery journal.", "Apply")) return;
            using var bank = new BankStaging(Path.Combine(ParkPlatform.DataDirectory, "bank"), Path.Combine(ParkPlatform.DataDirectory, "cache", "staging"));
            var bankService = Get<JournaledBankService>();
            bankService.ValidateSnapshot(bank.Bank);
            bankService.ValidateReads(bank.Bank, staged.StepOutcomes.Where(o => o.Step.Holding?.BankId is not null).Select(o => o.Step.Holding!.BankId!.Value));
            staged.ApplyToStagedBank(bank.Bank);
            var beforeEntries = Bank.GetAll().ToDictionary(e => e.Id); var afterEntries = bank.Bank.GetAll().ToDictionary(e => e.Id);
            bankService.Guard?.Invoke(beforeEntries.Values.Where(e => !afterEntries.TryGetValue(e.Id, out var next) || e != next).ToArray(), SaveAction.Move);
            bankService.Guard?.Invoke(afterEntries.Values.Where(e => !beforeEntries.TryGetValue(e.Id, out var previous) || e != previous).ToArray(), SaveAction.Move);
            var changes = staged.ExportPreparedSaves().Select(s => new PreparedFileChange(s.Save.DocumentId, s.Original, s.Candidate, s.Save.DocumentId == options.DestinationId ? 0 : 3, true, s.Scope, s.Save.EngineHint, s.Save.Format)).ToList();
            changes.AddRange(bank.Changes(plan.DestinationKind == LivingDexSourceKind.Bank ? 0 : 3, 5));
            var journal = Get<RecoveryJournal>(); var record = await journal.PrepareAsync("Living Dex Autopilot", changes); await journal.CommitAsync(record.Id);
            await RefreshStoresAsync(); await InspectAsync("Autopilot results", staged.StepOutcomes);
        }
        finally { Screens.Pop(); }
    }
}

internal sealed class AutopilotRouteScreen(LivingDexPlan plan) : INativeScreen
{
    public SecondScreenOwner Owner => SecondScreenOwner.Autopilot;
    private int _index;
    public void Paint(SKCanvas canvas, SKRect bounds, bool secondary)
    {
        NativePaint.Background(canvas, bounds, "Living Dex · " + plan.DestinationLabel);
        if (secondary) { NativePaint.Paragraph(canvas, bounds, $"{plan.CoveredBefore}/{plan.TargetCount} covered\n" + (plan.Steps.Count > 0 ? NativeForms.Describe(plan.Steps[Math.Clamp(_index, 0, plan.Steps.Count - 1)]) : "No moves required.")); return; }
        var steps = plan.Steps.Skip(_index).Take(7).ToArray();
        for (var i = 0; i < steps.Length; i++) NativePaint.Row(canvas, SKRect.Create(12, 66 + i * 48, bounds.Width - 24, 44), NativeForms.Display(steps[i]), i == 0);
        NativePaint.Footer(canvas, bounds, "D-pad: route · B: back");
    }
    public bool Button(PadButton button)
    {
        if (button == PadButton.Down) _index = Math.Min(Math.Max(0, plan.Steps.Count - 1), _index + 1);
        if (button == PadButton.Up) _index = Math.Max(0, _index - 1);
        return button != PadButton.B;
    }
}
