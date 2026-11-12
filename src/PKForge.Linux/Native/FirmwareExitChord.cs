namespace PKForge.Linux.Native;

// KNULLI exports the frontend's configured Menu/hotkey as SDL Guide. Some Ports
// launch paths have no keyboard hotkey daemon; honor the semantic controller
// chord as well as the Alt+F4 event advertised by its shell hotkey context.
internal sealed class FirmwareExitChord(bool enabled)
{
    private bool _menu, _start;
    public void Reset() { _menu = false; _start = false; }
    public bool Handle(Sdl.Event e)
    {
        if (!enabled) return false;
        if (e.Type == Sdl.KeyDown && e.KeyRepeat == 0 && e.Key == 1073741885 && (e.KeyMod & 0x0300) != 0) return true;
        if (e.Type is not (Sdl.ControllerDown or Sdl.ControllerUp)) return false;
        if (e.Button == 5) _menu = e.Type == Sdl.ControllerDown;
        else if (e.Button == 6) _start = e.Type == Sdl.ControllerDown;
        else return false;
        return _menu && _start;
    }
}
