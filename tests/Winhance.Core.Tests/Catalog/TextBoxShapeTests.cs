using FluentAssertions;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Selections;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.Core.Tests.Catalog;

public class TextBoxShapeTests
{
    private static readonly TextRule ThreeCapitals = new("^[A-Z]{3}$", UpperCase: true, TestKeys.Of("Three capital letters."));

    private static Setting TextSetting(string? defaultValue = null) => new()
    {
        Id = "t",
        Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d") },
        Targets = [new AutounattendElement("K", "specialize", "Microsoft-Windows-Shell-Setup", "ComputerName")],
        TextBox = new(ThreeCapitals, defaultValue),
    };

    [Fact]
    public void A_rule_derives_a_text_box()
    {
        TextSetting().Control.Should().Be(ControlKind.TextBox);
    }

    [Fact]
    public void A_numeric_range_still_derives_a_slider()
    {
        var setting = TextSetting() with { Numeric = new() { Min = 0, Max = 10 } };

        setting.Control.Should().Be(ControlKind.Slider);
    }

    [Fact]
    public void A_text_setting_is_answer_file_only()
    {
        TextSetting().IsAnswerFileOnly.Should().BeTrue();
    }

    [Fact]
    public void A_seed_read_from_this_PC_keeps_the_setting_answer_file_only()
    {
        var setting = TextSetting() with
        {
            Targets =
            [
                new AutounattendElement("K", "specialize", "Microsoft-Windows-Shell-Setup", "ComputerName"),
                new RegTarget("this-pc", [@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters"], "Hostname", RegistryValueKind.String) { ReadOnly = true },
            ],
            TextBox = new(ThreeCapitals, SeedKey: "this-pc"),
        };

        setting.IsAnswerFileOnly.Should().BeTrue();
        setting.Control.Should().Be(ControlKind.TextBox);
    }

    [Fact]
    public void A_box_on_the_desktop_slideshow_is_a_live_setting()
    {
        var setting = TextSetting() with
        {
            Targets = [new DesktopSlideshowTarget("album")],
            CustomStateScripts = [new ScriptEffect("Set-Slideshow '{{value}}'", RunContext.User)],
        };

        setting.Control.Should().Be(ControlKind.TextBox);
        setting.IsAnswerFileOnly.Should().BeFalse();
    }

    [Fact]
    public void A_text_setting_is_a_text_box_in_the_config_file()
    {
        ConfigFileMapper.InputTypeFor(TextSetting()).Should().Be(InputType.TextBox);
    }

    [Fact]
    public void The_rule_normalizes_before_it_matches()
    {
        ThreeCapitals.Normalize("abc ").Should().Be("ABC");
        ThreeCapitals.Matches("abc").Should().BeTrue();
        ThreeCapitals.Matches("abcd").Should().BeFalse();
    }

    [Fact]
    public void A_text_setting_names_an_error_string_and_no_option_captions()
    {
        var setting = TextSetting();

        setting.TextBox!.Rule.Message.Value.Should().Be("Three capital letters.");
        setting.States.Should().BeEmpty();
    }

    [Fact]
    public void A_box_offers_no_picker_unless_it_asks_for_one()
    {
        TextSetting().TextBox!.Picker.Should().Be(PickerKind.None);
        (TextSetting().TextBox! with { Picker = PickerKind.Folder }).Picker.Should().Be(PickerKind.Folder);
    }
}
