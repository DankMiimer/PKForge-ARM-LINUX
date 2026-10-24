using PKForge.Domain;
using PKForge.Engine;

namespace PKForge.Application;

/// <summary>PKF1 keeps its v3.1.1 wire format. Equal-sized entity formats need an explicit source choice.</summary>
public static class QrImportWorkflow
{
    public static IReadOnlyList<BankEntryInfo> DescribeCandidates(ISaveEngine engine, QrEntityPayload payload)
    {
        var formats = EntityBytes.Candidates(payload.EntityBytes).AsEnumerable();
        formats = formats.Concat(payload.Generation switch
        {
            3 => ["PK3RR", "PK3GSC", "PK3UB"],
            6 => ["PK6"], 7 => ["PK7", "PB7"],
            8 => ["PK8", "PB8", "PA8"], 9 => ["PK9", "PA9"],
            _ => Array.Empty<string>()
        });
        List<BankEntryInfo> candidates = [];
        foreach (var format in formats.Distinct())
        {
            try
            {
                var info = engine.TryDescribeEntity(payload.EntityBytes, "QR transfer", format);
                if (info is not null && info.Generation == payload.Generation && info.Format == format) candidates.Add(info);
            }
            catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException or InvalidDataException)
            { /* A different game's record is not a readable candidate. */ }
        }
        return candidates;
    }
}
