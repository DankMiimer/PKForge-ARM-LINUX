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
    public InputRouter(GamepadRouter router, Action quit)
    {
        _router = router;
        _quit = quit;
        OpenControllers();
    }
    private void OpenControllers()
    {
        foreach (var controller in _controllers) Sdl.SDL_GameControllerClose(controller);
        _controllers.Clear(); ClearHeld();
        for (var i = 0; i < Sdl.SDL_NumJoysticks(); i++)
        {
            if (Sdl.SDL_IsGameController(i) == 0) ImportEmulationStationMapping(i);
            if (Sdl.SDL_IsGameController(i) == 0) { Console.WriteLine($"INPUT joystick {i}: no SDL controller mapping"); continue; }
            var controller = Sdl.SDL_GameControllerOpen(i);
            if (controller == IntPtr.Zero) continue;
            _controllers.Add(controller);
            Console.WriteLine("INPUT controller: " + Marshal.PtrToStringUTF8(Sdl.SDL_GameControllerName(controller)));
        }
    }
    private static void ImportEmulationStationMapping(int index)
    {
        var name = Marshal.PtrToStringUTF8(Sdl.SDL_JoystickNameForIndex(index));
        foreach (var path in new[] { "/userdata/system/configs/emulationstation/es_input.cfg", "/usr/share/emulationstation/es_input.cfg" })
        {
            if (!File.Exists(path)) continue;
            try
            {
                var config = XDocument.Load(path).Descendants("inputConfig").FirstOrDefault(c => (string?)c.Attribute("deviceName") == name);
                if (config is null || (string?)config.Attribute("deviceGUID") is not { Length: 32 } guid) continue;
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
                if (Sdl.SDL_GameControllerAddMapping(mapping) >= 0) Console.WriteLine("INPUT imported EmulationStation mapping: " + name);
                return;
            }
            catch (Exception error) when (error is IOException or System.Xml.XmlException) { Console.Error.WriteLine("INPUT mapping: " + error.Message); }
        }
    }
    public bool Handle(Sdl.Event e)
    {
        if (e.Type is Sdl.ControllerAdded or Sdl.ControllerRemoved) { OpenControllers(); return true; }
        // Focus loss may swallow key-up; no held key may keep navigating after it.
        if (e.Type == Sdl.WindowEvent && e.WindowKind == 13) { ClearHeld(); return false; }
        if (PKForge.App.App.IsSuspended) { ClearHeld(); return false; }
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
                113 => PadButton.L, 119 => PadButton.R, 9 => PadButton.Start, 8 or 1073742053 => PadButton.Select,
                1073741906 => PadButton.Up, 1073741905 => PadButton.Down,
                1073741904 => PadButton.Left, 1073741903 => PadButton.Right, _ => null
            };
            pressed = e.Type == Sdl.KeyDown;
        }
        if (button is null) return false;
        if (pressed)
        {
            if (_held.ContainsKey(button.Value)) return true;
            var repeats = button is PadButton.Up or PadButton.Down or PadButton.Left or PadButton.Right ||
                button is PadButton.L or PadButton.R && _router.TopPagesWithShoulders;
            _held[button.Value] = (Stopwatch.GetTimestamp(), 0);
            Console.WriteLine("INPUT " + button);
            if (!_router.Dispatch(button.Value) || !repeats) _repeatBlocked.Add(button.Value);
        }
        else { _held.Remove(button.Value); _repeatBlocked.Remove(button.Value); _router.DispatchRelease(button.Value); }
        return true;
    }
    public void Update()
    {
        if (PKForge.App.App.IsSuspended) { ClearHeld(); return; }
        var controller = _controllers.FirstOrDefault();
        static float Axis(short value) => Math.Abs((int)value) < 6000 ? 0 : Math.Clamp(value / 32767f, -1, 1);
        if (controller != IntPtr.Zero)
            _router.DispatchStick(Axis(Sdl.SDL_GameControllerGetAxis(controller, 0)), Axis(Sdl.SDL_GameControllerGetAxis(controller, 1)),
                Axis(Sdl.SDL_GameControllerGetAxis(controller, 2)), Axis(Sdl.SDL_GameControllerGetAxis(controller, 3)));
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
    private void ClearHeld()
    {
        foreach (var button in _held.Keys.ToArray()) _router.DispatchRelease(button);
        _held.Clear(); _repeatBlocked.Clear();
        _router.DispatchStick(0, 0, 0, 0);
    }
    public void Dispose() { foreach (var controller in _controllers) Sdl.SDL_GameControllerClose(controller); }
}
