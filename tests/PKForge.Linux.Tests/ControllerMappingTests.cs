using PKForge.Linux.Native;
using Xunit;

namespace PKForge.Linux.Tests;

public sealed class ControllerMappingTests
{
    // Captured from the RG34XXSP's frontend launch and its installed ES config.
    private const string Header = "19000000010000000100000000010000,Anbernic RG34XX-SP Controller,";
    private const string Other = "dpup:h0.1,lefttrigger:b13,righttrigger:b14,leftx:a0,lefty:a1,platform:Linux,";
    private const string Generated = Header + "a:b4,b:b3,x:b5,y:b6," + Other;
    private const string Frontend = Header + "a:b3,b:b4,x:b6,y:b5," + Other;

    [Fact]
    public void Knulli_default_matches_frontend_labels_and_retains_non_face_controls()
    {
        Assert.Equal(Frontend, ControllerMappings.KnulliLabels("knulli", null, false, Generated, Frontend));
        // The existing per-game Xbox layout already emits the frontend labels.
        Assert.Null(ControllerMappings.KnulliLabels("knulli", null, false, Frontend, Frontend));
        // Never use another controller's template or a partial mapping.
        Assert.Null(ControllerMappings.KnulliLabels("knulli", null, false, Generated, Frontend.Replace("19000000", "03000000")));
        Assert.Null(ControllerMappings.KnulliLabels("knulli", null, false, Generated, Frontend.Replace("y:b5,", "")));
    }

    [Fact]
    public void Explicit_mappings_layout_choices_and_other_firmware_keep_their_semantics()
    {
        Assert.Null(ControllerMappings.KnulliLabels("knulli", null, true, Generated, Frontend));
        Assert.Null(ControllerMappings.KnulliLabels("knulli", "sdl", false, Generated, Frontend));
        Assert.Null(ControllerMappings.KnulliLabels("ROCKNIX", null, false, Generated, Frontend));
    }

    [Fact]
    public void Pre_init_hint_keeps_other_controllers_and_explicit_app_overrides_last()
    {
        var other = "030000005e0400008e02000004010000,Xbox,a:b0,b:b1,x:b2,y:b3,platform:Linux,";
        Assert.Equal(other + "\n" + Frontend,
            ControllerMappings.Prepare("knulli", null, [other, Generated], [],
                (guid, _) => guid == Header[..32] ? Frontend : null));
        // An intentional reverse mapping must survive even on KNULLI.
        Assert.Equal(Frontend + "\n" + Generated,
            ControllerMappings.Prepare("knulli", null, [Frontend], [Generated], (_, _) => Frontend));
        Assert.Null(ControllerMappings.Prepare("knulli", "sdl", [Generated], [], (_, _) => Frontend));
        Assert.Null(ControllerMappings.Prepare("ROCKNIX", null, [Generated], [], (_, _) => Frontend));
    }
}
