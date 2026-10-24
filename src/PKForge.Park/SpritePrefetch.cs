using System.Text;
using System.Xml.Linq;
using PKForge.Domain;

namespace PKForge.App.Services;

public sealed partial class PokeparkSpriteService
{
    /// <summary>Downloads the current roster's poses to disk without decoding every sheet into RAM.</summary>
    public async Task PrefetchAsync(IEnumerable<ParkPokemon> residents, IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var roster = residents.DistinctBy(p => (p.Species, p.Form, p.Shiny)).ToArray();
        var errors = new List<string>();
        for (var i = 0; i < roster.Length; i++)
        {
            var mon = roster[i];
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"{i + 1}/{roster.Length}: {mon.Name}");
            var key = Key(mon.Species, mon.Form, mon.Shiny);
            if (key is null) continue;
            await _network.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var directory = CacheFolder(key);
                Directory.CreateDirectory(directory);
                var xml = await ReadAsync("sprite/" + key, directory, "AnimData.xml", 256 * 1024).ConfigureAwait(false);
                await ReadAsync("sprite/" + key, directory, "credits.txt", 256 * 1024).ConfigureAwait(false);
                var names = XDocument.Parse(Encoding.UTF8.GetString(xml)).Descendants("Anim")
                    .Where(a => a.Element("CopyOf") is null).Select(a => (string?)a.Element("Name"))
                    .Where(n => n is { Length: > 0 } && n.All(char.IsAsciiLetterOrDigit)).Distinct().ToArray();
                foreach (var name in names)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report($"{i + 1}/{roster.Length}: {mon.Name} · {name}");
                    try
                    {
                        await ReadAsync("sprite/" + key, directory, name + "-Anim.png", 4 * 1024 * 1024).ConfigureAwait(false);
                        await ReadAsync("sprite/" + key, directory, name + "-Shadow.png", 1024 * 1024).ConfigureAwait(false);
                    }
                    catch (HttpRequestException error) when (error.StatusCode == System.Net.HttpStatusCode.NotFound) { }
                }
                if (ParkSprites.PortraitKey(mon.Species, mon.Form) is { } portraitKey)
                {
                    var portraits = Path.Combine(CacheFolder(portraitKey), "portrait");
                    Directory.CreateDirectory(portraits);
                    foreach (var emotion in new[] { "Normal", "Happy", "Joyous", "Sad", "Angry", "Surprised", "Sigh", "Worried", "Determined", "Pain", "Shouting", "Teary-Eyed", "Inspired", "Dizzy", "Special0", "Special1" })
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        try { await ReadAsync("portrait/" + portraitKey, portraits, emotion + ".png", 64 * 1024).ConfigureAwait(false); }
                        catch (HttpRequestException error) when (error.StatusCode == System.Net.HttpStatusCode.NotFound) { }
                    }
                }
            }
            catch (Exception error) when (IsDownloadFailure(error)) { errors.Add(mon.Name + ": " + error.Message); }
            finally { _network.Release(); }
        }
        if (errors.Count > 0) throw new IOException(string.Join("\n", errors));
        progress?.Report("Park assets are cached for offline use.");
    }
}
