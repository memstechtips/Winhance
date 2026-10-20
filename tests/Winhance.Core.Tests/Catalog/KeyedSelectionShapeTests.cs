using FluentAssertions;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Selections;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Core.Tests.Catalog;

public class KeyedSelectionShapeTests
{
    [Fact]
    public void An_option_list_derives_the_keyed_selection_kind()
    {
        FakeOptionProvider.SettingFor().Control.Should().Be(ControlKind.KeyedSelection);
    }

    [Fact]
    public void The_power_plan_derives_the_keyed_selection_kind()
    {
        // It has no states, so without its option list it would derive an Action button.
        SettingCatalog.Find("power-plan-selection")!.Control.Should().Be(ControlKind.KeyedSelection);
    }

    [Fact]
    public void A_keyed_selection_is_a_selection_input_in_the_config_file()
    {
        ConfigFileMapper.InputTypeFor(FakeOptionProvider.SettingFor()).Should().Be(InputType.Selection);
    }

    [Fact]
    public void The_fake_provider_offers_three_options_and_reports_the_registry_selection()
    {
        var setting = FakeOptionProvider.SettingFor();
        var context = new FakeDetectionContext()
            .Set(FakeOptionProvider.ValuePath, FakeOptionProvider.ValueName, "beta");
        var provider = new FakeOptionProvider();

        provider.Options(setting, context).Select(o => o.Value).Should().Equal("alpha", "beta", "gamma");
        provider.CurrentKey(setting, context).Should().Be("beta");
    }

    [Fact]
    public void The_fake_setting_writes_the_key_over_its_target()
    {
        var set = KeyedOptions.SetFor(FakeOptionProvider.SettingFor(), "gamma", new FakeOptionProvider());

        set.Should().NotBeNull();
        set![FakeOptionProvider.TargetKey].WritePayload.Should().Be("gamma");
    }

    [Fact]
    public void The_fake_setting_writes_nothing_for_a_key_the_provider_does_not_offer()
    {
        KeyedOptions.SetFor(FakeOptionProvider.SettingFor(), "delta", new FakeOptionProvider()).Should().BeNull();
    }

    [Fact]
    public void A_keyed_choice_carries_the_key_and_the_label_it_was_saved_under()
    {
        var choice = new ChoiceValue.Keyed("beta", "Beta zone");

        choice.Key.Should().Be("beta");
        choice.Label.Should().Be("Beta zone");
    }
}
