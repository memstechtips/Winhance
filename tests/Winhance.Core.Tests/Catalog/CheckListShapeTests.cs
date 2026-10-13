using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.Core.Tests.Catalog;

public class CheckListShapeTests
{
    private static Setting CheckListSetting() => new()
    {
        Id = "a",
        Display = new() { Name = TestKeys.Of("n"), Description = TestKeys.Of("d") },
        Targets = [new AutounattendMedia("editions")],
        CheckList = new(CheckListSource.MediaEditions),
    };

    [Fact]
    public void A_check_list_derives_a_check_list_card()
    {
        CheckListSetting().Control.Should().Be(ControlKind.CheckList);
    }

    [Fact]
    public void A_media_setting_is_answer_file_only()
    {
        CheckListSetting().IsAnswerFileOnly.Should().BeTrue();
    }

    [Fact]
    public void A_check_list_passes_the_validator()
    {
        CatalogValidator.Validate(CheckListSetting()).Should().BeEmpty();
    }

    [Fact]
    public void A_check_list_that_also_writes_the_answer_file_is_rejected()
    {
        var setting = CheckListSetting() with
        {
            Targets = [new AutounattendMedia("editions"), new AutounattendElement("x", "oobeSystem", "Microsoft-Windows-Shell-Setup", "X")],
        };

        CatalogValidator.Validate(setting).Should().NotBeEmpty();
    }

    [Fact]
    public void A_check_list_with_states_is_rejected()
    {
        var setting = CheckListSetting() with
        {
            States = [new() { Label = TestKeys.Of("s"), Set = new Dictionary<string, StateValue> { ["editions"] = StateValue.Of("x") } }],
        };

        CatalogValidator.Validate(setting).Should().Contain(e => e.Message.Contains("may not declare states"));
    }

    [Fact]
    public void The_ticked_keys_round_trip_through_the_config_file()
    {
        var setting = CheckListSetting();
        var item = new ConfigurationItem { Id = setting.Id };

        ConfigFileMapper.WriteValue(item, setting, new ChoiceValue.Keys(["Windows 11 Pro", "Windows 11 Home"]));

        ConfigFileMapper.InputTypeFor(setting).Should().Be(InputType.CheckList);
        item.SelectedKeys.Should().Equal("Windows 11 Pro", "Windows 11 Home");
        ConfigFileMapper.DecodeValue(setting, item).Should().Be(new ChoiceValue.Keys(["Windows 11 Pro", "Windows 11 Home"]));
    }

    [Fact]
    public void A_file_with_no_ticks_saved_reads_as_no_choice()
    {
        ConfigFileMapper.DecodeValue(CheckListSetting(), new ConfigurationItem { Id = "a" }).Should().BeNull();
    }

    [Fact]
    public void Two_check_lists_with_the_same_ticks_are_equal()
    {
        new ChoiceValue.Keys(["a", "b"]).Should().Be(new ChoiceValue.Keys(["a", "b"]));
        new ChoiceValue.Keys(["a", "b"]).GetHashCode().Should().Be(new ChoiceValue.Keys(["a", "b"]).GetHashCode());
        new ChoiceValue.Keys(["a"]).Should().NotBe(new ChoiceValue.Keys(["a", "b"]));
    }
}
