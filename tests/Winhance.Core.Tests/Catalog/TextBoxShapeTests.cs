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
    public void Control_RuleSetting_DerivesTextBox()
    {
        TextSetting().Control.Should().Be(ControlKind.TextBox);
    }

    [Fact]
    public void Control_NumericRange_StillDerivesSlider()
    {
        var setting = TextSetting() with { Numeric = new() { Min = 0, Max = 10 } };

        setting.Control.Should().Be(ControlKind.Slider);
    }

    [Fact]
    public void IsAnswerFileOnly_TextSetting_IsTrue()
    {
        TextSetting().IsAnswerFileOnly.Should().BeTrue();
    }

    [Fact]
    public void IsAnswerFileOnly_SeedReadFromThisPc_StaysTrue()
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
    public void IsAnswerFileOnly_BoxOnDesktopSlideshow_IsFalse()
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
    public void IsAnswerFileOnly_BoxWhoseTextOnlyFillsScripts_IsTrue()
    {
        var setting = TextSetting() with
        {
            Targets = [],
            CustomStateScripts = [new ScriptEffect("Move-Folders '{{value}}'", RunContext.User)],
        };

        setting.IsAnswerFileOnly.Should().BeTrue();
    }

    [Fact]
    public void InputTypeFor_TextSetting_ReturnsTextBox()
    {
        ConfigFileMapper.InputTypeFor(TextSetting()).Should().Be(InputType.TextBox);
    }

    [Fact]
    public void Matches_Rule_NormalizesBeforeMatching()
    {
        ThreeCapitals.Normalize("abc ").Should().Be("ABC");
        ThreeCapitals.Matches("abc").Should().BeTrue();
        ThreeCapitals.Matches("abcd").Should().BeFalse();
    }

    [Fact]
    public void TextBox_TextSetting_NamesErrorStringAndNoOptionCaptions()
    {
        var setting = TextSetting();

        setting.TextBox!.Rule.Message.Value.Should().Be("Three capital letters.");
        setting.States.Should().BeEmpty();
    }

    [Fact]
    public void Picker_Box_IsNoneUnlessItAsksForOne()
    {
        TextSetting().TextBox!.Picker.Should().Be(PickerKind.None);
        (TextSetting().TextBox! with { Picker = PickerKind.Folder }).Picker.Should().Be(PickerKind.Folder);
    }
}
