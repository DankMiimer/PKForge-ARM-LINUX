using System.Runtime.InteropServices;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace PKForge.Linux.Native;

/// <summary>Two identical Goodix devices cannot be distinguished by Sway's identifier.
/// Read their independent evdev streams instead, grabbing only for this app's lifetime.</summary>
internal sealed class RgdsTouch : IDisposable
{
    private readonly TouchDevice _top, _bottom;
    private RgdsTouch(TouchDevice top, TouchDevice bottom) { _top = top; _bottom = bottom; }
    public static RgdsTouch? TryCreate(bool dual)
    {
        if (!dual || !OperatingSystem.IsLinux() || !File.Exists("/proc/device-tree/model") ||
            !File.ReadAllText("/proc/device-tree/model").Contains("RG DS", StringComparison.Ordinal)) return null;
        var devices = Directory.EnumerateDirectories("/sys/class/input", "event*")
            .Where(p => File.Exists(Path.Combine(p, "device/name")) && File.ReadAllText(Path.Combine(p, "device/name")).Contains("Goodix", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => int.Parse(Path.GetFileName(p)[5..], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (devices.Length != 2) return null;
        TouchDevice? top = null;
        try
        {
            top = new TouchDevice("/dev/input/" + Path.GetFileName(devices[0]));
            var bottom = new TouchDevice("/dev/input/" + Path.GetFileName(devices[1]));
            Console.WriteLine($"TOUCH independent panels: top={Path.GetFileName(devices[0])}, bottom={Path.GetFileName(devices[1])}");
            return new(top, bottom);
        }
        catch (IOException error) { top?.Dispose(); Console.Error.WriteLine("TOUCH SDL fallback: " + error.Message); return null; }
    }
    public void Pump(int width, int height, Action<bool, SKTouchEventArgs> dispatch)
    {
        _top.Pump(width, height, e => dispatch(true, e));
        _bottom.Pump(width, height, e => dispatch(false, e));
    }
    public void Dispose() { _top.Dispose(); _bottom.Dispose(); }
    private sealed class TouchDevice : IDisposable
    {
        private readonly int _fd;
        private readonly int _minX, _maxX, _minY, _maxY;
        private readonly byte[] _buffer = new byte[24 * 64];
        private readonly Dictionary<int, Contact> _contacts = [];
        private int _slot;
        public TouchDevice(string path)
        {
            _fd = open(path, 0x800 | 0x80000); // O_NONBLOCK | O_CLOEXEC, read only
            if (_fd < 0) throw new IOException("Cannot open " + path);
            if (ioctl(_fd, 0x40044590, 1) < 0) { close(_fd); throw new IOException("Cannot grab " + path); }
            (_minX, _maxX) = Range(0x35); (_minY, _maxY) = Range(0x36);
        }
        private (int, int) Range(int axis)
        {
            var info = new byte[24];
            if (ioctl_abs(_fd, 0x80184540u + (uint)axis, info) < 0) return (0, axis == 0x35 ? 639 : 479);
            return (BitConverter.ToInt32(info, 4), BitConverter.ToInt32(info, 8));
        }
        private sealed class Contact { public int Id = -1, X, Y; public bool Sent, Changed; }
        public void Pump(int width, int height, Action<SKTouchEventArgs> dispatch)
        {
            int size;
            while ((size = (int)read(_fd, _buffer, (nuint)_buffer.Length)) > 0)
            for (var at = 0; at + 24 <= size; at += 24)
            {
                var type = BitConverter.ToUInt16(_buffer, at + 16); var code = BitConverter.ToUInt16(_buffer, at + 18); var value = BitConverter.ToInt32(_buffer, at + 20);
                if (type == 3)
                {
                    if (code == 0x2f) { _slot = Math.Clamp(value, 0, 31); continue; }
                    if (!_contacts.TryGetValue(_slot, out var contact)) _contacts[_slot] = contact = new();
                    if (code == 0x39) contact.Id = value;
                    if (code == 0x35) { contact.X = value; contact.Changed = true; }
                    if (code == 0x36) { contact.Y = value; contact.Changed = true; }
                }
                if (type != 0 || code != 0) continue;
                foreach (var (slot, contact) in _contacts)
                {
                    var x = (contact.X - _minX) * (width - 1f) / Math.Max(1, _maxX - _minX);
                    var y = (contact.Y - _minY) * (height - 1f) / Math.Max(1, _maxY - _minY);
                    if (contact.Id >= 0 && !contact.Sent) { dispatch(new(slot, SKTouchAction.Pressed, new(x, y))); contact.Sent = true; }
                    else if (contact.Id >= 0 && contact.Changed) dispatch(new(slot, SKTouchAction.Moved, new(x, y)));
                    else if (contact.Id < 0 && contact.Sent) { dispatch(new(slot, SKTouchAction.Released, new(x, y))); contact.Sent = false; }
                    contact.Changed = false;
                }
            }
        }
        public void Dispose() { ioctl(_fd, 0x40044590, 0); close(_fd); }
        [DllImport("libc", SetLastError = true)] private static extern int open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
        [DllImport("libc", SetLastError = true)] private static extern nint read(int fd, byte[] buffer, nuint count);
        [DllImport("libc", SetLastError = true)] private static extern int ioctl(int fd, uint request, int value);
        [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)] private static extern int ioctl_abs(int fd, uint request, byte[] info);
        [DllImport("libc")] private static extern int close(int fd);
    }
}
