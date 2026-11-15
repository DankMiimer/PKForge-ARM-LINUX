using System.Runtime.InteropServices;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace PKForge.Linux.Native;

/// <summary>Two identical Goodix devices cannot be distinguished by Sway's identifier.
/// Read their independent evdev streams instead, grabbing only for this app's lifetime.
/// Which stream is the top panel's comes from the hardware, not from the order the kernel met them in.</summary>
internal sealed class RgdsTouch : IDisposable
{
    private readonly TouchDevice _top, _bottom;
    private RgdsTouch(TouchDevice top, TouchDevice bottom) { _top = top; _bottom = bottom; }
    /// <summary>An Anbernic RG DS or RG DS Plus, by the board's own name.</summary>
    public static bool IsRgDs() => OperatingSystem.IsLinux() && File.Exists("/proc/device-tree/model") && IsRgDs(File.ReadAllText("/proc/device-tree/model"));
    internal static bool IsRgDs(string model) => model.Contains("RG DS", StringComparison.OrdinalIgnoreCase) || model.Contains("RGDS", StringComparison.OrdinalIgnoreCase);
    internal static bool IsTouchscreen(string name) => name.Contains("Goodix", StringComparison.OrdinalIgnoreCase) || name.Contains("touchscreen", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Which of two touchscreens is the top panel's and which the bottom's, from their sysfs paths.
    /// A firmware hint (BananaOS's BANANA_TOUCH: part of the bottom one's path) decides first; then
    /// the RG DS's wiring, where the bottom panel's controller is on the I2C bus at fe5e0000 (the top
    /// one's at fe5c0000); otherwise the order given, as ROCKNIX numbers them.
    /// </summary>
    internal static (int Top, int Bottom, string By) Order(IReadOnlyList<string> paths, string? bottomHint)
    {
        foreach (var (part, by) in new[] { (bottomHint, "firmware hint"), ("fe5e0000.i2c", "wiring") })
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            var bottom = paths.Select((path, index) => (path, index)).Where(p => p.path.Contains(part, StringComparison.Ordinal)).Select(p => p.index).ToArray();
            if (bottom.Length == 1) return (1 - bottom[0], bottom[0], by);
        }
        return (0, 1, "order");
    }

    public static RgdsTouch? TryCreate(bool dual)
    {
        if (!dual || !IsRgDs()) return null;
        var devices = Directory.EnumerateDirectories("/sys/class/input", "event*")
            .Where(p => File.Exists(Path.Combine(p, "device/name")) && IsTouchscreen(File.ReadAllText(Path.Combine(p, "device/name"))))
            .OrderBy(p => int.Parse(Path.GetFileName(p)[5..], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        if (devices.Length != 2) return null;
        var (first, second, by) = Order([.. devices.Select(p => Directory.ResolveLinkTarget(p, true)?.FullName ?? p)], Environment.GetEnvironmentVariable("BANANA_TOUCH"));
        devices = [devices[first], devices[second]];
        TouchDevice? top = null;
        try
        {
            top = new TouchDevice("/dev/input/" + Path.GetFileName(devices[0]));
            var bottom = new TouchDevice("/dev/input/" + Path.GetFileName(devices[1]));
            Console.WriteLine($"TOUCH independent panels: top={Path.GetFileName(devices[0])}, bottom={Path.GetFileName(devices[1])} (by {by})");
            return new(top, bottom);
        }
        catch (IOException error) { top?.Dispose(); Console.Error.WriteLine("TOUCH SDL fallback: " + error.Message); return null; }
    }
    public void Pump(int width, int height, Action<bool, SKTouchEventArgs> dispatch, bool swap = false)
    {
        _top.Pump(width, height, e => dispatch(!swap, e));
        _bottom.Pump(width, height, e => dispatch(swap, e));
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
