using PKForge.Linux.Native;
using Xunit;

namespace PKForge.Linux.Tests;

/// <summary>The RG DS and RG DS Plus on firmware other than ROCKNIX (BananaOS): panels, touch and pad.</summary>
public sealed class RgDsFamilyTests
{
    // As the RG DS reports them under ROCKNIX: the top panel's controller, then the bottom one's.
    private const string Top = "/sys/devices/platform/fe5c0000.i2c/i2c-3/3-0014/input/input1/event1";
    private const string Bottom = "/sys/devices/platform/fe5e0000.i2c/i2c-5/5-0014/input/input2/event2";

    [Fact]
    public void The_bottom_touchscreen_is_known_by_its_wiring_whatever_order_the_kernel_met_them_in()
    {
        Assert.Equal((0, 1, "wiring"), RgdsTouch.Order([Top, Bottom], null));
        Assert.Equal((1, 0, "wiring"), RgdsTouch.Order([Bottom, Top], ""));
    }

    [Fact]
    public void A_firmware_hint_for_the_bottom_touchscreen_decides_before_the_wiring()
    {
        Assert.Equal((1, 0, "firmware hint"), RgdsTouch.Order([Top, Bottom], "fe5c0000"));
        Assert.Equal((0, 1, "firmware hint"), RgdsTouch.Order([Top, Bottom], "5-0014"));
        // A hint that names neither or both tells nothing: the wiring still does.
        Assert.Equal((0, 1, "wiring"), RgdsTouch.Order([Top, Bottom], "nothing-like-it"));
        Assert.Equal((1, 0, "wiring"), RgdsTouch.Order([Bottom, Top], "i2c"));
    }

    [Fact]
    public void Unknown_boards_keep_the_given_order()
    {
        Assert.Equal((0, 1, "order"), RgdsTouch.Order(["/sys/devices/a/event3", "/sys/devices/b/event4"], null));
        Assert.Equal((0, 1, "order"), RgdsTouch.Order([Bottom, Bottom.Replace("event2", "event9")], null));
    }

    [Theory]
    [InlineData("Anbernic RG DS", true)]
    [InlineData("Anbernic RG DS Plus\0", true)]
    [InlineData("Anbernic RGDS Plus", true)]
    [InlineData("Anbernic RG34XX-SP", false)]
    [InlineData("Powkiddy RGB30", false)]
    public void The_family_is_recognized_by_the_board_name(string model, bool expected) => Assert.Equal(expected, RgdsTouch.IsRgDs(model));

    [Theory]
    [InlineData("Goodix Capacitive TouchScreen", true)]
    [InlineData("generic ft5x06 touchscreen", true)]
    [InlineData("retrogame_joypad", false)]
    [InlineData("gpio-keys-hall", false)]
    public void Touch_panels_are_told_from_the_other_input_devices(string name, bool expected) => Assert.Equal(expected, RgdsTouch.IsTouchscreen(name));

    [Fact]
    public void The_fallback_pad_mapping_binds_every_control_once()
    {
        var bindings = InputRouter.RgDsBindings.Split(',').Select(binding => binding.Split(':')).ToArray();
        Assert.All(bindings, binding => Assert.Equal(2, binding.Length));
        Assert.Equal(bindings.Length, bindings.Select(binding => binding[0]).Distinct().Count());
        Assert.Equal(bindings.Length, bindings.Select(binding => binding[1]).Distinct().Count());
        foreach (var control in new[] { "a", "b", "x", "y", "back", "start", "leftshoulder", "rightshoulder", "lefttrigger", "righttrigger", "dpup", "dpdown", "dpleft", "dpright" })
            Assert.Contains(bindings, binding => binding[0] == control);
        // The face buttons as PortMaster maps this pad, which is what PKForge's printed hints were checked against.
        Assert.Contains("a:b1,b:b0,x:b2,y:b3", InputRouter.RgDsBindings);
    }
}
