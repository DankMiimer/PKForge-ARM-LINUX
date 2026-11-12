using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using PKForge.App.Services;
using PKForge.App.Views;
using PKForge.Domain;
using PKForge.Engine;
using PKForge.Linux.Native;
using PKForge.Linux.Workspace;
using PKForge.Park;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using Xunit;

namespace PKForge.Linux.Tests;

public sealed class PickerTests
{
    [Fact]
    public async Task Type_pickers_preserve_stellar_and_mark_unavailable_hidden_power_spreads()
    {
        using var fixture = new Fixture();
        var tera = InfoPickers.ShowTeraAsync(fixture.Host, [new(10, "Water"), new(99, "Stellar")], 99, [10]);
        fixture.Draw(); var list = Find<CollectionView>(fixture.Host).Single();
        Assert.Equal(99, Assert.IsType<PickItem>(list.SelectedItem).Id);
        Assert.Equal("Own type", list.ItemsSource.Cast<PickItem>().First().Tag);
        fixture.Router.Dispatch(PadButton.A); Assert.Equal(99, (await tera)!.Id);
        var hidden = InfoPickers.ShowHiddenPowerAsync(fixture.Host, 1, type => type == 1 ? null : [31, 31, 31, 31, 31, 31]);
        fixture.Draw(); list = Find<CollectionView>(fixture.Host).Single();
        Assert.Equal(Enumerable.Range(1, 16), list.ItemsSource.Cast<PickItem>().Select(item => item.Id));
        var unavailable = list.ItemsSource.Cast<PickItem>().First();
        Assert.True(unavailable.Muted); Assert.Contains("Not reachable", unavailable.Detail);
        fixture.Router.Dispatch(PadButton.B); Assert.Null(await hidden);
    }

    [Fact]
    public async Task Move_fact_snapshot_keeps_legal_order_details_and_the_selected_game_id()
    {
        using var fixture = new Fixture();
        MoveChoice[] moves = [
            new(33, 0, MoveCategory.Physical, 40, 100, 35, new(LearnKind.LevelUp, 15), "A physical attack."),
            new(55, 10, MoveCategory.Special, 40, 100, 25, new(LearnKind.None), "A water attack."),
        ];
        var before = moves.ToArray();
        var picker = InfoPickers.ShowMovesAsync(fixture.Host, "Moves", new GameDataService(), moves, 33);
        fixture.Draw(); var list = Find<CollectionView>(fixture.Host).Single();
        Assert.Equal(new[] { 0, 33 }, list.ItemsSource.Cast<PickItem>().Select(item => item.Id));
        var row = list.ItemsSource.Cast<PickItem>().Last();
        Assert.Equal(0, row.TypeId); Assert.Equal(MoveCategory.Physical, row.Category);
        Assert.Contains("15", row.Tag); Assert.Contains("40", row.Detail);
        fixture.Router.Dispatch(PadButton.Y); fixture.Draw();
        Assert.Equal(new[] { 0, 33, 55 }, list.ItemsSource.Cast<PickItem>().Select(item => item.Id));
        Assert.True(list.ItemsSource.Cast<PickItem>().Last().Muted);
        fixture.Router.Dispatch(PadButton.Down); fixture.Router.Dispatch(PadButton.A);
        Assert.Equal(55, (await picker)!.Id); Assert.Equal(before, moves);
    }

    [Fact]
    public async Task Held_item_snapshot_uses_game_id_space_and_current_nonholdable_item_stays_reachable()
    {
        using var fixture = new Fixture();
        // These deliberately differ from the modern item table's IDs.
        string[] names = ["", "Potion", "Master Ball", "Oran Berry"];
        var picker = InfoPickers.ShowHeldItemsAsync(fixture.Host, "Held item", names, new HashSet<int> { 3 }, 2, _ => null);
        fixture.Draw(); var list = Find<CollectionView>(fixture.Host).Single();
        Assert.Equal(2, Assert.IsType<PickItem>(list.SelectedItem).Id);
        Assert.Equal("Master Ball", Assert.IsType<PickItem>(list.SelectedItem).Name);
        Assert.Equal(new[] { 0, 3, 1, 2 }, list.ItemsSource.Cast<PickItem>().Select(item => item.Id));
        fixture.Router.Dispatch(PadButton.Y); fixture.Draw();
        Assert.Equal(new[] { 0, 3 }, list.ItemsSource.Cast<PickItem>().Select(item => item.Id));
        fixture.Router.Dispatch(PadButton.Down); fixture.Router.Dispatch(PadButton.A);
        Assert.Equal(3, (await picker)!.Id); Assert.Equal("Oran Berry", names[3]);
    }

