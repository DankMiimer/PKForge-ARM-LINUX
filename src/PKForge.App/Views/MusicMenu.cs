using PKForge.Domain;

namespace PKForge.App.Views;

/// <summary>
/// Background music, the same menu from Home and from the Poképark's options: play/pause, skip,
/// the music of each place (the menus, the park by day, the park by night), the library, order,
/// autostart.
/// </summary>
public static class MusicMenu
{
    private static readonly (MusicPlace Place, string Name, string Detail)[] Places =
    [
        (MusicPlace.Menus, "Menus", "Plays everywhere in PKForge outside the Poképark."),
        (MusicPlace.ParkDay, "Park by day", "Plays in the Poképark in the morning and the afternoon."),
        (MusicPlace.ParkNight, "Park by night", "Plays in the Poképark in the evening and at night."),
    ];

    public static async Task ShowAsync(Grid host, Action<string> status)
    {
        var music = IPlatformApplication.Current?.Services.GetService<IMusicPlayer>();
        if (music is null) return;
        var start = 0;

        while (true)
        {
            var order = music.Order == MusicOrder.Shuffle ? "Shuffle" : "In order";
            var auto = music.Autostart ? "ON" : "OFF";
            var message = music.IsPlaying && music.CurrentIndex is { } index && index < music.Library.Count
                ? $"Playing: {music.Library[index].Title}"
                : music.IsOn && music.Library.Count > 0 ? $"Silence here ({PlaceName(music.Place)})."
                : $"Library: {music.Library.Count} track(s)";
#if ANDROID
            if (music is Platforms.Android.MusicPlayer { LastError: { } error })
                message += $"\nLast error: {error}";
#elif LINUX
            if (music is PKForge.Linux.Native.SdlMusicPlayer { LastError: { } error })
                message += $"\nLast error: {error}";
#endif
            PadOption[] options =
            [
                new(music.IsOn ? "Pause" : "Play", IconPath: music.IsOn ? "pause" : "play"),
                new("Skip to next track", IconPath: "skip"),
                .. Places.Select(p => new PadOption($"{p.Name}: {MusicPlaces.Describe(p.Place, music.SourceFor(p.Place), music.Library)}",
                    IconPath: p.Place == MusicPlace.Menus ? "music" : "park", Detail: p.Detail)),
                new($"Add music files ({music.Library.Count})", IconPath: "folder", Detail: "Pick audio files on your device to add to the library."),
                new(music.Library.Count > 0 ? "Clear library" : "-", IconPath: "delete",
                    Detail: music.Library.Count > 0 ? "Empties the list. Your audio files stay on the device." : null),
                new($"Order: {order}", IconPath: "shuffle", Detail: "Play the tracks in order or shuffled."),
                new($"Autostart: {auto}", IconPath: "settings", Detail: "ON starts the music when PKForge opens."),
            ];
            var choice = await PadMenu.ShowAtAsync(host, "Background music", message, start, options);
            start = Math.Max(0, Array.FindIndex(options, o => o.Label == choice));
            if (Places.FirstOrDefault(p => choice?.StartsWith(p.Name + ": ", StringComparison.Ordinal) == true) is { Name: not null } place)
            {
                await PickAsync(host, music, place.Place, status);
                continue;
            }
            switch (choice)
            {
                case "Play": music.Play(); break;
                case "Pause": music.Pause(); break;
                case "Skip to next track": music.Skip(); break;
                case var add when add?.StartsWith("Add music files", StringComparison.Ordinal) == true:
                {
#if LINUX
                    var dialogs = IPlatformApplication.Current?.Services.GetService<PKForge.Application.IFileDialogService>();
                    if (dialogs is null) break;
                    var documents = await dialogs.PickAsync(new(PKForge.Application.FilePurpose.Music, "Add music files", true));
#else
                    var picker = IPlatformApplication.Current?.Services.GetService<IDocumentPicker>();
                    if (picker is null) break;
                    var documents = await picker.PickManyAsync();
#endif
                    if (documents.Count == 0) break;
                    var added = music.Add(documents);
                    status(added > 0 ? $"Added {added} track(s) to the music library." : "Those files are already in the library.");
                    break;
                }
                case "Clear library":
                    var confirmed = await PadMenu.ConfirmAsync(host, "Clear music library?",
                        "Removes every track. Your audio files on storage are untouched.", "Clear");
                    if (confirmed) music.Clear();
                    break;
                case var o when o?.StartsWith("Order: ", StringComparison.Ordinal) == true:
                    music.SetOrder(music.Order == MusicOrder.InOrder ? MusicOrder.Shuffle : MusicOrder.InOrder);
                    break;
                case var a when a?.StartsWith("Autostart: ", StringComparison.Ordinal) == true:
                    music.SetAutostart(!music.Autostart);
                    break;
                default:
                    return;
            }
        }
    }

