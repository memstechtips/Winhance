using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class SettingItemViewModelKeyedSelectionTests
{
    private readonly Mock<ISettingApplicationService> _applyService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<IApplicationModeService> _modeService = new();

    private readonly Dictionary<string, SettingChoice> _authored = new();

    public SettingItemViewModelKeyedSelectionTests()
    {
        _localizationService.Setup(l => l.GetString(It.IsAny<string>())).Returns((string k) => k);
        _localizationService.MirrorTryGetString();

        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _modeService.Setup(m => m.RecordBuilderEdit(It.IsAny<SettingChoice>()))
            .Callback<SettingChoice>(e => _authored[e.SettingId] = e);
        _modeService.Setup(m => m.GetBuilderEdit(It.IsAny<string>()))
            .Returns<string>(id => _authored.TryGetValue(id, out var e) ? e : null);
        _modeService.Setup(m => m.GetBuilderEdits())
            .Returns(() => _authored.Values.ToList());
        _modeService.Setup(m => m.IsIncluded(It.IsAny<string>())).Returns(true);
    }

    private SettingItemViewModel CreateSut(SettingItemViewModelConfig config) =>
        new(
            config,
            SettingWriteStrategies.Selector(
                _applyService.Object, _dialogService.Object, _localizationService.Object,
                _logService.Object, _modeService.Object),
            _logService.Object,
            _dispatcherService.Object,
            _dialogService.Object,
            _localizationService.Object,
            null,
            null,
            null,
            _modeService.Object);

    private static SettingItemViewModelConfig Config(Setting setting, InputType inputType) =>
        new()
        {
            Setting = setting,
            SettingId = setting.Id,
            Name = setting.Display.Name.Value,
            Description = setting.Display.Description.Value,
            InputType = inputType,
        };

    private static SettingStateResult KeyedState(string selected) => FakeOptionProvider.StateOn(selected);

    private SettingItemViewModel KeyedCard()
    {
        var sut = CreateSut(Config(FakeOptionProvider.SettingFor(), InputType.Selection));
        sut.TryApplyKeyedOptions(KeyedState("beta"));
        return sut;
    }

    public static TheoryData<OptionSource> SourcesOtherThanPowerPlans()
    {
        var data = new TheoryData<OptionSource>();
        foreach (var source in Enum.GetValues<OptionSource>())
        {
            if (source != OptionSource.PowerPlans)
                data.Add(source);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(SourcesOtherThanPowerPlans))]
    public void An_option_list_other_than_power_plans_shows_no_status(OptionSource source)
    {
        var sut = CreateSut(Config(FakeOptionProvider.SettingFor() with { Options = new(source) }, InputType.Selection));
        sut.TryApplyKeyedOptions(KeyedState("beta"));

        sut.ShowsOptionStatus.Should().BeFalse(
            because: "only a power plan list offers an option the machine does not have, so an installed dot would say nothing");
    }

    [Fact]
    public void A_power_plan_list_shows_status()
    {
        var offered = new[]
        {
            new DynamicOption("Balanced", "g-bal", ExistsOnSystem: true, CanDelete: true),
            new DynamicOption("Ultimate Performance", "g-ult", ExistsOnSystem: false),
        };
        var sut = CreateSut(Config(
            FakeOptionProvider.SettingFor("power-plan-card") with { Options = new(OptionSource.PowerPlans) },
            InputType.Selection));

        sut.TryApplyKeyedOptions(new SettingStateResult
        {
            Success = true,
            CurrentValue = 0,
            Outcome = SettingDetectionOutcome.Resolved,
            DynamicOptions = offered,
            DynamicSelection = "g-bal",
        }).Should().BeTrue();

        sut.ShowsOptionStatus.Should().BeTrue();
        // The control draws the dot and the trash button off the option itself, so each row has to carry it.
        sut.ComboBoxOptions.Select(o => o.Tag).Should().Equal(offered[0], offered[1]);
    }

    [Fact]
    public void The_dropdown_is_the_sources_options_labelled_as_the_machine_gave_them()
    {
        var sut = KeyedCard();

        sut.ComboBoxOptions.Select(o => o.DisplayText).Should().Equal("Alpha zone", "Beta zone", "Gamma zone");
        sut.ComboBoxOptions.Select(o => o.Value).Should().Equal("alpha", "beta", "gamma");
    }

    [Fact]
    public void The_selected_value_is_the_key_and_the_combo_box_selects_it_by_position()
    {
        var sut = KeyedCard();

        sut.SelectedValue.Should().Be("beta");
        sut.ComboIndexForMode(SettingInputMode.Single).Should().Be(1);
    }

    [Fact]
    public void A_rebuild_re_enumerates_the_source_rather_than_reusing_the_first_list()
    {
        var sut = KeyedCard();

        sut.TryApplyKeyedOptions(new SettingStateResult
        {
            Success = true,
            CurrentValue = 0,
            Outcome = SettingDetectionOutcome.Resolved,
            DynamicOptions = [new DynamicOption("Zone alpha", "alpha")],
            DynamicSelection = "alpha",
        });

        sut.ComboBoxOptions.Should().ContainSingle().Which.DisplayText.Should().Be("Zone alpha");
        sut.SelectedValue.Should().Be("alpha");
    }

    [Fact]
    public void An_authored_key_the_machine_does_not_offer_stays_selected_and_is_marked_unavailable()
    {
        _localizationService.PresentKey("Setting_KeyedOption_Unavailable", "{0} (not available on this PC)");
        var sut = KeyedCard();
        _authored["fake-keyed"] = new SettingChoice("fake-keyed", new ChoiceValue.Keyed("delta", "Delta zone"));

        sut.UpdateStateFromSystemState(KeyedState("beta"));

        sut.SelectedValue.Should().Be("delta", because: "Save must still write the key the user authored");
        sut.ComboBoxOptions.Should().HaveCount(4);
        sut.ComboBoxOptions[3].Value.Should().Be("delta");
        sut.ComboBoxOptions[3].DisplayText.Should().Be("delta (not available on this PC)");
        sut.ComboIndexForMode(SettingInputMode.Single).Should().Be(3);
    }

    [Fact]
    public void A_refresh_onto_the_same_key_still_tells_the_card_to_reselect()
    {
        var sut = KeyedCard();
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.UpdateStateFromSystemState(KeyedState("beta"));

        raised.Should().Contain(nameof(SettingItemViewModel.ComboBoxOptions),
            because: "SelectedValue is unchanged and raises nothing, so nothing else re-pushes the index the cleared ItemsSource threw away");
        sut.ComboIndexForMode(SettingInputMode.Single).Should().Be(1);
    }

    [Fact]
    public void Choosing_an_option_records_the_key_and_the_label_it_showed()
    {
        var sut = KeyedCard();

        sut.ApplySelectionValue("gamma");

        _authored.Should().ContainKey("fake-keyed");
        _authored["fake-keyed"].Value.Should().Be(new ChoiceValue.Keyed("gamma", "Gamma zone"));
    }

    [Fact]
    public void The_live_key_is_remembered_apart_from_an_authored_pick()
    {
        var sut = KeyedCard();

        sut.ApplySelectionValue("gamma");

        sut.SelectedValue.Should().Be("gamma");
        sut.LiveKeyedSelection.Should().Be("beta",
            because: "the Active badge names what the machine is on, not what Builder authored over it");
    }

    [Fact]
    public void A_detection_that_read_no_options_keeps_the_last_live_key()
    {
        var sut = KeyedCard();

        sut.UpdateStateFromSystemState(new SettingStateResult
        {
            Success = true,
            CurrentValue = 0,
            Outcome = SettingDetectionOutcome.Resolved,
            DynamicOptions = null,
            DynamicSelection = null,
        });

        sut.LiveKeyedSelection.Should().Be("beta",
            because: "a refresh that read nothing is not evidence the machine moved off it");
    }

    [Fact]
    public void The_per_item_words_come_from_the_shared_power_plan_keys()
    {
        var sut = KeyedCard();

        sut.KeyedActiveBadgeText.Should().Be("PowerPlan_Active_Badge");
        sut.KeyedDeleteTooltipText.Should().Be("PowerPlan_Delete_Tooltip");
        sut.KeyedInstalledTooltipText.Should().Be("PowerPlan_Status_Exists");
        sut.KeyedNotInstalledTooltipText.Should().Be("PowerPlan_Status_NotExists");
    }

    [Fact]
    public void A_detection_that_read_no_options_leaves_the_card_on_its_key()
    {
        var sut = KeyedCard();

        sut.UpdateStateFromSystemState(new SettingStateResult
        {
            Success = true,
            CurrentValue = 0,
            Outcome = SettingDetectionOutcome.Resolved,
            DynamicOptions = null,
            DynamicSelection = null,
        });

        sut.SelectedValue.Should().Be("beta", because: "the placeholder index is not a key and must not become one");
        sut.ComboIndexForMode(SettingInputMode.Single).Should().Be(1);
    }

    [Fact]
    public void A_detection_that_read_an_empty_list_empties_the_dropdown()
    {
        var sut = KeyedCard();

        sut.UpdateStateFromSystemState(new SettingStateResult
        {
            Success = true,
            CurrentValue = 0,
            Outcome = SettingDetectionOutcome.Resolved,
            DynamicOptions = [],
            DynamicSelection = null,
        });

        sut.ComboBoxOptions.Should().BeEmpty();
        sut.LiveKeyedSelection.Should().BeNull(because: "nothing is offered, so nothing is live");
        sut.SelectedValue.Should().Be("beta", because: "an empty list is no more a key than the placeholder index is");
    }

    [Fact]
    public void A_keyed_card_names_its_name_and_description_and_nothing_else()
    {
        var setting = FakeOptionProvider.SettingFor();

        setting.States.Should().BeEmpty();
        setting.Display.GroupName.Should().BeNull();
    }
}