    [Fact]
    public async Task Long_list_virtualizes_rows_and_search_preserves_original_ids()
    {
        using var fixture = new Fixture();
        var picker = new NativeDialogs(fixture.Host, fixture.Router).PickAsync("Fixture",
            Enumerable.Range(0, 1200).Select(i => $"Entry {i:D4}").ToArray(), 1100);
        fixture.Draw();
        var list = Find<CollectionView>(fixture.Host).Single();
        Assert.InRange(list.RetainedRowCount, 1, 20);
        Assert.Equal(1100, Assert.IsType<PickItem>(list.SelectedItem).Id);
        Find<Entry>(fixture.Host).Single().Text = "1199";
        fixture.Draw(); Assert.Equal(1, list.RetainedRowCount);
        fixture.Router.Dispatch(PadButton.A);
        Assert.Equal(1199, await picker);
        Assert.Empty(fixture.Host.Children); Assert.False(fixture.Router.Dispatch(PadButton.A));
        Assert.Equal(0, list.RetainedRowCount);
    }

    [Fact]
    public async Task Empty_search_cannot_choose_a_stale_selection_and_current_hidden_item_remains_reachable()
    {
        using var fixture = new Fixture();
        var picker = PickerMenu.ShowAsync(fixture.Host, "Filter", [new(1, "Legal"), new(8, "Other") { Muted = true }], 8,
            filter: new("Legal only", "Show all", item => !item.Muted));
        fixture.Draw(); var list = Find<CollectionView>(fixture.Host).Single();
        Assert.Equal(8, Assert.IsType<PickItem>(list.SelectedItem).Id);
        fixture.Router.Dispatch(PadButton.Y); fixture.Draw();
        Assert.Equal(1, Assert.IsType<PickItem>(list.SelectedItem).Id);
        Find<Entry>(fixture.Host).Single().Text = "absent"; fixture.Draw();
        fixture.Router.Dispatch(PadButton.A); Assert.False(picker.IsCompleted);
        fixture.Router.Dispatch(PadButton.B); Assert.Null(await picker);
    }

    [Fact]
    public async Task Touch_drag_scrolls_without_picking_and_preview_requires_a_second_tap()
    {
        using var fixture = new Fixture(); var highlighted = new List<int>();
        var picker = PickerMenu.ShowAsync(fixture.Host, "Preview", Enumerable.Range(0, 100).Select(i => new PickItem(i, $"Item {i}")).ToArray(),
            preview: new(new Label { Text = "Preview", HeightRequest = 20 }, item => { if (item is not null) highlighted.Add(item.Id); }));
        fixture.Draw(); var list = Find<CollectionView>(fixture.Host).Single();
        var start = new SKPoint(list.Bounds.MidX, list.Bounds.Top + 10);
        var initialHighlights = highlighted.Count;
        list.HandleTouch(new(1, SKTouchAction.Pressed, start));
        list.HandleTouch(new(1, SKTouchAction.Moved, new(start.X, start.Y - 90)));
        list.HandleTouch(new(1, SKTouchAction.Released, new(start.X, start.Y - 90)));
        Assert.False(picker.IsCompleted); Assert.Equal(initialHighlights, highlighted.Count);
        // A fresh draw cancels the drag's row touch ownership and exposes the new rows.
        fixture.Draw(); var hit = new SKPoint(list.Bounds.MidX, list.Bounds.Top + 15);
        list.HandleTouch(new(2, SKTouchAction.Pressed, hit)); list.HandleTouch(new(2, SKTouchAction.Released, hit));
        Assert.False(picker.IsCompleted); Assert.True(highlighted.Count > 1);
        list.HandleTouch(new(3, SKTouchAction.Pressed, hit)); list.HandleTouch(new(3, SKTouchAction.Released, hit));
        Assert.NotNull(await picker); Assert.Empty(fixture.Host.Children);
    }

    [Fact]
    public async Task Suspend_cancels_picker_and_ignores_late_keyboard_result()
    {
        using var fixture = new Fixture(); var result = new TaskCompletionSource<string?>();
        Entry.RequestText = (_, _) => result.Task;
        var picker = PickerMenu.ShowAsync(fixture.Host, "Search", [new(1, "One")]);
        fixture.Draw(); var search = Find<Entry>(fixture.Host).Single();
        fixture.Router.Dispatch(PadButton.Select);
        PKForge.App.App.Suspend(); Assert.Null(await picker); Assert.Empty(fixture.Host.Children);
        result.SetResult("late"); await Task.Delay(20); UiDispatcher.Instance.Pump();
        Assert.Equal("", search.Text); Assert.False(fixture.Router.Dispatch(PadButton.A));
        Assert.Equal(0, UiDispatcher.Instance.ActiveTimerCount);
    }

