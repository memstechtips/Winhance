using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Selections;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

// A filter toggle or language change during Builder recreates every setting ViewModel from live state while
// the recorded edits survive and are still written on Save - screen and file disagreed silently, and each half
// was individually correct. The round-trips drive the REAL recording path (TrySetToRecommended -> the write
// strategy) and the real restore path.
public class SettingItemViewModelAuthoredOverlayTests
{
    private readonly Mock<ISettingApplicationService> _applyService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<IApplicationModeService> _modeService = new();

    private readonly Dictionary<string, SettingChoice> _authored = new();

    public SettingItemViewModelAuthoredOverlayTests()
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

    private static SettingItemViewModelConfig Config(Setting setting, InputType inputType, bool isSelected = false) =>
        new()
        {
            Setting = setting,
            SettingId = setting.Id,
            Name = setting.Display.Name,
            Description = setting.Display.Description,
            InputType = inputType,
            IsSelected = isSelected,
        };

    private static Setting ToggleSetting(string id) => new()
    {
        Id = id,
        Display = new() { Name = id, Description = "" },
    };

    // Without a WindowsDefault or Recommended role, HasQuickSetTarget is false and the command silently no-ops, so
    // the round-trip would prove nothing.
    private static Setting ToggleSettingWithRoles(string id, bool recommendedEnabled, bool defaultEnabled)
    {
        var enabled = new List<StateRole>();
        var disabled = new List<StateRole>();
        (recommendedEnabled ? enabled : disabled).Add(StateRole.Recommended);
        (defaultEnabled ? enabled : disabled).Add(StateRole.WindowsDefault);

        return new Setting
        {
            Id = id,
            Display = new() { Name = id, Description = "" },
            States = new[]
            {
                new SettingState { Label = "Enabled", Roles = enabled },
                new SettingState { Label = "Disabled", Roles = disabled },
            },
        };
    }

    private static Setting NumericSetting(string id, string? units = null, int recommended = 90) => new()
    {
        Id = id,
        Display = new() { Name = id, Description = "" },
        Numeric = new()
        {
            Min = 0,
            Max = 100,
            Units = units,
            Recommended = new[] { new ContextValue(PowerContext.Always, recommended) },
            WindowsDefault = new[] { new ContextValue(PowerContext.Always, 10) },
        },
    };

    // IsPowerPlanSetting keys off OptionSource alone, and the recorded choice reads the GUID off the option Tag,
    // so a stub source is enough here.
    private static Setting PowerPlanSetting(string id) => new()
    {
        Id = id,
        Display = new() { Name = id, Description = "" },
        OptionSource = new Mock<IDynamicOptionSource>().Object,
    };

    // What SettingsLoadingService.BuildBuilderPowerPlanOptions hands the factory: index-valued options carrying
    // the plan GUID on the Tag, DisplayName left as the raw loc key. The order is a parameter so a reload can
    // present the plans in a different order than the one the user authored against.
    private static void AddBuilderPowerPlanOptions(SettingItemViewModel vm, bool balancedFirst = false)
    {
        var high = new PowerPlanComboBoxOption { DisplayName = "PowerPlan_HighPerformance", Guid = "g-high", ExistsOnSystem = true };
        var balanced = new PowerPlanComboBoxOption { DisplayName = "PowerPlan_Balanced", Guid = "g-bal", ExistsOnSystem = true };
        var ordered = balancedFirst ? new[] { balanced, high } : new[] { high, balanced };
        for (int i = 0; i < ordered.Length; i++)
            vm.ComboBoxOptions.Add(new ComboBoxDisplayOption(ordered[i].DisplayName, i, "Installed on system", ordered[i] with { Index = i }));
    }

    private static SettingStateResult LiveState(bool isEnabled = false, object? currentValue = null) => new()
    {
        Success = true,
        IsEnabled = isEnabled,
        CurrentValue = currentValue,
        Outcome = SettingDetectionOutcome.Resolved,
    };

    [Fact]
    public void ARefreshFromLiveState_DoesNotOverwriteAnAuthoredToggle()
    {
        var sut = CreateSut(Config(ToggleSetting("authored-toggle"), InputType.Toggle));
        _authored["authored-toggle"] = new SettingChoice("authored-toggle", new ChoiceValue.Toggle(true));

        sut.UpdateStateFromSystemState(LiveState(isEnabled: false));

        sut.IsSelected.Should().BeTrue(
            because: "the card must show what Save will write, not what the machine currently says");
    }

    [Fact]
    public void ARefreshFromLiveState_DoesNotOverwriteAnAuthoredNumericValue()
    {
        var sut = CreateSut(Config(NumericSetting("authored-numeric"), InputType.NumericRange));
        _authored["authored-numeric"] = new SettingChoice("authored-numeric", new ChoiceValue.Number(42));

        sut.UpdateStateFromSystemState(LiveState(currentValue: 7));

        sut.NumericValue.Should().Be(42);
    }

    [Fact]
    public void ARefreshFromLiveState_AppliesLiveValuesForSettingsTheUserNeverAuthored()
    {
        var sut = CreateSut(Config(ToggleSetting("untouched"), InputType.Toggle));

        sut.UpdateStateFromSystemState(LiveState(isEnabled: true));

        sut.IsSelected.Should().BeTrue(
            because: "the overlay must only defend authored settings, never freeze the rest");
    }

