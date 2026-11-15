using PKForge.Chrome;
using PKForge.Domain;
using PKForge.Linux.Workspace;
using SkiaSharp;
using Xunit;

namespace PKForge.Linux.Tests;

/// <summary>Rough edges seen in the handheld screenshots of 15 November 2026.</summary>
public sealed class UiConsistencyTests
{
    // The faces load in the background; measuring while one arrives would measure another font.
    public UiConsistencyTests() => Task.WhenAll(PKForge.App.Views.PixelFont.WarmAsync(), PKForge.App.Views.PixelFont.WarmFallbackAsync()).GetAwaiter().GetResult();

    private static readonly (string Key, string Label)[] ParkPrompts =
        [("B", "Save / Bank"), ("A", "Meet Pokémon"), ("LR", "Places"), ("↑↓", "Zoom"), ("R2+✚", "Pan"), ("X", "Photo"), ("+", "Options"), ("−", "Spectate")];

    [Theory]
    [InlineData(640)]   // RG DS lower screen
    [InlineData(720)]   // RG34XXSP
    public void The_parks_eight_prompts_fit_inside_the_bar(int width)
    {
        var bar = SKRect.Create(0, 426, width, 54);
        using var font = AppChromePaint.HintFont(bar, ParkPrompts, PKForge.App.Views.PixelFont.Face, 14);
        var hits = PksmPaint.HintBarHitRects(bar, ParkPrompts, font);
        Assert.True(hits[0].Left >= bar.Left, $"first prompt starts at {hits[0].Left}");
        Assert.True(hits[^1].Right <= bar.Right, $"last prompt ends at {hits[^1].Right} of {bar.Right}");
        Assert.True(font.Size > 9, $"font shrank to {font.Size}");
    }

    [Fact]
    public void A_short_bar_keeps_its_font_size()
    {
        var bar = SKRect.Create(0, 438, 640, 42);
        using var font = AppChromePaint.HintFont(bar, [("A", "Open"), ("B", "Back")], PKForge.App.Views.PixelFont.Face, 14);
        Assert.Equal(14, font.Size);
    }

    [Theory]
    [InlineData("Bulbasaur", "", true, "Bulbasaur · Owned")]
    [InlineData("Bulbasaur", "", false, "Bulbasaur · Missing")]
    [InlineData("Rotom", "Wash Rotom", true, "Rotom · Wash Rotom · Owned")]
    public void A_collection_caption_has_no_empty_part(string name, string form, bool owned, string expected) =>
        Assert.Equal(expected, CollectionScreen.CollectionCaption(name, form, owned));

    [Fact]
    public void A_cartridge_drops_the_year_of_this_years_save_before_cutting_text()
    {
        var now = new DateTime(2026, 11, 15);
        var save = new DetectedSave("doc", "ruby.sav", "Pokémon Ruby", EmulatorKind.RetroArch, false, new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero));
        var choices = HomeCardText.DetailChoices([save], now);
        Assert.Equal(3, choices.Count);
        Assert.StartsWith("RetroArch · ", choices[0]);
        Assert.Contains("2026", choices[0]);
        Assert.StartsWith("RetroArch · ", choices[1]);
        Assert.DoesNotContain("2026", choices[1]);
        Assert.DoesNotContain("RetroArch", choices[2]);
        Assert.Equal(choices[0], HomeCardText.Detail([save]));

        var older = save with { LastModified = new DateTimeOffset(2025, 3, 1, 12, 0, 0, TimeSpan.Zero) };
        Assert.All(HomeCardText.DetailChoices([older], now), choice => Assert.Contains("2025", choice));
    }

    [Theory]
    [InlineData(.5161f)]   // the old width-only unit on the RG DS: the journal ran past its frame
    [InlineData(.4930f)]   // the unit the journal now uses on 640×424
    public void The_journal_draws_nothing_below_its_area(float unit)
    {
        var area = new SKRect(0, 0, 640, 424);
        using var bitmap = new SKBitmap(new SKImageInfo(640, 480, SKColorType.Rgba8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            var card = new ParkCardPaint.Card("Bulbasaur", false, "Curious explorer", "Curious", "Ralts is coming over", "new places and battling",
                [3, 4, 3, 3], 2);
            var journal = new ParkCardPaint.Journal(card, "loves ??? · dislikes ???", "Met Charmander by the pond and stayed until dark, talking about everything",
                ["Ralts found a berry", "Charmander took a nap", "It started to rain"], "best friend Charmander");
            ParkCardPaint.PaintJournal(canvas, area, journal, null, PKForge.App.Views.PixelFont.Face, unit);
        }
        for (var y = (int)area.Bottom; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
                Assert.True(bitmap.GetPixel(x, y).Alpha == 0, $"drawn at {x},{y}");
    }
}
