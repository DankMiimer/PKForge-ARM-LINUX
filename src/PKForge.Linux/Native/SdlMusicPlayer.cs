using System.Runtime.InteropServices;
using System.Text.Json;
using PKForge.Domain;

namespace PKForge.Linux.Native;

public sealed class SdlMusicPlayer : IMusicPlayer, IDisposable
{
    private const string Lib = "SDL2_mixer";
    private List<MusicTrack> _library;
    private IntPtr _music;
    private bool _ready;
    public string? LastError { get; private set; }
    public IReadOnlyList<MusicTrack> Library => _library;
    public bool IsPlaying => _ready && Mix_PlayingMusic() != 0 && Mix_PausedMusic() == 0;
    public bool IsOn { get; private set; }
    public int? CurrentIndex { get; private set; }
    public MusicOrder Order => Enum.TryParse<MusicOrder>(Preferences.Default.Get("music_order", "InOrder"), out var order) ? order : MusicOrder.InOrder;
    public MusicPlace Place { get; private set; }
    public bool Autostart => Preferences.Default.Get("music_autostart", false);
    public SdlMusicPlayer()
    {
        _library = Preferences.Default.Get("music_library", "").Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\u0001')).Where(parts => parts.Length == 2).Select(parts => new MusicTrack(parts[0], parts[1])).ToList();
    }
    public void Initialize()
    {
        try
        {
            var drivers = Enumerable.Range(0, Sdl.SDL_GetNumAudioDrivers())
                .Select(i => Marshal.PtrToStringUTF8(Sdl.SDL_GetAudioDriver(i))).OfType<string>().ToArray();
            Console.WriteLine("AUDIO available drivers: " + string.Join(", ", drivers));
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SDL_AUDIODRIVER")) && drivers.Contains("pulseaudio"))
            {
                var runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR") ?? "/var/run";
                if (File.Exists(Path.Combine(runtime, "pulse", "native")))
                {
                    Sdl.SDL_setenv("SDL_AUDIODRIVER", "pulseaudio", 1);
                    Sdl.SDL_setenv("PULSE_SERVER", "unix:" + Path.Combine(runtime, "pulse", "native"), 1);
                }
            }
            if (Sdl.SDL_InitSubSystem(Sdl.InitAudio) != 0) throw new InvalidOperationException(Sdl.Error);
            if (Mix_OpenAudio(44100, 0x8010, 2, 1024) != 0) throw new InvalidOperationException(Sdl.Error);
            _ready = true; Mix_VolumeMusic(48);
            PKForge.App.App.Suspended += Suspend;
            PKForge.App.App.Resumed += Resume;
            Console.WriteLine("AUDIO SDL2_mixer initialized.");
            if (Autostart) Play();
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        { LastError = error.Message; Console.Error.WriteLine("AUDIO " + LastError); }
    }
    public int Add(IReadOnlyList<PickedDocument> documents)
    {
        var before = _library.Count;
        foreach (var document in documents)
            if (File.Exists(document.DocumentId) && _library.All(t => t.DocumentId != document.DocumentId))
                _library.Add(new(document.DocumentId, Path.GetFileNameWithoutExtension(document.DisplayName)));
        SaveLibrary(); return _library.Count - before;
    }
    public void Remove(int index) { if ((uint)index < _library.Count) { Pause(); _library.RemoveAt(index); CurrentIndex = null; SaveLibrary(); } }
    public void Clear() { Pause(); _library.Clear(); CurrentIndex = null; SaveLibrary(); }
    private void SaveLibrary() => Preferences.Default.Set("music_library", string.Join('\n', _library.Select(t => t.DocumentId + "\u0001" + t.Title)));
    public void Play() { IsOn = true; if (_ready && Mix_PausedMusic() != 0 && CurrentIndex is not null) Mix_ResumeMusic(); else StartCurrent(); }
    public void Pause() { IsOn = false; if (_ready) Mix_PauseMusic(); }
    public void Skip() { CurrentIndex = Next(); if (IsOn) StartCurrent(); }
    public void SetOrder(MusicOrder order) => Preferences.Default.Set("music_order", order.ToString());
    public void SetAutostart(bool on) => Preferences.Default.Set("music_autostart", on);
    public MusicSource SourceFor(MusicPlace place) => MusicSource.Parse(Preferences.Default.Get("music_place_" + place, ""));
    public void SetSource(MusicPlace place, MusicSource source)
    {
        Preferences.Default.Set("music_place_" + place, source.Serialize());
        if (IsOn && (Place == place || place == MusicPlace.Menus && SourceFor(Place).Kind == MusicSourceKind.Default)) { CurrentIndex = null; StartCurrent(); }
    }
    public void EnterPlace(MusicPlace place)
    {
        if (Place == place) return;
        Place = place;
        var list = Playlist();
        if (CurrentIndex is { } current && list?.Contains(current) == true) return;
        CurrentIndex = null;
        if (IsOn) StartCurrent();
    }
    private int[]? Playlist() => MusicPlaces.Playlist(Place, SourceFor, _library);
    private int? Next()
    {
        var playlist = Playlist();
        if (playlist is null || playlist.Length == 0) return null;
        if (Order == MusicOrder.Shuffle) return playlist[Random.Shared.Next(playlist.Length)];
        var position = CurrentIndex is { } current ? Array.IndexOf(playlist, current) : -1;
        return playlist[(position + 1) % playlist.Length];
    }
    private void StartCurrent()
    {
        if (!_ready) return;
        var playlist = Playlist();
        if (playlist is null) { Mix_HaltMusic(); CurrentIndex = null; return; }
        if (CurrentIndex is null || !playlist.Contains(CurrentIndex.Value)) CurrentIndex = Next();
        if (CurrentIndex is not { } index) return;
        Mix_HaltMusic();
        if (_music != IntPtr.Zero) { Mix_FreeMusic(_music); _music = IntPtr.Zero; }
        _music = Mix_LoadMUS(_library[index].DocumentId);
        if (_music == IntPtr.Zero || Mix_PlayMusic(_music, 0) != 0)
        { LastError = Sdl.Error; IsOn = false; Console.Error.WriteLine("AUDIO " + LastError); }
        else Console.WriteLine("AUDIO playing " + _library[index].Title);
    }
    public void Tick() { if (_ready && IsOn && CurrentIndex is not null && Mix_PlayingMusic() == 0) Skip(); }
    private void Suspend() { if (_ready && IsOn) Mix_PauseMusic(); }
    private void Resume() { if (_ready && IsOn && Mix_PausedMusic() != 0) Mix_ResumeMusic(); }
    public void Dispose()
    {
        if (!_ready) return;
        PKForge.App.App.Suspended -= Suspend; PKForge.App.App.Resumed -= Resume;
        Mix_HaltMusic(); if (_music != IntPtr.Zero) Mix_FreeMusic(_music); _music = IntPtr.Zero;
        Mix_CloseAudio(); _ready = false;
    }
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int Mix_OpenAudio(int frequency, ushort format, int channels, int chunksize);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void Mix_CloseAudio();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern IntPtr Mix_LoadMUS([MarshalAs(UnmanagedType.LPUTF8Str)] string file);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void Mix_FreeMusic(IntPtr music);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int Mix_PlayMusic(IntPtr music, int loops);
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int Mix_HaltMusic();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int Mix_PlayingMusic();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int Mix_PausedMusic();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void Mix_PauseMusic();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern void Mix_ResumeMusic();
    [DllImport(Lib, CallingConvention = CallingConvention.Cdecl)] private static extern int Mix_VolumeMusic(int volume);
}
