using PKForge.Linux.Native;
using Xunit;

namespace PKForge.Linux.Tests;

public sealed class FirmwareExitChordTests
{
    private static Sdl.Event Button(byte button, bool down = true) =>
        new() { Type = down ? Sdl.ControllerDown : Sdl.ControllerUp, Button = button };

    [Fact]
    public void Knulli_menu_start_exits_in_either_order_and_release_or_reset_clears_it()
    {
        var chord = new FirmwareExitChord(true);
        foreach (var (first, second) in new (byte, byte)[] { (5, 6), (6, 5) })
        {
            chord.Reset();
            Assert.False(chord.Handle(Button(first)));
            Assert.True(chord.Handle(Button(second)));
            Assert.False(chord.Handle(Button(first, false)));
            chord.Reset();
            Assert.False(chord.Handle(Button(second)));
        }
        chord.Reset();
        Assert.False(chord.Handle(Button(5)));
        Assert.False(chord.Handle(Button(5, false)));
        Assert.False(chord.Handle(Button(6)));
    }

    [Fact]
    public void Forwarded_alt_f4_is_supported_without_treating_f4_or_other_firmware_as_exit()
    {
        var chord = new FirmwareExitChord(true);
        var key = new Sdl.Event { Type = Sdl.KeyDown, Key = 1073741885 };
        Assert.False(chord.Handle(key));
        key.KeyMod = 0x0100; Assert.True(chord.Handle(key));
        key.KeyMod = 0x0200; Assert.True(chord.Handle(key));
        key.Type = Sdl.KeyUp; Assert.False(chord.Handle(key));
        var other = new FirmwareExitChord(false);
        Assert.False(other.Handle(Button(5))); Assert.False(other.Handle(Button(6)));
        key.Type = Sdl.KeyDown; Assert.False(other.Handle(key));
    }
}
