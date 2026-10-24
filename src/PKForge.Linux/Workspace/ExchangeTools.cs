using System.Text;
using PKForge.Application;
using PKForge.Domain;
using PKForge.Engine;
using PKForge.App.Services;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using ZXing;
using ZXing.Common;

namespace PKForge.Linux.Workspace;

public sealed partial class WorkspaceHost
{
    private async Task SharePokemonAsync(PokemonTarget target)
    {
        var action = await Dialogs.MenuAsync("Share Pokémon", "Exports are local files. PKForge QR images carry exact Pokémon bytes.", "Showdown text", "QR image", "Showdown QR", "Evolution information");
        if (action == "Showdown text") await ExportAsync(Encoding.UTF8.GetBytes(Pokemon.Read(target, (s, b, i) => s.GetShowdownText(b, i))), "pokemon-showdown.txt");
        if (action == "Evolution information")
        {
            var plan = Pokemon.Read(target, (s, b, i) => Get<IEvolutionService>().Plan(s, b, i, HaXMode.IsOn));
            var options = plan.Options;
            var index = await Dialogs.PickAsync("Evolution", options.Select(NativeForms.Display).ToArray()); if (index is null) return;
            var request = (EvolutionRequest?)await Forms.RecordAsync("Evolution request", typeof(EvolutionRequest), new EvolutionRequest(options[index.Value].Id, Hax: HaXMode.IsOn));
            if (request is not null) await EditMonAsync(target, (s, b, i) => Get<IEvolutionService>().Evolve(s, b, i, request), "Evolve Pokémon");
        }
        if (action == "Showdown QR")
        {
            var textPng = NativeQr.EncodeText(Pokemon.Read(target, (s, b, i) => s.GetShowdownText(b, i)));
            var textPath = Path.Combine(ParkPlatform.DataDirectory, "exports", "pokemon-showdown-qr.png");
            await DurableFiles.WriteAsync(textPath, textPng);
            Screens.Push(new NativeImageScreen(Screens, "Showdown QR · B: back", textPng, textPath)); return;
        }
        if (action != "QR image") return;
        var export = Pokemon.Read(target, (s, b, i) => s.ExportSlot(b, i));
        var generation = Pokemon.Read(target, (s, _, _) => s.Generation);
        var payload = QrEntityCodec.MakePayload(export.Data, generation, Pokemon.Read(target, (s, b, i) => s.ReadEntity(b, i).SpeciesName));
        var png = NativeQr.Encode(payload);
        var path = Path.Combine(ParkPlatform.DataDirectory, "exports", "pokemon-qr.png"); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await DurableFiles.WriteAsync(path, png);
        Screens.Push(new NativeImageScreen(Screens, "Pokémon QR · B: back", png, path));
    }
    private async Task ImportExchangeAsync(bool bank, int box)
    {
        var mode = await Dialogs.MenuAsync("Receive Pokémon", "Select a QR image, type Showdown text, or import a UTF-8 team file.", "QR image", "Showdown text", "Showdown file");
        if (mode is null) return;
        Get<MutationPolicy>().Demand(SaveAction.CreateMon);
        string? text = null;
        if (mode == "QR image")
        {
            var file = (await Files.PickAsync(new(FilePurpose.Image, "Pokémon QR image"))).FirstOrDefault(); if (file is null) return;
            var imageBytes = await File.ReadAllBytesAsync(file.DocumentId);
            var payload = NativeQr.Decode(imageBytes);
            var parsed = payload is null ? null : QrEntityCodec.TryParse(payload);
            if (parsed is null) text = NativeQr.DecodeText(imageBytes) ?? throw new InvalidDataException("No QR was found in this image.");
            else
            {
            var candidates = QrImportWorkflow.DescribeCandidates(Get<ISaveEngine>(), parsed);
            if (candidates.Count == 0) throw new InvalidDataException("The QR's Pokémon could not be read in its recorded generation.");
            var formatIndex = candidates.Count == 1 ? 0 : await Dialogs.PickAsync("QR source format · choose the original game", candidates.Select(c => $"{c.Format} · {c.Nickname} · {c.Species}").ToArray());
            if (formatIndex is null) return;
            var info = candidates[formatIndex.Value];
            if (!await Dialogs.ConfirmAsync("QR preview", NativeForms.Describe(info), "Receive")) return;
            if (bank) Bank.Add(parsed.EntityBytes, info);
            else
            {
                var empty = ShowdownTeamService.EmptySlots(Session, Math.Max(0, box), false);
                if (empty.Count == 0) throw new InvalidOperationException("The PC is full.");
                var at = empty[0];
                await CommitAsync(s => new(BankTransfers.TryImport(s, at.Box, at.Slot, parsed.EntityBytes, out _, info.Format), "QR received."), "Receive Pokémon QR", SaveAction.CreateMon, WriteScope.Only(at));
            }
            return;
            }
        }
        if (mode == "Showdown file")
        {
            var file = (await Files.PickAsync(new(FilePurpose.Text, "Showdown team"))).FirstOrDefault(); text = file is null ? null : await File.ReadAllTextAsync(file.DocumentId, Encoding.UTF8);
        }
        else if (mode == "Showdown text") text = await Dialogs.TextAsync("Showdown text", "");
        if (string.IsNullOrWhiteSpace(text)) return;
        using var blank = Sessions.CurrentSession is null && bank ? await BankContextAsync() : null;
        var context = Sessions.CurrentSession ?? blank; if (context is null) return;
        var previews = await Task.Run(() => ShowdownTeamService.Preview(context, Get<ILegalizerService>(), text, HaXMode.IsOn));
        List<ShowdownSetPreview> selected = [];
        foreach (var preview in previews)
            if (await Dialogs.ConfirmAsync("Showdown set", NativeForms.Describe(preview), "Include")) selected.Add(preview);
        if (selected.Count == 0) return;
        if (bank)
        {
            List<BankDeposit> deposits = [];
            foreach (var set in selected)
            {
                var generated = await Task.Run(() => Get<ILegalizerService>().GenerateDataFromShowdown(context, set.Text, HaXMode.IsOn)) ?? throw new InvalidOperationException("Could not generate " + set.Title);
                deposits.Add(new(generated.Data, generated.Info));
            }
            Bank.AddMany(deposits);
        }
        else
        {
            var slots = ShowdownTeamService.EmptySlots(context, box, false).Take(selected.Count).ToArray();
            if (slots.Length != selected.Count) throw new InvalidOperationException("There are not enough empty slots.");
            await CommitAsync(s => ShowdownTeamService.Place(s, Get<ILegalizerService>(), selected, slots, HaXMode.IsOn), "Import Showdown team", SaveAction.CreateMon, new(slots));
        }
    }
}

