using System.Globalization;
using System.Runtime.InteropServices;
using PKForge.App;

namespace PKForge.Linux.Native;

internal sealed class LidMonitor : IDisposable
{
    private readonly int _fd = -1;
    private readonly byte[] _buffer = new byte[24 * 32];
    public LidMonitor()
    {
        if (!OperatingSystem.IsLinux()) return;
        foreach (var path in Directory.EnumerateDirectories("/sys/class/input", "event*"))
        {
            var capability = Path.Combine(path, "device/capabilities/sw");
            if (!File.Exists(capability) || !ulong.TryParse(File.ReadAllText(capability).Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bits) || (bits & 1) == 0) continue;
            // Read-only, nonexclusive observation: firmware retains its configured lid/power action.
            // Never grab this device, inhibit shutdown, or change OS power settings here.
            _fd = open("/dev/input/" + Path.GetFileName(path), 0x800 | 0x80000);
            if (_fd >= 0) { Console.WriteLine("LID monitor: " + Path.GetFileName(path)); return; }
        }
    }
    public void Pump()
    {
        if (_fd < 0) return;
        int size;
        while ((size = (int)read(_fd, _buffer, (nuint)_buffer.Length)) > 0)
        for (var i = 0; i + 24 <= size; i += 24)
            if (BitConverter.ToUInt16(_buffer, i + 16) == 5 && BitConverter.ToUInt16(_buffer, i + 18) == 0)
            {
                var closed = BitConverter.ToInt32(_buffer, i + 20) != 0;
                Console.WriteLine(closed ? "LID closed: park saved and paused" : "LID opened: park resumed");
                if (closed) PKForge.App.App.Suspend(); else PKForge.App.App.Resume();
            }
    }
    public void Dispose() { if (_fd >= 0) close(_fd); }
    [DllImport("libc", SetLastError = true)] private static extern int open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
    [DllImport("libc", SetLastError = true)] private static extern nint read(int fd, byte[] buffer, nuint count);
    [DllImport("libc")] private static extern int close(int fd);
}
