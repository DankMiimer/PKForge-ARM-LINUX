using PKForge.Domain;
using PKForge.Infrastructure;

namespace PKForge.Application;

/// <summary>Publishes Bank mutations with the same durable recovery protocol as multi-save transfers.</summary>
public sealed class JournaledBankService(FileBankService inner, string bankDirectory, string stagingDirectory,
    RecoveryJournal journal, MutationPolicy policy) : IBankService
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, (byte[] Bytes, BankEntryInfo Info)> _reads = [];
    private long _revision;
    public long Revision => Interlocked.Read(ref _revision);
    public void Reload() { lock (_gate) { inner.Reload(); _reads.Clear(); Interlocked.Increment(ref _revision); } }
    public Action<IReadOnlyList<BankEntry>, SaveAction>? Guard { get; set; }
    public IReadOnlyList<BankEntry> GetAll() => inner.GetAll();
    public int BoxCount => inner.BoxCount;
    public int MigrationVersion => inner.MigrationVersion;
    public byte[] GetData(Guid id)
    {
        lock (_gate)
        {
            var bytes = inner.GetData(id); var entry = inner.GetAll().Single(e => e.Id == id);
            _reads[id] = (bytes.ToArray(), entry.Info); return bytes;
        }
    }
    public void ValidateSnapshot(IBankService staged)
    {
        var expected = inner.GetAll().OrderBy(e => e.Id).ToArray(); var actual = staged.GetAll().OrderBy(e => e.Id).ToArray();
        if (inner.BoxCount != staged.BoxCount || inner.MigrationVersion != staged.MigrationVersion || !expected.SequenceEqual(actual))
            throw new IOException("The Bank index changed outside PKForge. Reopen the Bank before modifying it.");
    }
    public void ValidateReads(IBankService staged, IEnumerable<Guid> ids)
    {
        lock (_gate)
            foreach (var id in ids)
                if (_reads.TryGetValue(id, out var expected) && (!staged.GetData(id).AsSpan().SequenceEqual(expected.Bytes) || staged.GetAll().Single(e => e.Id == id).Info != expected.Info))
                    throw new IOException("A Bank entry changed outside PKForge. Reopen it before modifying it.");
    }
    private T Change<T>(SaveAction action, string description, Func<IBankService, T> change, IEnumerable<Guid>? ids = null)
        => ChangeStaged(action, description, staged => change(staged.Bank), ids);
    public Task<T> ExecuteAsync<T>(SaveAction action, string description, Func<BankStaging, T> change)
        => Task.Run(() => ChangeStaged(action, description, change));
    private T ChangeStaged<T>(SaveAction action, string description, Func<BankStaging, T> change, IEnumerable<Guid>? ids = null)
    {
        lock (_gate)
        {
            policy.Demand(action);
            Guard?.Invoke(ids is null ? [] : inner.GetAll().Where(e => ids.Contains(e.Id)).ToArray(), action);
            using var staged = new BankStaging(bankDirectory, stagingDirectory);
            ValidateSnapshot(staged.Bank);
            var before = inner.GetAll().ToDictionary(e => e.Id);
            if (ids is not null) ValidateReads(staged.Bank, ids);
            var result = change(staged);
            var after = staged.Bank.GetAll().ToDictionary(e => e.Id);
            Guard?.Invoke(before.Values.Where(e => !after.TryGetValue(e.Id, out var next) || e != next).ToArray(), action);
            Guard?.Invoke(after.Values.Where(e => !before.TryGetValue(e.Id, out var previous) || e != previous).ToArray(), action);
            var changes = staged.Changes();
            if (changes.Count == 0) return result;
            try
            {
                Task.Run(async () =>
                {
                    var record = await journal.PrepareAsync(description, changes).ConfigureAwait(false);
                    await journal.CommitAsync(record.Id).ConfigureAwait(false);
                }).GetAwaiter().GetResult();
            }
            finally { Reload(); }
            return result;
        }
    }
    public BankEntry Add(byte[] data, BankEntryInfo info) => Change(SaveAction.Duplicate, "Bank deposit", bank => bank.Add(data, info));
    public IReadOnlyList<BankEntry> AddMany(IReadOnlyList<BankDeposit> deposits) => Change(SaveAction.Duplicate, "Bank deposits", bank => bank.AddMany(deposits));
    public void Replace(Guid id, byte[] data, BankEntryInfo info) => Change(SaveAction.EditMon, "Bank Pokémon edit", bank => { bank.Replace(id, data, info); return true; }, [id]);
    public void Remove(Guid id) => Change(SaveAction.Release, "Bank release", bank => { bank.Remove(id); return true; }, [id]);
    public int RemoveMany(IReadOnlyList<Guid> ids) => Change(SaveAction.Release, "Bank releases", bank => bank.RemoveMany(ids), ids);
    public void Move(Guid id, int box, int slot) => Change(SaveAction.Move, "Bank move", bank => { bank.Move(id, box, slot); return true; }, inner.GetAll().Where(e => e.Id == id || e.Box == box && e.Slot == slot).Select(e => e.Id).ToArray());
    public void AddBox() => Change(SaveAction.Move, "Add Bank box", bank => { bank.AddBox(); return true; });
    public void RemapBoxes(BankBoxRemap remap) => Change(SaveAction.Move, "Organize Bank boxes", bank => { bank.RemapBoxes(remap); return true; }, inner.GetAll().Select(e => e.Id).ToArray());
    public int Place(IReadOnlyList<(Guid Id, int Box, int Slot)> placements) => Change(SaveAction.Move, "Organize Bank Pokémon", bank => bank.Place(placements), placements.Select(p => p.Id).ToArray());
    public int UpdateInfo(IReadOnlyList<(Guid Id, BankEntryInfo Info)> updates) => Change(SaveAction.Move, "Refresh Bank metadata", bank => bank.UpdateInfo(updates));
    public int CompleteMigration(int version, IReadOnlyList<(Guid Id, BankEntryInfo Expected, BankEntryInfo Info)> updates) => Change(SaveAction.Move, "Migrate Bank metadata", bank => bank.CompleteMigration(version, updates));
}