public static class NativeQr
{
    public static byte[] Encode(byte[] payload) => EncodeCore(Encoding.Latin1.GetString(payload), "ISO-8859-1");
    public static byte[] EncodeText(string payload) => EncodeCore(payload, "UTF-8");
    private static byte[] EncodeCore(string payload, string characterSet)
    {
        var matrix = new MultiFormatWriter().encode(payload, BarcodeFormat.QR_CODE, 480, 480,
            new Dictionary<EncodeHintType, object> { [EncodeHintType.CHARACTER_SET] = characterSet, [EncodeHintType.ERROR_CORRECTION] = ZXing.QrCode.Internal.ErrorCorrectionLevel.M, [EncodeHintType.MARGIN] = 4 });
        using var bitmap = new SKBitmap(matrix.Width, matrix.Height);
        for (var y = 0; y < matrix.Height; y++) for (var x = 0; x < matrix.Width; x++) bitmap.SetPixel(x, y, matrix[x, y] ? SKColors.Black : SKColors.White);
        using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100); return data.ToArray();
    }
    public static byte[]? Decode(byte[] image) => DecodeText(image) is { } text ? Encoding.Latin1.GetBytes(text) : null;
    public static string? DecodeText(byte[] image)
    {
        using var source = SKBitmap.Decode(image); if (source is null) return null;
        var scale = Math.Min(1f, 1600f / Math.Max(source.Width, source.Height));
        using var surface = SKSurface.Create(new SKImageInfo(Math.Max(1, (int)(source.Width * scale)), Math.Max(1, (int)(source.Height * scale)), SKColorType.Rgba8888, SKAlphaType.Opaque));
        surface.Canvas.Clear(SKColors.White); surface.Canvas.Scale(scale); surface.Canvas.DrawBitmap(source, 0, 0);
        using var pixels = surface.PeekPixels(); if (pixels is null) return null;
        var reader = new BarcodeReaderGeneric { AutoRotate = true, Options = new DecodingOptions { TryHarder = true, PossibleFormats = [BarcodeFormat.QR_CODE], CharacterSet = "ISO-8859-1" } };
        var result = reader.Decode(new RGBLuminanceSource(pixels.GetPixelSpan().ToArray(), pixels.Width, pixels.Height, RGBLuminanceSource.BitmapFormat.RGBA32));
        return result?.Text;
    }
}

internal sealed class NativeImageScreen(NativeScreenStack stack, string title, byte[] png, string path) : INativeScreen
{
    private SKBitmap? _bitmap;
    public void Enter() => _bitmap = SKBitmap.Decode(png);
    public void Leave() { _bitmap?.Dispose(); _bitmap = null; }
    public void Paint(SKCanvas c, SKRect bounds, bool secondary)
    {
        NativePaint.Background(c, bounds, title);
        if (secondary) { NativePaint.Paragraph(c, bounds, "Saved locally:\n" + path); return; }
        if (_bitmap is null) return;
        var size = Math.Min(bounds.Width, bounds.Height - 58); c.DrawBitmap(_bitmap, SKRect.Create((bounds.Width - size) / 2, 54, size, size));
    }
    public bool Button(PadButton button) { if (button is not (PadButton.A or PadButton.B)) return true; stack.Pop(); return true; }
    public void Touch(SKTouchEventArgs touch, bool secondary) { if (touch.ActionType == SKTouchAction.Released) stack.Pop(); }
}
