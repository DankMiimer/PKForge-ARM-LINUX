using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using PKForge.App.Services;
using PKForge.Domain;

namespace PKForge.Linux.Native;

internal sealed class InputRouter : IDisposable
{
    private readonly GamepadRouter _router;
    private readonly Action _quit;
    private readonly List<IntPtr> _controllers = [];
    private readonly Dictionary<PadButton, (long Since, long Last)> _held = [];
    private readonly HashSet<PadButton> _repeatBlocked = [];
    private bool _needsNeutral;
    public InputRouter(GamepadRouter router, Action quit)
    {
        _router = router;
        _quit = quit;
        OpenControllers();
    }
    public static void PrepareMappings()
    {
        static string[] Read(string? path)
        {
            try { return !string.IsNullOrEmpty(path) && File.Exists(path) ? File.ReadAllLines(path) : []; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            { Console.Error.WriteLine("INPUT mapping file: " + error.Message); return []; }
        }
        var inherited = Read(Environment.GetEnvironmentVariable("SDL_GAMECONTROLLERCONFIG_FILE"))
            .Concat((Environment.GetEnvironmentVariable("SDL_GAMECONTROLLERCONFIG") ?? "").Split('\n'));
        var frontend = new Lazy<XElement[]>(() => FrontendInputs().ToArray());
        var mappings = ControllerMappings.Prepare(Firmware(), Environment.GetEnvironmentVariable("PKFORGE_CONTROLLER_LAYOUT"),
            inherited, Read(Path.Combine(ParkPlatform.DataDirectory, "gamecontrollerdb.txt")),
            (guid, name) => EmulationStationMapping(guid, name, frontend.Value));
        if (mappings is not null)
        {
            if (Sdl.SDL_SetHintWithPriority("SDL_GAMECONTROLLERCONFIG", mappings, 2) == 0)
                Console.Error.WriteLine("INPUT could not apply controller mapping hint");
            else Console.WriteLine("INPUT prepared frontend labels and app controller overrides before SDL init");
        }
    }
    private void OpenControllers()
    {
        foreach (var controller in _controllers) Sdl.SDL_GameControllerClose(controller);
        _controllers.Clear(); ClearHeld();
        for (var i = 0; i < Sdl.SDL_NumJoysticks(); i++)
        {
            if (Sdl.SDL_IsGameController(i) == 0) ImportEmulationStationMapping(i);
            if (Sdl.SDL_IsGameController(i) == 0) ImportNextUiMapping(i);
            if (Sdl.SDL_IsGameController(i) == 0) { Console.WriteLine($"INPUT joystick {i}: no SDL controller mapping"); continue; }
            var controller = Sdl.SDL_GameControllerOpen(i);
            if (controller == IntPtr.Zero) continue;
            _controllers.Add(controller);
            Console.WriteLine("INPUT controller: " + Marshal.PtrToStringUTF8(Sdl.SDL_GameControllerName(controller)));
            Console.WriteLine("INPUT controller mapping: " + ReadMapping(controller));
        }
    }
    private static void ImportEmulationStationMapping(int index)
    {
        if (EmulationStationMapping(index) is not { } mapping) return;
        if (Sdl.SDL_GameControllerAddMapping(mapping) >= 0) Console.WriteLine("INPUT imported EmulationStation mapping");
    }
    private static string? Firmware()
    {
        var name = Environment.GetEnvironmentVariable("CFW_NAME");
        if (!string.IsNullOrEmpty(name)) return name;
        try
        {
            if (File.Exists("/etc/os-release") && File.ReadLines("/etc/os-release")
                .Any(line => line is "OS_NAME=knulli" or "OS_NAME=\"knulli\"")) return "knulli";
        }
        catch (IOException) { }
        return null;
    }
    private static string? ReadMapping(IntPtr controller)
    {
        var value = Sdl.SDL_GameControllerMapping(controller);
        try { return Marshal.PtrToStringUTF8(value); }
        finally { if (value != IntPtr.Zero) Sdl.SDL_free(value); }
    }
    private static string? EmulationStationMapping(int index)
    {
        var name = Marshal.PtrToStringUTF8(Sdl.SDL_JoystickNameForIndex(index));
        var buffer = new byte[33]; Sdl.SDL_JoystickGetGUIDString(Sdl.SDL_JoystickGetDeviceGUID(index), buffer, buffer.Length);
        var actualGuid = System.Text.Encoding.ASCII.GetString(buffer, 0, 32);
        return EmulationStationMapping(actualGuid, name ?? "", FrontendInputs());
    }
    private static IEnumerable<XElement> FrontendInputs()
    {
        foreach (var path in new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".emulationstation/es_input.cfg"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config/emulationstation/es_input.cfg"),
            "/storage/.config/emulationstation/es_input.cfg", "/userdata/system/configs/emulationstation/es_input.cfg", "/etc/emulationstation/es_input.cfg", "/usr/share/emulationstation/es_input.cfg" })
        {
            if (!File.Exists(path)) continue;
            XElement[] inputs;
            try { inputs = XDocument.Load(path).Descendants("inputConfig").ToArray(); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Xml.XmlException)
            { Console.Error.WriteLine("INPUT mapping: " + error.Message); continue; }
            foreach (var input in inputs) yield return input;
        }
    }
    private static string? EmulationStationMapping(string actualGuid, string name, IEnumerable<XElement> inputs)
    {
        foreach (var config in inputs)
        {
                if ((string?)config.Attribute("deviceName") != name ||
                    (string?)config.Attribute("deviceGUID") is not { Length: 32 } guid ||
                    !string.Equals(actualGuid, guid, StringComparison.OrdinalIgnoreCase)) continue;
                var fields = new List<string>();
                foreach (var input in config.Elements("input"))
                {
                    var target = (string?)input.Attribute("name") switch
                    {
                        "a" => "a", "b" => "b", "x" => "x", "y" => "y", "start" => "start", "select" => "back",
                        "pageup" => "leftshoulder", "pagedown" => "rightshoulder", "up" => "dpup", "down" => "dpdown",
                        "left" => "dpleft", "right" => "dpright", "joystick1left" => "leftx", "joystick1up" => "lefty",
                        "joystick2left" => "rightx", "joystick2up" => "righty", "l2" => "lefttrigger", "r2" => "righttrigger", _ => null
                    };
                    var id = (string?)input.Attribute("id");
                    var binding = (string?)input.Attribute("type") switch
                    {
                        "button" => "b" + id, "hat" => "h" + id + "." + (string?)input.Attribute("value"),
                        "axis" => "a" + id + ((string?)input.Attribute("value") == "1" && target is "leftx" or "lefty" or "rightx" or "righty" ? "~" : ""), _ => null
                    };
                    if (target is not null && binding is not null) fields.Add(target + ":" + binding);
                }
                var mapping = guid + "," + name!.Replace(',', ' ') + "," + string.Join(',', fields) + ",platform:Linux,";
                return mapping;
        }
        return null;
    }
    private static void ImportNextUiMapping(int index)
    {
        if (Environment.GetEnvironmentVariable("IS_NEXT") != "yes") return;
        var name = Marshal.PtrToStringUTF8(Sdl.SDL_JoystickNameForIndex(index)) ?? "";
        if (!new[] { "trimui", "retrogame", "anbernic", "gpio", "deeplay" }.Any(n => name.Contains(n, StringComparison.OrdinalIgnoreCase))) return;
        var platform = Environment.GetEnvironmentVariable("PLATFORM");
        var bindings = platform switch
        {
            "tg5040" => "a:b1,b:b0,x:b3,y:b2,back:b6,start:b7,leftshoulder:b4,rightshoulder:b5,dpup:h0.1,dpdown:h0.4,dpleft:h0.8,dpright:h0.2,leftx:a0,lefty:a1,rightx:a3,righty:a4,lefttrigger:a2,righttrigger:a5",
            "tg5050" => "a:b1,b:b0,x:b3,y:b2,back:b6,start:b7,leftshoulder:b4,rightshoulder:b5,dpup:h0.1,dpdown:h0.4,dpleft:h0.8,dpright:h0.2,leftx:a0,lefty:a1,rightx:a3,righty:a4,lefttrigger:a2,righttrigger:a5",
            "h700" => "a:b0,b:b1,x:b3,y:b2,back:b6,start:b7,leftshoulder:b4,rightshoulder:b5,dpup:h0.1,dpdown:h0.4,dpleft:h0.8,dpright:h0.2,leftx:a0,lefty:a1,rightx:a2,righty:a3,lefttrigger:b10,righttrigger:b11",
            _ => null
        };
        if (bindings is null) return;
        var buffer = new byte[33]; Sdl.SDL_JoystickGetGUIDString(Sdl.SDL_JoystickGetDeviceGUID(index), buffer, buffer.Length);
        var guid = System.Text.Encoding.ASCII.GetString(buffer, 0, 32);
        if (Sdl.SDL_GameControllerAddMapping($"{guid},{name.Replace(',', ' ')},{bindings},platform:Linux,") >= 0)
            Console.WriteLine($"INPUT NextUI {platform} fallback: {name} GUID={guid}");
    }
    public bool Handle(Sdl.Event e)
    {
        if (e.Type is Sdl.ControllerAdded or Sdl.ControllerRemoved) { OpenControllers(); return true; }
        // Focus loss may swallow key-up; no held key may keep navigating after it.
        if (e.Type == Sdl.WindowEvent && e.WindowKind == 13) { ClearHeld(); return false; }
        if (PKForge.App.App.IsSuspended) { ClearHeld(); return false; }
        if (e.Type == Sdl.ControllerAxis && e.Axis == 5)
        {
            if (!_needsNeutral) SetButton(PadButton.R2, TriggerPressed(e.AxisValue, _held.ContainsKey(PadButton.R2)));
            return true;
        }
        PadButton? button = null;
        var pressed = false;
        if (e.Type is Sdl.ControllerDown or Sdl.ControllerUp)
        {
            button = e.Button switch
            {
                0 => PadButton.A, 1 => PadButton.B, 2 => PadButton.X, 3 => PadButton.Y,
                4 => PadButton.Select, 6 => PadButton.Start, 9 => PadButton.L, 10 => PadButton.R,
                11 => PadButton.Up, 12 => PadButton.Down, 13 => PadButton.Left, 14 => PadButton.Right, _ => null
            };
            pressed = e.Type == Sdl.ControllerDown;
        }
        if (e.Type is Sdl.KeyDown or Sdl.KeyUp)
        {
            if (e.KeyRepeat != 0) return true;
            button = e.Key switch
            {
                13 or 122 => PadButton.A, 27 or 120 => PadButton.B, 115 or 99 => PadButton.X, 97 or 118 => PadButton.Y,
                113 => PadButton.L, 119 => PadButton.R, 101 => PadButton.R2, 9 => PadButton.Start, 8 or 1073742053 => PadButton.Select,
                1073741906 => PadButton.Up, 1073741905 => PadButton.Down,
                1073741904 => PadButton.Left, 1073741903 => PadButton.Right, _ => null
            };
            pressed = e.Type == Sdl.KeyDown;
        }
        if (button is null) return false;
        SetButton(button.Value, pressed);
        return true;
    }
    internal static bool TriggerPressed(short value, bool held) => value >= (held ? 8000 : 16000);
    private void SetButton(PadButton button, bool pressed)
    {
        if (pressed)
        {
            if (_held.ContainsKey(button)) return;
            var repeats = button is PadButton.Up or PadButton.Down or PadButton.Left or PadButton.Right ||
                button is PadButton.L or PadButton.R && _router.TopPagesWithShoulders;
            _held[button] = (Stopwatch.GetTimestamp(), 0);
            Console.WriteLine("INPUT " + button);
            if (!_router.Dispatch(button) || !repeats) _repeatBlocked.Add(button);
        }
        else if (_held.Remove(button)) { _repeatBlocked.Remove(button); _router.DispatchRelease(button); }
    }
    public void Update()
    {
        if (PKForge.App.App.IsSuspended) { ClearHeld(); return; }
        var controller = _controllers.FirstOrDefault();
        static float Axis(short value) => Math.Abs((int)value) < 6000 ? 0 : Math.Clamp(value / 32767f, -1, 1);
        var x = controller == IntPtr.Zero ? 0 : Axis(Sdl.SDL_GameControllerGetAxis(controller, 0));
        var y = controller == IntPtr.Zero ? 0 : Axis(Sdl.SDL_GameControllerGetAxis(controller, 1));
        var rx = controller == IntPtr.Zero ? 0 : Axis(Sdl.SDL_GameControllerGetAxis(controller, 2));
        var ry = controller == IntPtr.Zero ? 0 : Axis(Sdl.SDL_GameControllerGetAxis(controller, 3));
        var trigger = controller != IntPtr.Zero && TriggerPressed(Sdl.SDL_GameControllerGetAxis(controller, 5), _held.ContainsKey(PadButton.R2));
        if (_needsNeutral)
        {
            if (x == 0 && y == 0 && rx == 0 && ry == 0 && !trigger) _needsNeutral = false;
            else { _router.ResetInput(); return; }
        }
        _router.DispatchStick(x, y, rx, ry);
        var now = Stopwatch.GetTimestamp();
        if (_held.TryGetValue(PadButton.Start, out var start) && _held.TryGetValue(PadButton.Select, out var select) &&
            Stopwatch.GetElapsedTime(Math.Max(start.Since, select.Since), now).TotalMilliseconds >= 600) _quit();
        foreach (var (button, time) in _held.ToArray())
        {
            var directional = button is PadButton.Up or PadButton.Down or PadButton.Left or PadButton.Right;
            if (!directional && button is not (PadButton.L or PadButton.R)) continue;
            if (!directional && !_router.TopPagesWithShoulders) _repeatBlocked.Add(button);
            if (_repeatBlocked.Contains(button)) continue;
            if (!PadRepeatTiming.Due(directional, Stopwatch.GetElapsedTime(time.Since, now).TotalMilliseconds,
                time.Last == 0 ? null : Stopwatch.GetElapsedTime(time.Last, now).TotalMilliseconds)) continue;
            _held[button] = (time.Since, now);
            if (!_router.Dispatch(button)) _repeatBlocked.Add(button);
        }
    }
    public void ClearHeld()
    {
        foreach (var button in _held.Keys.ToArray()) _router.DispatchRelease(button);
        _held.Clear(); _repeatBlocked.Clear();
        _router.ResetInput(); _needsNeutral = true;
    }
    public void Dispose() { foreach (var controller in _controllers) Sdl.SDL_GameControllerClose(controller); }
}