    [Fact]
    public async Task Nature_highlight_uses_read_only_stats_and_staged_form_values()
    {
        using var fixture = new Fixture(); var dialogs = new NativeDialogs(fixture.Host, fixture.Router);
        IReadOnlyDictionary<string, object?>? seen = null;
        var current = new EntityEdit(Species: 1, Level: 50, Nature: 3, IVs: [31, 31, 31, 31, 31, 31]);
        var form = OnUi(() => new NativeForms(dialogs).EditAsync<EntityEdit>("Fixture", current, field => field is "Level" or "Nature",
            picker: (field, values) =>
            {
                if (field == "Level") return Task.FromResult<(bool, object?)>((true, 70));
                seen = values;
                return Choose();
                async Task<(bool, object?)> Choose()
                {
                    var preview = NatureStatMath.Preview(new BaseStats(80, 82, 83, 100, 100, 80), (int)values["Level"]!,
                        (IReadOnlyList<int>)values["IVs"]!, [0, 0, 0, 0, 0, 0], 3, "Staged fixture");
                    var choice = await NaturePicker.ShowAsync(fixture.Host, Enumerable.Range(0, 25).Select(i => "Nature " + i).ToArray(), 3, preview);
                    return (true, choice?.Id);
                }
            }));
        fixture.Router.Dispatch(PadButton.Down); fixture.Router.Dispatch(PadButton.A);
        await WaitFor(() => fixture.Host.Children.Count == 1);
        fixture.Router.Dispatch(PadButton.Down); fixture.Router.Dispatch(PadButton.Down); fixture.Router.Dispatch(PadButton.A);
        await WaitFor(() => seen is not null); fixture.Draw();
        Assert.Equal(70, seen!["Level"]); Assert.Equal(50, current.Level);
        fixture.Router.Dispatch(PadButton.Down); fixture.Router.Dispatch(PadButton.B);
        await WaitFor(() => Find<Entry>(fixture.Host).Count() == 0 && fixture.Host.Children.Count == 1);
        fixture.Router.Dispatch(PadButton.A); await WaitFor(() => form.IsCompleted); var edited = await form;
        Assert.Equal(70, edited!.Level); Assert.Null(edited.Nature); Assert.Equal(3, current.Nature);
    }

    private static async Task WaitFor(Func<bool> condition)
    {
        for (var i = 0; i < 200; i++) { OnUi(() => { UiDispatcher.Instance.Pump(); return true; }); if (condition()) return; await Task.Delay(5); }
        Assert.Fail("Expected UI transition did not complete.");
    }
    private static T OnUi<T>(Func<T> action)
    {
        var context = SynchronizationContext.Current;
        try { SynchronizationContext.SetSynchronizationContext(UiDispatcher.Instance); return action(); }
        finally { SynchronizationContext.SetSynchronizationContext(context); }
    }
    private static IEnumerable<T> Find<T>(View view) where T : View
    {
        if (view is T result) yield return result;
        IEnumerable<View> children = view is Grid grid ? grid.Children : view is Border { Content: { } content } ? [content] : [];
        foreach (var child in children) foreach (var found in Find<T>(child)) yield return found;
    }
    private sealed class Fixture : IDisposable
    {
        private readonly IPlatformApplication? _previous = IPlatformApplication.Current;
        private readonly Func<string, string, Task<string?>>? _keyboard = Entry.RequestText;
        private readonly ServiceProvider _services;
        private readonly GraphicsDevice _device = GraphicsDevice.CreateHeadless(640, 480);
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "pkforge-picker-" + Guid.NewGuid().ToString("N"));
        private TimeSpan _time;
        public Grid Host { get; } = new();
        public GamepadRouter Router { get; } = new();
        public Fixture()
        {
            ParkPlatform.Configure(_directory, _directory);
            PKForge.App.App.Resume(); _services = new ServiceCollection().AddSingleton(Router).BuildServiceProvider();
            UiDispatcher.Instance.UseAnimationClock(() => _time);
            IPlatformApplication.Current = new PlatformApplication(_services); Draw();
        }
        public void Draw()
        {
            _time += TimeSpan.FromMilliseconds(200); UiDispatcher.Instance.Pump();
            Host.Draw(_device.CurrentSurface.Canvas, new(0, 0, 640, 480), _device);
        }
        public void Dispose()
        {
            Host.ReleaseSurface(); PKForge.App.App.Resume(); UiDispatcher.Instance.Pump();
            UiDispatcher.Instance.UseAnimationClock(() => System.Diagnostics.Stopwatch.GetElapsedTime(0));
            Entry.RequestText = _keyboard; IPlatformApplication.Current = _previous; _device.Dispose(); _services.Dispose();
            Directory.Delete(_directory, true);
        }
    }
}
