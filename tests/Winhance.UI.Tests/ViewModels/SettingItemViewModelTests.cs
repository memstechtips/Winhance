using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class SettingItemViewModelTests : IDisposable
{
    private readonly Mock<ISettingApplicationService> _mockSettingApplicationService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<IUserPreferencesService> _mockUserPreferencesService = new();
    private readonly Mock<IRegeditLauncher> _mockRegeditLauncher = new();

    private readonly SettingDefinition _defaultSettingDefinition;
    private readonly SettingItemViewModelConfig _defaultConfig;

    public SettingItemViewModelTests()
    {
        // Set up dispatcher to execute actions synchronously
        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(f => f());

        // Default localization returns null so fallbacks are used
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => null!);

        _defaultSettingDefinition = new SettingDefinition
        {
            Id = "test-setting",
            Name = "Test Setting",
            Description = "A test setting description",
            InputType = InputType.Toggle
        };

        _defaultConfig = new SettingItemViewModelConfig
        {
            SettingDefinition = _defaultSettingDefinition,
            SettingId = "test-setting",
            Name = "Test Setting",
            Description = "A test setting description",
            InputType = InputType.Toggle,
            IsSelected = false,
            GroupName = "Test Group",
            Icon = "TestIcon",
            IconPack = "Material"
        };
    }

    private SettingItemViewModel CreateSut(SettingItemViewModelConfig? config = null)
    {
        return new SettingItemViewModel(
            config ?? _defaultConfig,
            _mockSettingApplicationService.Object,
            _mockLogService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockEventBus.Object,
            _mockUserPreferencesService.Object,
            _mockRegeditLauncher.Object);
    }

    public void Dispose()
    {
        // Intentionally empty; individual tests dispose their SUT as needed.
    }

    // ── Constructor / Initialization ──

    [Fact]
    public void Constructor_InitializesPropertiesFromConfig()
    {
        var sut = CreateSut();

        sut.SettingId.Should().Be("test-setting");
        sut.Name.Should().Be("Test Setting");
        sut.Description.Should().Be("A test setting description");
        sut.GroupName.Should().Be("Test Group");
        sut.Icon.Should().Be("TestIcon");
        sut.IconPack.Should().Be("Material");
        sut.InputType.Should().Be(InputType.Toggle);
        sut.IsSelected.Should().BeFalse();
        sut.SettingDefinition.Should().BeSameAs(_defaultSettingDefinition);
    }

    [Fact]
    public void Constructor_InitializesDefaults()
    {
        var sut = CreateSut();

        sut.Status.Should().BeEmpty();
        sut.ComboBoxOptions.Should().NotBeNull().And.BeEmpty();
        sut.MaxValue.Should().Be(100);
        sut.Units.Should().BeEmpty();
        sut.TechnicalDetails.Should().NotBeNull().And.BeEmpty();
        sut.IsVisible.Should().BeTrue();
        sut.IsEnabled.Should().BeTrue();
        sut.ParentIsEnabled.Should().BeTrue();
        sut.IsApplying.Should().BeFalse();
    }

    [Fact]
    public void Constructor_SetsOnOffTextFromConfig()
    {
        var config = _defaultConfig with { OnText = "Enable", OffText = "Disable" };
        var sut = CreateSut(config);

        sut.OnText.Should().Be("Enable");
        sut.OffText.Should().Be("Disable");
    }

    [Fact]
    public void Constructor_SetsActionButtonTextFromConfig()
    {
        var config = _defaultConfig with { ActionButtonText = "Run" };
        var sut = CreateSut(config);

        sut.ActionButtonText.Should().Be("Run");
    }

    // ── Property Binding / Computed Properties ──

    [Fact]
    public void IsToggleType_ReturnsTrueForToggleInputType()
    {
        var sut = CreateSut();

        sut.IsToggleType.Should().BeTrue();
        sut.IsSelectionType.Should().BeFalse();
        sut.IsNumericType.Should().BeFalse();
        sut.IsActionType.Should().BeFalse();
        sut.IsCheckBoxType.Should().BeFalse();
    }

    [Fact]
    public void IsSelectionType_ReturnsTrueForSelectionInputType()
    {
        var config = _defaultConfig with { InputType = InputType.Selection };
        var sut = CreateSut(config);

        sut.IsSelectionType.Should().BeTrue();
        sut.IsToggleType.Should().BeFalse();
    }

    [Fact]
    public void IsNumericType_ReturnsTrueForNumericRangeInputType()
    {
        var config = _defaultConfig with { InputType = InputType.NumericRange };
        var sut = CreateSut(config);

        sut.IsNumericType.Should().BeTrue();
        sut.IsToggleType.Should().BeFalse();
    }

    [Fact]
    public void IsActionType_ReturnsTrueForActionInputType()
    {
        var config = _defaultConfig with { InputType = InputType.Action };
        var sut = CreateSut(config);

        sut.IsActionType.Should().BeTrue();
    }

    [Fact]
    public void IsCheckBoxType_ReturnsTrueForCheckBoxInputType()
    {
        var config = _defaultConfig with { InputType = InputType.CheckBox };
        var sut = CreateSut(config);

        sut.IsCheckBoxType.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, true, true, false)]
    public void EffectiveIsEnabled_CombinesIsEnabledAndParentIsEnabledAndReviewMode(
        bool isEnabled, bool parentIsEnabled, bool isInReviewMode, bool expected)
    {
        var sut = CreateSut();
        sut.IsEnabled = isEnabled;
        sut.ParentIsEnabled = parentIsEnabled;
        sut.IsInReviewMode = isInReviewMode;

        sut.EffectiveIsEnabled.Should().Be(expected);
    }

    [Fact]
    public void HasStatusBanner_ReturnsTrueWhenStatusBannerMessageIsSet()
    {
        var sut = CreateSut();

        sut.HasStatusBanner.Should().BeFalse();

        sut.StatusBannerMessage = "Some warning";
        sut.HasStatusBanner.Should().BeTrue();
    }

    [Fact]
    public void HasStatusBanner_ReturnsFalseWhenStatusBannerMessageIsCleared()
    {
        var sut = CreateSut();
        sut.StatusBannerMessage = "Some warning";
        sut.HasStatusBanner.Should().BeTrue();

        sut.StatusBannerMessage = null;
        sut.HasStatusBanner.Should().BeFalse();
    }

    [Fact]
    public void HasTechnicalDetails_ReturnsFalseWhenEmpty()
    {
        var sut = CreateSut();
        sut.HasTechnicalDetails.Should().BeFalse();
    }

    [Fact]
    public void IsSubSetting_ReturnsTrueWhenParentSettingIdIsSet()
    {
        var settingDef = new SettingDefinition
        {
            Id = "child-setting",
            Name = "Child",
            Description = "Child setting",
            ParentSettingId = "parent-setting"
        };
        var config = _defaultConfig with
        {
            SettingDefinition = settingDef,
            SettingId = "child-setting"
        };
        var sut = CreateSut(config);

        sut.IsSubSetting.Should().BeTrue();
    }

    [Fact]
    public void IsSubSetting_ReturnsFalseWhenParentSettingIdIsNull()
    {
        var sut = CreateSut();
        sut.IsSubSetting.Should().BeFalse();
    }

    // ── Visibility / Search Filtering ──

    [Fact]
    public void UpdateVisibility_EmptySearch_MakesVisible()
    {
        var sut = CreateSut();
        sut.IsVisible = false;

        sut.UpdateVisibility("");

        sut.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void UpdateVisibility_WhitespaceSearch_MakesVisible()
    {
        var sut = CreateSut();
        sut.IsVisible = false;

        sut.UpdateVisibility("   ");

        sut.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void UpdateVisibility_MatchingName_MakesVisible()
    {
        var sut = CreateSut();

        sut.UpdateVisibility("Test");

        sut.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void UpdateVisibility_MatchingDescription_MakesVisible()
    {
        var sut = CreateSut();

        sut.UpdateVisibility("description");

        sut.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void UpdateVisibility_MatchingGroupName_MakesVisible()
    {
        var sut = CreateSut();

        sut.UpdateVisibility("Group");

        sut.IsVisible.Should().BeTrue();
    }

    [Fact]
    public void UpdateVisibility_NonMatchingSearch_HidesItem()
    {
        var sut = CreateSut();

        sut.UpdateVisibility("zzz_nonexistent");

        sut.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void UpdateVisibility_IsCaseInsensitive()
    {
        var sut = CreateSut();

        sut.UpdateVisibility("test setting");

        sut.IsVisible.Should().BeTrue();
    }

    // ── UpdateStateFromEvent ──

    [Fact]
    public void UpdateStateFromEvent_ToggleType_UpdatesIsSelected()
    {
        var sut = CreateSut();
        sut.IsSelected.Should().BeFalse();

        sut.UpdateStateFromEvent(true, null);

        sut.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void UpdateStateFromEvent_CheckBoxType_UpdatesIsSelected()
    {
        var config = _defaultConfig with { InputType = InputType.CheckBox };
        var sut = CreateSut(config);

        sut.UpdateStateFromEvent(true, null);

        sut.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void UpdateStateFromEvent_SelectionType_UpdatesSelectedValue()
    {
        var config = _defaultConfig with { InputType = InputType.Selection };
        var sut = CreateSut(config);

        sut.UpdateStateFromEvent(true, "OptionA");

        sut.SelectedValue.Should().Be("OptionA");
    }

    [Fact]
    public void UpdateStateFromEvent_NumericType_UpdatesNumericValue()
    {
        var config = _defaultConfig with { InputType = InputType.NumericRange };
        var sut = CreateSut(config);

        sut.UpdateStateFromEvent(true, 42);

        sut.NumericValue.Should().Be(42);
    }

    // ── UpdateStateFromSystemState ──

    [Fact]
    public void UpdateStateFromSystemState_ToggleType_UpdatesIsSelected()
    {
        var sut = CreateSut();
        var state = new SettingStateResult { Success = true, IsEnabled = true };

        sut.UpdateStateFromSystemState(state);

        sut.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void UpdateStateFromSystemState_FailedResult_DoesNotUpdate()
    {
        var sut = CreateSut();
        sut.IsSelected = true;
        var state = new SettingStateResult { Success = false, IsEnabled = false };

        sut.UpdateStateFromSystemState(state);

        sut.IsSelected.Should().BeTrue(); // unchanged
    }

    [Fact]
    public void UpdateStateFromSystemState_SelectionType_UpdatesSelectedValue()
    {
        var config = _defaultConfig with { InputType = InputType.Selection };
        var sut = CreateSut(config);
        var state = new SettingStateResult { Success = true, CurrentValue = "ValueB" };

        sut.UpdateStateFromSystemState(state);

        sut.SelectedValue.Should().Be("ValueB");
    }

    [Fact]
    public void UpdateStateFromSystemState_NumericType_UpdatesNumericValue()
    {
        var config = _defaultConfig with { InputType = InputType.NumericRange };
        var sut = CreateSut(config);
        var state = new SettingStateResult { Success = true, CurrentValue = 75 };

        sut.UpdateStateFromSystemState(state);

        sut.NumericValue.Should().Be(75);
    }

    // ── UpdateStateFromSystemState: NumericRange unit conversion ──

    [Fact]
    public void UpdateStateFromSystemState_NumericRange_WithMinuteUnits_ConvertsSecondsToMinutes()
    {
        var settingDef = new SettingDefinition
        {
            Id = "power-timeout",
            Name = "Power Timeout",
            Description = "Timeout setting",
            InputType = InputType.NumericRange,
            NumericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 120, Units = "Minutes" }
        };
        var config = _defaultConfig with
        {
            SettingDefinition = settingDef,
            SettingId = "power-timeout",
            InputType = InputType.NumericRange
        };
        var sut = CreateSut(config);

        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, CurrentValue = 1200 });

        sut.NumericValue.Should().Be(20); // 1200 seconds / 60 = 20 minutes
    }

    [Fact]
    public void UpdateStateFromSystemState_NumericRange_WithHourUnits_ConvertsSecondsToHours()
    {
        var settingDef = new SettingDefinition
        {
            Id = "disk-timeout",
            Name = "Disk Timeout",
            Description = "Disk timeout setting",
            InputType = InputType.NumericRange,
            NumericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 24, Units = "Hours" }
        };
        var config = _defaultConfig with
        {
            SettingDefinition = settingDef,
            SettingId = "disk-timeout",
            InputType = InputType.NumericRange
        };
        var sut = CreateSut(config);

        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, CurrentValue = 7200 });

        sut.NumericValue.Should().Be(2); // 7200 seconds / 3600 = 2 hours
    }

    [Fact]
    public void UpdateStateFromSystemState_NumericRange_WithNullUnits_PassesValueThrough()
    {
        var settingDef = new SettingDefinition
        {
            Id = "raw-setting",
            Name = "Raw Setting",
            Description = "No unit conversion",
            InputType = InputType.NumericRange,
            NumericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 1000, Units = null }
        };
        var config = _defaultConfig with
        {
            SettingDefinition = settingDef,
            SettingId = "raw-setting",
            InputType = InputType.NumericRange
        };
        var sut = CreateSut(config);

        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, CurrentValue = 300 });

        sut.NumericValue.Should().Be(300);
    }

    [Fact]
    public void UpdateStateFromSystemState_NumericRange_ZeroValue_RemainsZero()
    {
        var settingDef = new SettingDefinition
        {
            Id = "zero-setting",
            Name = "Zero Setting",
            Description = "Zero value test",
            InputType = InputType.NumericRange,
            NumericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 120, Units = "Minutes" }
        };
        var config = _defaultConfig with
        {
            SettingDefinition = settingDef,
            SettingId = "zero-setting",
            InputType = InputType.NumericRange
        };
        var sut = CreateSut(config);

        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, CurrentValue = 0 });

        sut.NumericValue.Should().Be(0);
    }

    // ── UpdateStateFromSystemState: AC/DC separate value handling for NumericRange ──

    [Fact]
    public void UpdateStateFromSystemState_NumericRange_SeparateACDC_UpdatesBothValues()
    {
        var settingDef = new SettingDefinition
        {
            Id = "acdc-numeric",
            Name = "AC/DC Numeric",
            Description = "Separate AC/DC numeric",
            InputType = InputType.NumericRange,
            NumericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 120, Units = "Minutes" },
            PowerCfgSettings = new List<PowerCfgSetting>
            {
                new PowerCfgSetting { PowerModeSupport = PowerModeSupport.Separate, RecommendedValueAC = null, RecommendedValueDC = null, DefaultValueAC = null, DefaultValueDC = null }
            }
        };
        var config = _defaultConfig with
        {
            SettingDefinition = settingDef,
            SettingId = "acdc-numeric",
            InputType = InputType.NumericRange
        };
        var sut = CreateSut(config);

        var rawValues = new Dictionary<string, object?> { { "ACValue", 1200 }, { "DCValue", 600 } };
        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, RawValues = rawValues });

        sut.AcNumericValue.Should().Be(20); // 1200 / 60
        sut.DcNumericValue.Should().Be(10); // 600 / 60
    }

    [Fact]
    public void UpdateStateFromSystemState_NumericRange_SeparateACDC_MissingDCValue_OnlyUpdatesAC()
    {
        var settingDef = new SettingDefinition
        {
            Id = "acdc-ac-only",
            Name = "AC Only Numeric",
            Description = "Only AC value present",
            InputType = InputType.NumericRange,
            NumericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 120, Units = "Minutes" },
            PowerCfgSettings = new List<PowerCfgSetting>
            {
                new PowerCfgSetting { PowerModeSupport = PowerModeSupport.Separate, RecommendedValueAC = null, RecommendedValueDC = null, DefaultValueAC = null, DefaultValueDC = null }
            }
        };
        var config = _defaultConfig with
        {
            SettingDefinition = settingDef,
            SettingId = "acdc-ac-only",
            InputType = InputType.NumericRange
        };
        var sut = CreateSut(config);
        sut.DcNumericValue = 99; // pre-set DC value

        var rawValues = new Dictionary<string, object?> { { "ACValue", 1200 } };
        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, RawValues = rawValues });

        sut.AcNumericValue.Should().Be(20); // 1200 / 60
        sut.DcNumericValue.Should().Be(99); // unchanged
    }

    [Fact]
    public void UpdateStateFromSystemState_NumericRange_SeparateACDC_NullRawValues_FallsBackToCurrentValue()
    {
        var settingDef = new SettingDefinition
        {
            Id = "acdc-fallback",
            Name = "ACDC Fallback",
            Description = "Null RawValues falls back",
            InputType = InputType.NumericRange,
            NumericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 120, Units = "Minutes" },
            PowerCfgSettings = new List<PowerCfgSetting>
            {
                new PowerCfgSetting { PowerModeSupport = PowerModeSupport.Separate, RecommendedValueAC = null, RecommendedValueDC = null, DefaultValueAC = null, DefaultValueDC = null }
            }
        };
        var config = _defaultConfig with
        {
            SettingDefinition = settingDef,
            SettingId = "acdc-fallback",
            InputType = InputType.NumericRange
        };
        var sut = CreateSut(config);

        // RawValues is null but CurrentValue is set — falls through to the else branch
        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, CurrentValue = 600, RawValues = null });

        sut.NumericValue.Should().Be(10); // 600 / 60 via fallback path
    }

    // ── UpdateStateFromSystemState: AC/DC separate value handling for Selection ──

    [Fact]
    public void UpdateStateFromSystemState_Selection_SeparateACDC_UpdatesBothIndices()
    {
        var settingDef = new SettingDefinition
        {
            Id = "acdc-selection",
            Name = "AC/DC Selection",
            Description = "Separate AC/DC selection",
            InputType = InputType.Selection,
            ComboBox = new ComboBoxMetadata
            {
                Options = new[]
                {
                    new Winhance.Core.Features.Common.Models.ComboBoxOption { DisplayName = "Option A", ValueMappings = new Dictionary<string, object?> { { "PowerCfgValue", 10 } } },
                    new Winhance.Core.Features.Common.Models.ComboBoxOption { DisplayName = "Option B", ValueMappings = new Dictionary<string, object?> { { "PowerCfgValue", 20 } } },
                    new Winhance.Core.Features.Common.Models.ComboBoxOption { DisplayName = "Option C", ValueMappings = new Dictionary<string, object?> { { "PowerCfgValue", 30 } } }
                }
            },
            PowerCfgSettings = new List<PowerCfgSetting>
            {
                new PowerCfgSetting { PowerModeSupport = PowerModeSupport.Separate, RecommendedValueAC = null, RecommendedValueDC = null, DefaultValueAC = null, DefaultValueDC = null }
            }
        };
        var config = _defaultConfig with
        {
            SettingDefinition = settingDef,
            SettingId = "acdc-selection",
            InputType = InputType.Selection
        };
        var sut = CreateSut(config);

        var rawValues = new Dictionary<string, object?> { { "ACValue", 30 }, { "DCValue", 10 } };
        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, RawValues = rawValues });

        sut.AcValue.Should().Be(2); // PowerCfgValue 30 maps to index 2
        sut.DcValue.Should().Be(0); // PowerCfgValue 10 maps to index 0
    }

    [Fact]
    public void UpdateStateFromSystemState_Selection_SeparateACDC_UnknownPowerCfgValue_DefaultsToZero()
    {
        var settingDef = new SettingDefinition
        {
            Id = "acdc-unknown",
            Name = "AC/DC Unknown Value",
            Description = "Unknown PowerCfg value defaults to 0",
            InputType = InputType.Selection,
            ComboBox = new ComboBoxMetadata
            {
                Options = new[]
                {
                    new Winhance.Core.Features.Common.Models.ComboBoxOption { DisplayName = "Option A", ValueMappings = new Dictionary<string, object?> { { "PowerCfgValue", 10 } } },
                    new Winhance.Core.Features.Common.Models.ComboBoxOption { DisplayName = "Option B", ValueMappings = new Dictionary<string, object?> { { "PowerCfgValue", 20 } } }
                }
            },
            PowerCfgSettings = new List<PowerCfgSetting>
            {
                new PowerCfgSetting { PowerModeSupport = PowerModeSupport.Separate, RecommendedValueAC = null, RecommendedValueDC = null, DefaultValueAC = null, DefaultValueDC = null }
            }
        };
        var config = _defaultConfig with
        {
            SettingDefinition = settingDef,
            SettingId = "acdc-unknown",
            InputType = InputType.Selection
        };
        var sut = CreateSut(config);

        var rawValues = new Dictionary<string, object?> { { "ACValue", 99 }, { "DCValue", 10 } };
        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, RawValues = rawValues });

        sut.AcValue.Should().Be(0); // 99 not in mappings, defaults to 0
        sut.DcValue.Should().Be(0); // 10 maps to index 0
    }

    [Fact]
    public void UpdateStateFromSystemState_Selection_NonSeparate_UpdatesSelectedValue()
    {
        var settingDef = new SettingDefinition
        {
            Id = "standard-selection",
            Name = "Standard Selection",
            Description = "Non-separate selection",
            InputType = InputType.Selection
        };
        var config = _defaultConfig with
        {
            SettingDefinition = settingDef,
            SettingId = "standard-selection",
            InputType = InputType.Selection
        };
        var sut = CreateSut(config);

        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, CurrentValue = 2 });

        sut.SelectedValue.Should().Be(2);
    }

    // ── UpdateStateFromSystemState: Failed/missing state handling ──

    [Fact]
    public void UpdateStateFromSystemState_FailedResult_DoesNotResetNumericValue()
    {
        var settingDef = new SettingDefinition
        {
            Id = "fail-numeric",
            Name = "Fail Numeric",
            Description = "Failed result test",
            InputType = InputType.NumericRange,
            NumericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 120, Units = "Minutes" }
        };
        var config = _defaultConfig with
        {
            SettingDefinition = settingDef,
            SettingId = "fail-numeric",
            InputType = InputType.NumericRange
        };
        var sut = CreateSut(config);
        sut.NumericValue = 42;

        sut.UpdateStateFromSystemState(new SettingStateResult { Success = false, CurrentValue = 0 });

        sut.NumericValue.Should().Be(42); // preserved, NOT reset to 0
    }

    [Fact]
    public void UpdateStateFromSystemState_NumericRange_NullCurrentValue_DoesNotResetToZero()
    {
        var settingDef = new SettingDefinition
        {
            Id = "null-current",
            Name = "Null Current Value",
            Description = "Null CurrentValue test",
            InputType = InputType.NumericRange,
            NumericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 120 }
        };
        var config = _defaultConfig with
        {
            SettingDefinition = settingDef,
            SettingId = "null-current",
            InputType = InputType.NumericRange
        };
        var sut = CreateSut(config);
        sut.NumericValue = 55;

        // CurrentValue is null (not int), so the `is int` pattern match fails
        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, CurrentValue = null });

        sut.NumericValue.Should().Be(55); // preserved
    }

    // ── Review Mode ──

    [Fact]
    public void IsInReviewMode_ChangingValue_NotifiesEffectiveIsEnabled()
    {
        var sut = CreateSut();
        var changedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        sut.IsInReviewMode = true;

        changedProperties.Should().Contain(nameof(sut.EffectiveIsEnabled));
    }

    [Fact]
    public void IsReviewApproved_SettingTrue_ClearsIsReviewRejected()
    {
        var sut = CreateSut();
        sut.IsReviewRejected = true;

        sut.IsReviewApproved = true;

        sut.IsReviewApproved.Should().BeTrue();
        sut.IsReviewRejected.Should().BeFalse();
    }

    [Fact]
    public void IsReviewRejected_SettingTrue_ClearsIsReviewApproved()
    {
        var sut = CreateSut();
        sut.IsReviewApproved = true;

        sut.IsReviewRejected = true;

        sut.IsReviewRejected.Should().BeTrue();
        sut.IsReviewApproved.Should().BeFalse();
    }

    [Fact]
    public void IsReviewDecisionMade_ReturnsTrueWhenApproved()
    {
        var sut = CreateSut();

        sut.IsReviewApproved = true;

        sut.IsReviewDecisionMade.Should().BeTrue();
    }

    [Fact]
    public void IsReviewDecisionMade_ReturnsTrueWhenRejected()
    {
        var sut = CreateSut();

        sut.IsReviewRejected = true;

        sut.IsReviewDecisionMade.Should().BeTrue();
    }

    [Fact]
    public void IsReviewDecisionMade_ReturnsFalseWhenNeitherApprovedNorRejected()
    {
        var sut = CreateSut();

        sut.IsReviewDecisionMade.Should().BeFalse();
    }

    [Fact]
    public void ReviewApprovalChanged_RaisedWhenIsReviewApprovedChanges()
    {
        var sut = CreateSut();
        bool? receivedApproval = null;
        sut.ReviewApprovalChanged += (_, approved) => receivedApproval = approved;

        sut.IsReviewApproved = true;

        receivedApproval.Should().BeTrue();
    }

    [Fact]
    public void ReviewApprovalChanged_RaisedWithFalseWhenRejected()
    {
        var sut = CreateSut();
        bool? receivedApproval = null;
        sut.ReviewApprovalChanged += (_, approved) => receivedApproval = approved;

        sut.IsReviewRejected = true;

        receivedApproval.Should().BeFalse();
    }

    [Fact]
    public void ClearReviewState_ResetsAllReviewProperties()
    {
        var sut = CreateSut();
        sut.IsInReviewMode = true;
        sut.HasReviewDiff = true;
        sut.ReviewDiffMessage = "Some diff";
        sut.IsReviewApproved = true;

        sut.ClearReviewState();

        sut.IsInReviewMode.Should().BeFalse();
        sut.HasReviewDiff.Should().BeFalse();
        sut.ReviewDiffMessage.Should().BeNull();
        sut.IsReviewApproved.Should().BeFalse();
        sut.IsReviewRejected.Should().BeFalse();
    }

    [Fact]
    public void ClearReviewState_ClearsEventHandler()
    {
        var sut = CreateSut();
        bool raised = false;
        sut.ReviewApprovalChanged += (_, _) => raised = true;

        sut.ClearReviewState();
        sut.IsReviewApproved = true;

        raised.Should().BeFalse("ReviewApprovalChanged handler should have been cleared");
    }

    // ── Technical Details ──

    [Fact]
    public void ToggleTechnicalDetails_TogglesIsTechnicalDetailsExpanded()
    {
        var sut = CreateSut();
        sut.IsTechnicalDetailsExpanded.Should().BeFalse();

        sut.ToggleTechnicalDetails();
        sut.IsTechnicalDetailsExpanded.Should().BeTrue();

        sut.ToggleTechnicalDetails();
        sut.IsTechnicalDetailsExpanded.Should().BeFalse();
    }

    [Fact]
    public void ShowTechnicalDetailsBar_FalseWhenNoTechnicalDetails()
    {
        var sut = CreateSut();
        sut.IsTechnicalDetailsGloballyVisible = true;

        sut.ShowTechnicalDetailsBar.Should().BeFalse();
    }

    [Fact]
    public void IsTechnicalDetailsGloballyVisible_SetToFalse_CollapsesExpanded()
    {
        var sut = CreateSut();
        sut.IsTechnicalDetailsGloballyVisible = true;
        sut.IsTechnicalDetailsExpanded = true;

        sut.IsTechnicalDetailsGloballyVisible = false;

        sut.IsTechnicalDetailsExpanded.Should().BeFalse();
    }

    // ── Advanced Unlock ──

    [Fact]
    public void RequiresAdvancedUnlock_ReturnsTrueWhenSettingDefinitionRequiresIt()
    {
        var settingDef = new SettingDefinition
        {
            Id = "advanced-setting",
            Name = "Advanced",
            Description = "Requires unlock",
            RequiresAdvancedUnlock = true
        };
        var config = _defaultConfig with
        {
            SettingDefinition = settingDef,
            SettingId = "advanced-setting"
        };
        var sut = CreateSut(config);

        sut.RequiresAdvancedUnlock.Should().BeTrue();
    }

    [Fact]
    public void RequiresAdvancedUnlock_ReturnsFalseWhenSettingDefinitionDoesNotRequireIt()
    {
        var sut = CreateSut();
        sut.RequiresAdvancedUnlock.Should().BeFalse();
    }

    // ── PropertyChanged Notifications ──

    [Fact]
    public void IsEnabled_Change_NotifiesEffectiveIsEnabled()
    {
        var sut = CreateSut();
        var changedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        sut.IsEnabled = false;

        changedProperties.Should().Contain(nameof(sut.EffectiveIsEnabled));
    }

    [Fact]
    public void ParentIsEnabled_Change_NotifiesEffectiveIsEnabled()
    {
        var sut = CreateSut();
        var changedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        sut.ParentIsEnabled = false;

        changedProperties.Should().Contain(nameof(sut.EffectiveIsEnabled));
    }

    [Fact]
    public void StatusBannerMessage_Change_NotifiesHasStatusBanner()
    {
        var sut = CreateSut();
        var changedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        sut.StatusBannerMessage = "Warning!";

        changedProperties.Should().Contain(nameof(sut.HasStatusBanner));
    }

    [Fact]
    public void IsTechnicalDetailsExpanded_Change_NotifiesTechnicalDetailsToggleCornerRadius()
    {
        var sut = CreateSut();
        var changedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        sut.IsTechnicalDetailsExpanded = true;

        changedProperties.Should().Contain(nameof(sut.TechnicalDetailsToggleCornerRadius));
    }

    [Fact]
    public void IsTechnicalDetailsGloballyVisible_Change_NotifiesShowTechnicalDetailsBar()
    {
        var sut = CreateSut();
        var changedProperties = new List<string>();
        sut.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        sut.IsTechnicalDetailsGloballyVisible = true;

        changedProperties.Should().Contain(nameof(sut.ShowTechnicalDetailsBar));
    }

    // ── IDisposable ──

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var sut = CreateSut();

        var act = () =>
        {
            sut.Dispose();
            sut.Dispose();
        };

        act.Should().NotThrow();
    }

    // ── Localized Strings with Fallbacks ──

    [Fact]
    public void TechnicalDetailsLabel_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();

        sut.TechnicalDetailsLabel.Should().Be("Technical Details");
    }

    [Fact]
    public void OpenRegeditTooltip_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();

        sut.OpenRegeditTooltip.Should().Be("Open in Registry Editor");
    }

    [Fact]
    public void ClickToUnlockText_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();

        sut.ClickToUnlockText.Should().Be("Click to unlock");
    }

    // ── BadgeRow: multi-pill tests ──

    [Fact]
    public void BadgeRow_Toggle_NonSubjective_DisabledMatchesBothRecommendedAndDefault_BothLit()
    {
        // fax-like: RecommendedValue = 0 (disabled), DefaultValue = 0 (disabled)
        // IsSelected = false (disabled) => both Recommended + Default lit, Custom dim, no Preference.
        var def = BuildToggleSettingDefinition(
            id: "toggle-fax-like",
            recommendedValue: 0,
            defaultValue: 0);
        var config = BuildToggleConfig(def);
        var sut = CreateSut(config);
        sut.IsSelected = false;
        sut.ComputeBadgeState();

        var row = sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).ToArray();
        row.Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Recommended, true),
            (SettingBadgeKind.Default,     true),
            (SettingBadgeKind.Custom,      false),
        }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void BadgeRow_Toggle_NonSubjective_EnabledMismatch_AllDim()
    {
        var def = BuildToggleSettingDefinition(id: "toggle-svc", recommendedValue: 0, defaultValue: 0);
        var sut = CreateSut(BuildToggleConfig(def));
        sut.IsSelected = true;
        sut.ComputeBadgeState();

        sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Recommended, false),
            (SettingBadgeKind.Default,     false),
            (SettingBadgeKind.Custom,      false),
        }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void BadgeRow_Toggle_InvertedPolicy_ToggleOff_OnlyRecommendedLit()
    {
        // Inverted policy: EnabledValue=[null], DisabledValue=[1],
        // RecommendedToggleState=false (recommend the blocking state).
        // Toggle OFF means user has the recommended blocking state applied.
        var def = BuildInvertedPolicyToggleDefinition(
            id: "security-workplace-join-messages-like",
            recommendedToggleState: false);
        var sut = CreateSut(BuildToggleConfig(def));
        sut.IsSelected = false; // toggle OFF -> matches recommended, NOT default
        sut.ComputeBadgeState();

        sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Recommended, true),
            (SettingBadgeKind.Default,     false),
            (SettingBadgeKind.Custom,      false),
        }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void BadgeRow_Toggle_InvertedPolicy_ToggleOn_OnlyDefaultLit()
    {
        // Same inverted-policy shape; toggle ON means key-absent state,
        // which is the Windows default (messages shown / feature enabled).
        var def = BuildInvertedPolicyToggleDefinition(
            id: "security-workplace-join-messages-like-on",
            recommendedToggleState: false);
        var sut = CreateSut(BuildToggleConfig(def));
        sut.IsSelected = true;
        sut.ComputeBadgeState();

        sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Recommended, false),
            (SettingBadgeKind.Default,     true),
            (SettingBadgeKind.Custom,      false),
        }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void BadgeRow_Selection_Subjective_OnRecommended_PreferenceAndRecommendedLit()
    {
        var def = BuildSelectionSettingDefinition(
            id: "uac-like",
            options: new[] { ("DefOpt", 0, false, true), ("RecOpt", 1, true, false) })
            with { IsSubjectivePreference = true };
        var sut = CreateSut(BuildSelectionConfig(def));
        sut.SelectedValue = 1;
        sut.ComputeBadgeState();

        sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Preference,  true),
            (SettingBadgeKind.Recommended, true),
            (SettingBadgeKind.Default,     false),
            (SettingBadgeKind.Custom,      false),
        }, opts => opts.WithStrictOrdering());

        sut.BadgeRow.Should().OnlyContain(
            p => !string.IsNullOrEmpty(p.Label) && !string.IsNullOrEmpty(p.Tooltip),
            because: "every pill must carry resolved Label/Tooltip strings — empty values would surface as blank text in XAML.");
    }

    [Fact]
    public void BadgeRow_Selection_Subjective_OnDefault_PreferenceAndDefaultLit()
    {
        var def = BuildSelectionSettingDefinition(
            id: "uac-like-2",
            options: new[] { ("DefOpt", 0, false, true), ("RecOpt", 1, true, false) })
            with { IsSubjectivePreference = true };
        var sut = CreateSut(BuildSelectionConfig(def));
        sut.SelectedValue = 0;
        sut.ComputeBadgeState();

        sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Preference,  true),
            (SettingBadgeKind.Recommended, false),
            (SettingBadgeKind.Default,     true),
            (SettingBadgeKind.Custom,      false),
        }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void BadgeRow_Selection_Subjective_UnmappedValue_CustomLit()
    {
        var def = BuildSelectionSettingDefinition(
            id: "uac-like-3",
            options: new[] { ("DefOpt", 0, false, true), ("RecOpt", 1, true, false) })
            with { IsSubjectivePreference = true };
        var sut = CreateSut(BuildSelectionConfig(def));
        sut.SelectedValue = 99;
        sut.ComputeBadgeState();

        sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Preference,  true),
            (SettingBadgeKind.Recommended, false),
            (SettingBadgeKind.Default,     false),
            (SettingBadgeKind.Custom,      true),
        }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void BadgeRow_Selection_MultiDefault_NoRecommended_OnEitherOption_DefaultLit()
    {
        var def = BuildSelectionSettingDefinition(
            id: "measurement-like",
            options: new[] { ("Metric", 0, false, true), ("Imperial", 1, false, true) })
            with { IsSubjectivePreference = true };
        var sut = CreateSut(BuildSelectionConfig(def));

        sut.SelectedValue = 0;
        sut.ComputeBadgeState();
        sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Preference, true),
            (SettingBadgeKind.Default,    true),
            (SettingBadgeKind.Custom,     false),
        }, opts => opts.WithStrictOrdering());

        sut.SelectedValue = 1;
        sut.ComputeBadgeState();
        sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Preference, true),
            (SettingBadgeKind.Default,    true),
            (SettingBadgeKind.Custom,     false),
        }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void BadgeRow_Selection_NonSubjective_OnRecommended_OnlyRecommendedLit()
    {
        var def = BuildSelectionSettingDefinition(
            id: "non-subj-rec",
            options: new[] { ("Def", 0, false, true), ("Rec", 1, true, false) });
        var sut = CreateSut(BuildSelectionConfig(def));
        sut.SelectedValue = 1;
        sut.ComputeBadgeState();

        sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Recommended, true),
            (SettingBadgeKind.Default,     false),
            (SettingBadgeKind.Custom,      false),
        }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void BadgeRow_Selection_OptionIsBothRecommendedAndDefault_BothLit()
    {
        var def = BuildSelectionSettingDefinition(
            id: "both-flags",
            options: new[] { ("OnlyOption", 0, true, true) });
        var sut = CreateSut(BuildSelectionConfig(def));
        sut.SelectedValue = 0;
        sut.ComputeBadgeState();

        sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Recommended, true),
            (SettingBadgeKind.Default,     true),
            (SettingBadgeKind.Custom,      false),
        }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void BadgeRow_NumericRange_AtRecommended_OnlyRecommendedLit()
    {
        var def = BuildNumericSettingDefinition(
            id: "numeric-rec",
            recommendedValue: 50,
            defaultValue: 10);
        var config = new SettingItemViewModelConfig
        {
            SettingDefinition = def,
            SettingId = def.Id,
            Name = def.Name,
            Description = def.Description,
            InputType = InputType.NumericRange,
            IsSelected = false,
        };
        var sut = CreateSut(config);
        sut.NumericValue = 50;
        sut.ComputeBadgeState();

        sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Recommended, true),
            (SettingBadgeKind.Default,     false),
            (SettingBadgeKind.Custom,      false),
        }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void BadgeRow_Definition_HasNoRecommendedAtAll_RecommendedPillAbsent()
    {
        var def = BuildSelectionSettingDefinition(
            id: "no-rec",
            options: new[] { ("A", 0, false, true), ("B", 1, false, true) })
            with { IsSubjectivePreference = true };
        var sut = CreateSut(BuildSelectionConfig(def));
        sut.SelectedValue = 0;
        sut.ComputeBadgeState();

        sut.BadgeRow.Should().NotContain(p => p.Kind == SettingBadgeKind.Recommended);
    }

    private static SettingDefinition BuildSelectionSettingDefinition(
        string id,
        IEnumerable<(string DisplayName, int Value, bool IsRecommended, bool IsDefault)> options)
    {
        var list = new List<Winhance.Core.Features.Common.Models.ComboBoxOption>();
        foreach (var (name, v, rec, def) in options)
        {
            list.Add(new Winhance.Core.Features.Common.Models.ComboBoxOption
            {
                DisplayName = name,
                ValueMappings = new Dictionary<string, object?> { ["V"] = v },
                IsRecommended = rec,
                IsDefault = def,
            });
        }
        return new SettingDefinition
        {
            Id = id,
            Name = id,
            Description = "",
            InputType = InputType.Selection,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_CURRENT_USER\Software\Winhance\Test",
                    ValueName = "V",
                    RecommendedValue = null,
                    DefaultValue = null,
                    ValueType = Microsoft.Win32.RegistryValueKind.DWord,
                    IsPrimary = true,
                },
            },
            ComboBox = new ComboBoxMetadata { Options = list },
        };
    }

    private SettingItemViewModelConfig BuildSelectionConfig(SettingDefinition def) =>
        new SettingItemViewModelConfig
        {
            SettingDefinition = def,
            SettingId = def.Id,
            Name = def.Name,
            Description = def.Description,
            InputType = InputType.Selection,
            IsSelected = false,
        };

    private static SettingDefinition BuildToggleSettingDefinition(
        string id,
        object recommendedValue,
        object defaultValue)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = id,
            Description = "",
            InputType = InputType.Toggle,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_CURRENT_USER\Software\Winhance\Test",
                    ValueName = "V",
                    EnabledValue = new object?[] { 1 },
                    DisabledValue = new object?[] { 0 },
                    RecommendedValue = recommendedValue,
                    DefaultValue = defaultValue,
                    ValueType = Microsoft.Win32.RegistryValueKind.DWord,
                    IsPrimary = true,
                },
            },
        };
    }

    private static SettingDefinition BuildInvertedPolicyToggleDefinition(string id, bool? recommendedToggleState)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = id,
            Description = "",
            InputType = InputType.Toggle,
            RecommendedToggleState = recommendedToggleState,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Test",
                    ValueName = "BlockThing",
                    EnabledValue = new object?[] { null },
                    DisabledValue = new object?[] { 1 },
                    RecommendedValue = null,
                    DefaultValue = null,
                    ValueType = Microsoft.Win32.RegistryValueKind.DWord,
                    IsGroupPolicy = true,
                    IsPrimary = true,
                },
            },
        };
    }

    private SettingItemViewModelConfig BuildToggleConfig(SettingDefinition def) =>
        new SettingItemViewModelConfig
        {
            SettingDefinition = def,
            SettingId = def.Id,
            Name = def.Name,
            Description = def.Description,
            InputType = InputType.Toggle,
            IsSelected = false,
        };

    // ───────── Task B4: NumericRange quick-set buttons ─────────

    private static SettingDefinition BuildNumericSettingDefinition(
        string id,
        object? recommendedValue,
        object? defaultValue)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = id,
            Description = "",
            InputType = InputType.NumericRange,
            NumericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 100, Units = null },
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKEY_CURRENT_USER\Software\Winhance\Test",
                    ValueName = "V",
                    RecommendedValue = recommendedValue,
                    DefaultValue = defaultValue,
                    ValueType = Microsoft.Win32.RegistryValueKind.DWord,
                    IsPrimary = true,
                },
            },
        };
    }

    private SettingItemViewModelConfig BuildNumericConfig(SettingDefinition def) =>
        new SettingItemViewModelConfig
        {
            SettingDefinition = def,
            SettingId = def.Id,
            Name = def.Name,
            Description = def.Description,
            InputType = InputType.NumericRange,
            IsSelected = false,
        };

    private static SettingDefinition BuildPowerCfgSeparateNumericDefinition(
        string id,
        int? recAc, int? recDc, int? defAc, int? defDc)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = id,
            Description = "",
            InputType = InputType.NumericRange,
            NumericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 100, Units = null },
            PowerCfgSettings = new List<PowerCfgSetting>
            {
                new PowerCfgSetting
                {
                    PowerModeSupport = PowerModeSupport.Separate,
                    RecommendedValueAC = recAc,
                    RecommendedValueDC = recDc,
                    DefaultValueAC = defAc,
                    DefaultValueDC = defDc,
                },
            },
        };
    }

    [Fact]
    public void SetNumericToRecommendedCommand_NumericRange_SetsNumericValueToRecommended()
    {
        _mockSettingApplicationService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var def = BuildNumericSettingDefinition("numeric-rec", recommendedValue: 100, defaultValue: 0);
        var sut = CreateSut(BuildNumericConfig(def));

        sut.SetNumericToRecommendedCommand.Execute(null);

        sut.NumericValue.Should().Be(100);
        _mockSettingApplicationService.Verify(
            s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
                r.SettingId == "numeric-rec" && (int)r.Value! == 100)),
            Times.Once);
    }

    [Fact]
    public void SetNumericToDefaultCommand_NumericRange_SetsNumericValueToDefault()
    {
        _mockSettingApplicationService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var def = BuildNumericSettingDefinition("numeric-def", recommendedValue: 100, defaultValue: 25);
        var sut = CreateSut(BuildNumericConfig(def));

        sut.SetNumericToDefaultCommand.Execute(null);

        sut.NumericValue.Should().Be(25);
        _mockSettingApplicationService.Verify(
            s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
                r.SettingId == "numeric-def" && (int)r.Value! == 25)),
            Times.Once);
    }

    [Fact]
    public void SetAcNumericToRecommendedCommand_PowerCfgSeparate_OnlySetsAcValue()
    {
        _mockSettingApplicationService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var def = BuildPowerCfgSeparateNumericDefinition("acdc-rec", recAc: 50, recDc: 25, defAc: 0, defDc: 0);
        var sut = CreateSut(BuildNumericConfig(def));
        sut.AcNumericValue = 0;
        sut.DcNumericValue = 0;

        sut.SetAcNumericToRecommendedCommand.Execute(null);

        sut.AcNumericValue.Should().Be(50);
        sut.DcNumericValue.Should().Be(0, because: "the AC quick-set button must never touch the DC value");
    }

    [Fact]
    public void SetDcNumericToDefaultCommand_PowerCfgSeparate_OnlySetsDcValue()
    {
        _mockSettingApplicationService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var def = BuildPowerCfgSeparateNumericDefinition("acdc-def", recAc: 50, recDc: 25, defAc: 100, defDc: 75);
        var sut = CreateSut(BuildNumericConfig(def));
        sut.AcNumericValue = 10;
        sut.DcNumericValue = 20;

        sut.SetDcNumericToDefaultCommand.Execute(null);

        sut.DcNumericValue.Should().Be(75);
        sut.AcNumericValue.Should().Be(10, because: "the DC quick-set button must never touch the AC value");
    }

    [Fact]
    public void ShowNumericQuickSetButtons_ReflectsIsInfoBadgeGloballyVisible()
    {
        var def = BuildNumericSettingDefinition("numeric-toggle", recommendedValue: 100, defaultValue: 0);
        var sut = CreateSut(BuildNumericConfig(def));

        sut.IsInfoBadgeGloballyVisible = false;
        sut.ShowNumericQuickSetButtons.Should().BeFalse(
            because: "ShowInfoBadges is off, the quick-set buttons must be hidden");

        sut.IsInfoBadgeGloballyVisible = true;
        sut.ShowNumericQuickSetButtons.Should().BeTrue(
            because: "ShowInfoBadges is on AND the setting has Recommended/Default data");
    }
}