    private static string PlaceName(MusicPlace place) => Places.First(p => p.Place == place).Name.ToLowerInvariant();

    /// <summary>
    /// A place's music: the default (the whole library for the menus, the menus' music for the
    /// park), every track, silence, or the tracks ticked in the list. Ticking a track keeps the
    /// menu open on it, so several can be ticked in a row; B goes back.
    /// </summary>
    private static async Task PickAsync(Grid host, IMusicPlayer music, MusicPlace place, Action<string> status)
    {
        var (_, name, detail) = Places.First(p => p.Place == place);
        var start = 0;
        while (true)
        {
            var source = music.SourceFor(place);
            var library = music.Library;
            var titles = UniqueTitles(library);
            var fallback = place == MusicPlace.Menus ? "All tracks" : "Same as menus";
            var ticked = source.Kind == MusicSourceKind.Tracks ? source.Tracks.ToHashSet() : [];
            PadOption Option(string label, bool on, string icon, string? optionDetail = null) =>
                new(label, IconPath: on ? "check" : icon, Detail: optionDetail);
            var options = new List<PadOption>
            {
                Option(fallback, source.Kind == MusicSourceKind.Default || (source.Kind == MusicSourceKind.AllTracks && place == MusicPlace.Menus), "music",
                    place == MusicPlace.Menus ? "The whole library." : "Keeps the menus' music playing, without a cut."),
            };
            if (place != MusicPlace.Menus)
                options.Add(Option("All tracks", source.Kind == MusicSourceKind.AllTracks, "music", "The whole library."));
            options.Add(Option("Silence", source.Kind == MusicSourceKind.Silence, "close", "No music here."));
            for (var i = 0; i < library.Count; i++)
                options.Add(Option(titles[i], ticked.Contains(library[i].DocumentId), "music"));
            var message = library.Count == 0
                ? $"{detail} Add music files first to pick tracks."
                : $"{detail} Tick one or more tracks.";
            var choice = await PadMenu.ShowAtAsync(host, $"Music: {name}", message, start, options.ToArray());
            if (choice is null) return;
            start = Math.Max(0, options.FindIndex(o => o.Label == choice));
            var track = Array.IndexOf(titles, choice);
            if (track >= 0)
            {
                var id = library[track].DocumentId;
                music.SetSource(place, MusicSource.Of(ticked.Contains(id) ? ticked.Where(t => t != id) : ticked.Append(id)));
                continue;
            }
            music.SetSource(place, choice switch
            {
                "Silence" => MusicSource.Silence,
                "All tracks" when place != MusicPlace.Menus => MusicSource.AllTracks,
                _ => MusicSource.Default,
            });
            status($"{name}: {MusicPlaces.Describe(place, music.SourceFor(place), library)}.");
            return;
        }
    }

    // Menu entries are told apart by their label: two files with one title get a number.
    private static string[] UniqueTitles(IReadOnlyList<MusicTrack> library)
    {
        var seen = new Dictionary<string, int>();
        var titles = new string[library.Count];
        for (var i = 0; i < library.Count; i++)
        {
            var title = library[i].Title;
            seen[title] = seen.GetValueOrDefault(title) + 1;
            titles[i] = seen[title] == 1 && title is not ("All tracks" or "Same as menus" or "Silence") ? title : $"{title} ({seen[title]})";
        }
        return titles;
    }
}