    [Fact]
    public void AfterARefresh_TheCardAndTheSavedEditStillAgree()
    {
        var sut = CreateSut(Config(NumericSetting("agree", units: null), InputType.NumericRange));
        sut.TrySetToRecommended();

        int shownBeforeRefresh = sut.NumericValue;
        sut.UpdateStateFromSystemState(LiveState(currentValue: 3));

        var saved = _modeService.Object.GetBuilderEdits().Single(e => e.SettingId == "agree");

        sut.NumericValue.Should().Be(shownBeforeRefresh);
        sut.NumericValue.Should().Be(saved.Value.Should().BeOfType<ChoiceValue.Number>().Which.Value,
            because: "the screen and the file are the same fact read from the same store");
    }

    [Fact]
    public void RoundTrip_Toggle()
    {
        // Live state is off; the recommended state is on, so the quick-set authors a real change.
        var setting = ToggleSettingWithRoles("rt-toggle", recommendedEnabled: true, defaultEnabled: false);
        var authoring = CreateSut(Config(setting, InputType.Toggle, isSelected: false));

        authoring.TrySetToRecommended().Should().BeTrue();
        authoring.IsSelected.Should().BeTrue();
        _authored.Should().ContainKey("rt-toggle");

        var reloaded = CreateSut(Config(setting, InputType.Toggle, isSelected: false));
        reloaded.ApplyAuthoredOverlay();

        reloaded.IsSelected.Should().Be(authoring.IsSelected);
    }

    [Fact]
    public void RoundTrip_NumericRange()
    {
        var setting = NumericSetting("rt-numeric");
        var authoring = CreateSut(Config(setting, InputType.NumericRange));
        authoring.TrySetToRecommended();

        var reloaded = CreateSut(Config(setting, InputType.NumericRange));
        reloaded.NumericValue = 0;
        reloaded.ApplyAuthoredOverlay();

        reloaded.NumericValue.Should().Be(authoring.NumericValue).And.Be(90);
    }

    [Fact]
    public void RoundTrip_NumericRange_SurvivesUnitConversion()
    {
        // The record holds SYSTEM units and the slider reads DISPLAY units. A restore that skipped
        // the conversion would look right for unitless settings and be wrong by 60x here.
        var setting = NumericSetting("rt-minutes", units: "Minutes", recommended: 15);
        var authoring = CreateSut(Config(setting, InputType.NumericRange));
        authoring.TrySetToRecommended();

        var saved = _authored["rt-minutes"];
        saved.Value.Should().BeOfType<ChoiceValue.Number>().Which.Value.Should().NotBe(authoring.NumericValue,
            because: "this fixture's display and system units genuinely differ - otherwise the test proves nothing");

        var reloaded = CreateSut(Config(setting, InputType.NumericRange));
        reloaded.NumericValue = 0;
        reloaded.ApplyAuthoredOverlay();

        reloaded.NumericValue.Should().Be(authoring.NumericValue);
    }

    [Fact]
    public void OutsideAnAuthoringMode_TheOverlayIsANoOp()
    {
        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.Normal);
        _authored["stale"] = new SettingChoice("stale", new ChoiceValue.Toggle(true));

        var sut = CreateSut(Config(ToggleSetting("stale"), InputType.Toggle));
        sut.UpdateStateFromSystemState(LiveState(isEnabled: false));

        sut.IsSelected.Should().BeFalse(
            because: "Normal mode shows the machine; an edit lingering in the store must never reach the card");
    }

    [Fact]
    public void InConfigReview_TheOverlayIsANoOp()
    {
        _modeService.SetupGet(m => m.CurrentMode).Returns(WinhanceMode.ConfigReview);
        _authored["reviewed"] = new SettingChoice("reviewed", new ChoiceValue.Toggle(true));

        var sut = CreateSut(Config(ToggleSetting("reviewed"), InputType.Toggle));
        sut.UpdateStateFromSystemState(LiveState(isEnabled: false));

        sut.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void ApplyingTheOverlay_DoesNotRecordASecondEdit()
    {
        var sut = CreateSut(Config(ToggleSetting("no-echo"), InputType.Toggle));
        _authored["no-echo"] = new SettingChoice("no-echo", new ChoiceValue.Toggle(true));

        sut.ApplyAuthoredOverlay();

        _modeService.Verify(m => m.RecordBuilderEdit(It.IsAny<SettingChoice>()), Times.Never,
            failMessage: "restoring the user's own value must not re-enter the input handlers");
    }

    [Fact]
    public void RoundTrip_PowerPlan()
    {
        // The Builder dropdown is index-valued, but an index means nothing to the machine the config is applied
        // to - the recorded choice has to be the plan GUID, read off the option Tag, with the human name beside it.
        _localizationService.Setup(l => l.GetString("PowerPlan_Balanced")).Returns("Balanced");
        var setting = PowerPlanSetting("rt-power-plan");

        var authoring = CreateSut(Config(setting, InputType.Selection));
        AddBuilderPowerPlanOptions(authoring);
        authoring.ApplySelectionValue(1);

        _authored["rt-power-plan"].Value.Should().Be(new ChoiceValue.PowerPlan("g-bal", "Balanced"));

        // Reloaded with the plans in the other order: only a GUID lookup lands on Balanced again.
        var reloaded = CreateSut(Config(setting, InputType.Selection));
        AddBuilderPowerPlanOptions(reloaded, balancedFirst: true);
        reloaded.ApplyAuthoredOverlay();

        reloaded.SelectedValue.Should().Be(0);
        reloaded.Outcome.Should().Be(SettingDetectionOutcome.Resolved);
    }
}
