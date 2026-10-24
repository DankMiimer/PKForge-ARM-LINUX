using PKForge.Domain;

namespace PKForge.Application;

public sealed record CollectionAuditReport(int TotalChecked, IReadOnlyList<CloneGroup> CloneGroups,
    IReadOnlyList<SlotLegality> IllegalPokemon, IReadOnlyList<string> Lines);

/// <summary>The same read-only fingerprint collection and report on Android and Linux.</summary>
public static class AuditWorkflow
{
    public static CollectionAuditReport Analyze(ISaveEngineSession session, ILegalityService legality,
        Action<int, int>? onProgress = null, CancellationToken cancellationToken = default)
    {
        if (!session.SupportsLegalityAnalysis) throw new NotSupportedException("This save format has no offline legality tables.");
        var fingerprints = CollectFingerprints(session, cancellationToken);
        var verdicts = legality.Sweep(session, onProgress, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var clones = CollectionAudit.GroupClones(fingerprints); var illegal = verdicts.Where(v => !v.Valid).ToArray();
        return new(fingerprints.Count, clones, illegal, BuildReport(clones, illegal, fingerprints.Count));
    }

    public static IReadOnlyList<MonFingerprint> CollectFingerprints(ISaveEngineSession session, CancellationToken cancellationToken = default)
    {
        List<MonFingerprint> fingerprints = [];
        foreach (var summary in session.Snapshot.Slots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (summary.Species is not > 0) continue;
            var rng = session.GetRngInfo(summary.Box, summary.Slot);
            var detail = session.ReadEntity(summary.Box, summary.Slot);
            fingerprints.Add(new(rng.Pid, rng.EncryptionConstant, detail.OriginalTrainer, session.GetMetInfo(summary.Box, summary.Slot).TID,
                SlotLabel(summary.Box, summary.Slot), string.IsNullOrEmpty(detail.Nickname) ? detail.SpeciesName : detail.Nickname));
        }
        return fingerprints;
    }

    public static IReadOnlyList<string> BuildReport(IReadOnlyList<CloneGroup> clones, IReadOnlyList<SlotLegality> illegal, int total)
    {
        List<string> lines = [$"{total} Pokémon audited in this save.", ""];
        if (clones.Count == 0) lines.Add("No clones: every Pokémon has a unique identity fingerprint.");
        else
        {
            lines.Add($"## Clones ({clones.Count} groups)");
            foreach (var group in clones)
            {
                lines.Add($"{group.KeyKind} match ×{group.Members.Count}:");
                lines.AddRange(group.Members.Select(member => $"   {member.SlotLabel} - {member.DisplayName}"));
            }
        }
        lines.Add("");
        if (illegal.Count == 0) lines.Add("No impossible values: every Pokémon passed PKHeX's checks.");
        else
        {
            lines.Add($"## Impossible values ({illegal.Count})");
            lines.AddRange(illegal.Select(verdict => $"{SlotLabel(verdict.Box, verdict.Slot)} - {verdict.Problem}"));
        }
        lines.Add(""); lines.Add("Bank audit is future work: bank entries keep no EC/PID fingerprint.");
        return lines;
    }
    private static string SlotLabel(int box, int slot) => box < 0 ? $"Party {slot + 1}" : $"B{box + 1}-{slot + 1}";
}
