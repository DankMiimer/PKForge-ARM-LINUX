using PKForge.Domain;
using PKForge.Engine;
using PKForge.Infrastructure;

namespace PKForge.Application;

/// <summary>The original PKSM codecs and exact format conversion, shared by native and Android hosts.</summary>
public static class PksmWorkflow
{
    public static PksmDecoded Decode(PksmGeneration? generation, byte[] bytes, string source)
    {
        var number = generation is { } tag ? PksmBankFile.GenerationNumber(tag) : (int?)null;
        if (number == 0) return PksmDecoded.Skip("Unknown generation tag");
        var result = PksmEntityConversion.Decode(number, generation == PksmGeneration.LetsGo, bytes, source);
        return new(result.Bytes, result.Info, result.Reason);
    }
    public static PksmEncoded Encode(byte[] bytes, string? format)
    {
        var result = PksmEntityConversion.Encode(bytes, format);
        return result.Data is not null && PksmBankFile.FromGenerationNumber(result.Generation, result.LetsGo) is { } tag
            ? new(tag, result.Data, null) : PksmEncoded.Skip(result.Reason ?? "PKSM has no slot for this format");
    }
    public static (IReadOnlyList<PksmImportPlan> Plans, IReadOnlyList<string> Notes) Plan(IEnumerable<(string, byte[])> files)
    {
        var sources = PksmBankTransfer.Collect(files); List<PksmImportPlan> plans = []; List<string> notes = [.. sources.Ignored];
        foreach (var (name, bank, names) in sources.Banks)
        {
            try { plans.Add(PksmBankTransfer.PlanBank(name, bank, names, Decode)); }
            catch (InvalidDataException error) { notes.Add(error.Message); }
        }
        if (sources.LooseFiles.Count > 0) plans.Add(PksmBankTransfer.PlanLoose("Loose Pokémon files", sources.LooseFiles, Decode));
        return (plans, notes);
    }
}
