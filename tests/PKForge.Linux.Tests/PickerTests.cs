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
    public void Fractional_picker_strokes_keep_native_pixel_row_extents_when_centered()
    {
        using var fixture = new Fixture();
        var list = new CollectionView
        {
            ItemsSource = Enumerable.Range(0, 20),
            ItemTemplate = new DataTemplate(() => new Border
            {
                StrokeThickness = 1.2, Margin = new Thickness(0, 0, 0, 3),
                Content = new BoxView { HeightRequest = 50 }
            })
        };
        fixture.Host.Children.Add(list); fixture.Draw();
        var rows = list.RetainedRows.ToArray();
        // Native reference: 50px content + fractional stroke -> 53px border,
        // then 3px margin. Every item keeps that extent rather than drifting.
        Assert.Equal(53, rows[0].Bounds.Height);
        Assert.Equal(56, rows[1].Bounds.Top - rows[0].Bounds.Top);
        list.ScrollTo(5, ScrollToPosition.Center); fixture.Draw();
        var centered = list.RetainedRows.Single(row => Equals(row.BindingContext, 5));
        Assert.Equal(212, centered.Bounds.Top);
        Assert.InRange(list.RetainedRowCount, 1, 10);
    }

    [Fact]
    public async Task Type_pickers_preserve_stellar_and_unavailable_hidden_power_choices()
    {
        using var fixture = new Fixture();
        var tera = InfoPickers.ShowTeraAsync(fixture.Host, [new(10, "Water"), new(99, "Stellar")], 99, [10]);
        fixture.Draw(); fixture.Router.Dispatch(PadButton.A);
        Assert.Equal(99, (await tera)!.Id);
        var hidden = InfoPickers.ShowHiddenPowerAsync(fixture.Host, 1, type => type == 1 ? null : [31, 31, 31, 31, 31, 31]);
        fixture.Draw(); fixture.Router.Dispatch(PadButton.A);
        var unavailable = (await hidden)!;
        Assert.Equal(1, unavailable.Id); Assert.True(unavailable.Muted);
        Assert.Contains("Not reachable", unavailable.Detail);
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
        fixture.Draw(); fixture.Router.Dispatch(PadButton.A);
        var row = (await picker)!;
        Assert.Equal(33, row.Id); Assert.Equal(0, row.TypeId); Assert.Equal(MoveCategory.Physical, row.Category);
        Assert.Contains("15", row.Tag); Assert.Contains("40", row.Detail);
        picker = InfoPickers.ShowMovesAsync(fixture.Host, "Moves", new GameDataService(), moves, 33);
        fixture.Draw(); fixture.Router.Dispatch(PadButton.Y); fixture.Router.Dispatch(PadButton.Down); fixture.Router.Dispatch(PadButton.A);
        Assert.Equal(55, (await picker)!.Id); Assert.Equal(before, moves);
    }

    [Fact]
    public async Task Held_item_snapshot_preserves_the_games_ids_and_current_nonholdable_item()
    {
        using var fixture = new Fixture();
        string[] names = ["", "Potion", "Master Ball", "Oran Berry"];
        var picker = InfoPickers.ShowHeldItemsAsync(fixture.Host, "Held item", names, new HashSet<int> { 3 }, 2, _ => null);
        fixture.Draw(); fixture.Router.Dispatch(PadButton.A);
        Assert.Equal(2, (await picker)!.Id);
        picker = InfoPickers.ShowHeldItemsAsync(fixture.Host, "Held item", names, new HashSet<int> { 3 }, 2, _ => null);
        fixture.Draw(); fixture.Router.Dispatch(PadButton.Y); fixture.Router.Dispatch(PadButton.Down); fixture.Router.Dispatch(PadButton.A);
        Assert.Equal(3, (await picker)!.Id); Assert.Equal("Oran Berry", names[3]);
    }

    [Fact]
    public async Task Long_painted_picker_search_preserves_original_ids_and_releases_input()
    {
        using var fixture = new Fixture();
        Entry.RequestText = (_, _) => Task.FromResult<string?>("1199");
        var picker = new NativeDialogs(fixture.Host, fixture.Router).PickAsync("Fixture",
            Enumerable.Range(0, 1200).Select(i => $"Entry {i:D4}").ToArray(), 1100);
        fixture.Draw(); Assert.Single(Find<SkiaSharp.Views.Maui.Controls.SKCanvasView>(fixture.Host));
        Assert.Empty(Find<CollectionView>(fixture.Host));
        fixture.Router.Dispatch(PadButton.Select); fixture.Draw(); fixture.Router.Dispatch(PadButton.A);
        Assert.Equal(1199, await picker); Assert.Empty(fixture.Host.Children);
        Assert.False(fixture.Router.Dispatch(PadButton.A));
    }

    [Fact]
    public async Task Empty_search_cannot_choose_a_stale_selection()
    {
        using var fixture = new Fixture();
        Entry.RequestText = (_, _) => Task.FromResult<string?>("absent");
        var picker = PickerMenu.ShowAsync(fixture.Host, "Filter", [new(1, "Legal"), new(8, "Other") { Muted = true }], 8,
            filter: new("Legal only", "Show all", item => !item.Muted));
        fixture.Draw(); fixture.Router.Dispatch(PadButton.Y); fixture.Router.Dispatch(PadButton.Select); fixture.Draw();
        fixture.Router.Dispatch(PadButton.A); Assert.False(picker.IsCompleted);
        fixture.Router.Dispatch(PadButton.B); Assert.Null(await picker);
    }

    [Fact]
    public async Task Touch_preview_requires_a_second_tap_and_closes_without_lingering_work()
    {
        using var fixture = new Fixture(); var highlighted = new List<int>();
        var picker = PickerMenu.ShowAsync(fixture.Host, "Preview", Enumerable.Range(0, 100).Select(i => new PickItem(i, $"Item {i}")).ToArray(),
            preview: new(new Label { Text = "Preview", HeightRequest = 20 }, item => { if (item is not null) highlighted.Add(item.Id); }));
        fixture.Draw();
        var canvas = Find<SkiaSharp.Views.Maui.Controls.SKCanvasView>(fixture.Host).Single();
        var info = new SKImageInfo((int)canvas.CanvasSize.Width, (int)canvas.CanvasSize.Height);
        using var surface = SKSurface.Create(info);
        var scene = new PKForge.Chrome.PickerScene("Preview", "", 100, i => new("Item " + i), 0, 0, [], "", [], false, Preview: true);
        var hits = PKForge.Chrome.PickerPaint.Paint(surface.Canvas, info, scene, PixelFont.Face);
        var row = hits.Rows[1]; var point = new SKPoint(canvas.Bounds.Left + row.Rect.MidX, canvas.Bounds.Top + row.Rect.MidY);
        fixture.Host.HandleTouch(new(1, SKTouchAction.Pressed, point)); fixture.Host.HandleTouch(new(1, SKTouchAction.Released, point));
        Assert.False(picker.IsCompleted); Assert.Contains(row.Index, highlighted);
        fixture.Draw(); fixture.Host.HandleTouch(new(1, SKTouchAction.Pressed, point)); fixture.Host.HandleTouch(new(1, SKTouchAction.Released, point));
        Assert.Equal(row.Index, (await picker)!.Id); Assert.Empty(fixture.Host.Children);
        Assert.Equal(0, UiDispatcher.Instance.ActiveTimerCount);
    }

    [Fact]
    public async Task Suspend_cancels_picker_and_ignores_late_keyboard_result()
    {
        using var fixture = new Fixture(); var result = new TaskCompletionSource<string?>();
        Entry.RequestText = (_, _) => result.Task;
        var picker = PickerMenu.ShowAsync(fixture.Host, "Search", [new(1, "One")]);
        fixture.Draw(); fixture.Router.Dispatch(PadButton.Select);
        PKForge.App.App.Suspend(); Assert.Null(await picker); Assert.Empty(fixture.Host.Children);
        result.SetResult("late"); await Task.Delay(20); UiDispatcher.Instance.Pump();
        Assert.Empty(fixture.Host.Children); Assert.False(fixture.Router.Dispatch(PadButton.A));
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
            ParkPlatform.Configure(_directory, Path.Combine(AppContext.BaseDirectory, "assets"));
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
