using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Catalog;
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

    private readonly Setting _defaultSetting;
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

        _defaultSetting = new Setting
        {
            Id = "test-setting",
            Display = new() { Name = "Test Setting", Description = "A test setting description" },
        };

        _defaultConfig = new SettingItemViewModelConfig
        {
            Setting = _defaultSetting,
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
        sut.Setting.Should().BeSameAs(_defaultSetting);
    }

    [Fact]
    public void Constructor_InitializesDefaults()
    {
        var sut = CreateSut();

        sut.Status.Should().BeEmpty();
        sut.ComboBoxOptions.Should().NotBeNull().And.BeEmpty();
        sut.MaxValue.Should().Be(100);
        sut.Units.Should().BeEmpty();
        sut.TechnicalDetailSections.Should().NotBeNull().And.BeEmpty();
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
    public void Action_WithRecommendedRegistryValue_ShowsNoStateBadges()
    {
        // Regression: a one-shot Action must not light up Recommended/Default/Custom state badges,
        // even when it carries recommended/default data (Win11 Clean Start Menu's
        // ConfigureStartPins did, which wrongly lit Recommended + Custom). Matches Win10 clean / taskbar clean.
        // An Action carries no roled states (Control=Action), and InitializeHasBadgeData
        // short-circuits InputType.Action to HasBadgeData=false, so the badge row stays empty regardless.
        var setting = new Setting
        {
            Id = "act-badge",
            Display = new() { Name = "Action", Description = "d" },
        };
        var config = _defaultConfig with
        {
            Setting = setting,
            SettingId = "act-badge",
            InputType = InputType.Action,
        };

        var sut = CreateSut(config);

        sut.HasBadgeData.Should().BeFalse();
        sut.BadgeRow.Should().BeEmpty();
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
    public void UpdateStatusBanner_WithOptionWarning_SetsErrorBanner()
    {
        // Arrange: selection setting where option 0 has a Warning string.
        // The option-warning banner is computed from config.OptionWarnings, not the Setting, so a minimal synthetic Setting suffices.
        var config = _defaultConfig with
        {
            Setting = new Setting { Id = "gaming-windows-search-service", Display = new() { Name = "Windows Search Indexing Service", Description = "desc" } },
            SettingId = "gaming-windows-search-service",
            InputType = InputType.Selection,
            OptionWarnings = new string?[] { "WARNING: Disabling WSearch breaks Outlook search.", null },
        };
        var sut = CreateSut(config);

        // Act: user selects the Warning-flagged option (index 0).
        sut.UpdateStatusBanner(0);

        // Assert: Error banner with the option Warning message.
        sut.StatusBannerMessage.Should().Be("WARNING: Disabling WSearch breaks Outlook search.");
        sut.StatusBannerSeverity.Should().Be(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error);
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
        var config = _defaultConfig with
        {
            Setting = new Setting { Id = "child-setting", Display = new() { Name = "Child", Description = "Child setting" }, UiParentId = "parent-setting" },
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
        var config = _defaultConfig with
        {
            Setting = AlwaysNumericSetting("power-timeout", 0, 120, "Minutes"),
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
        var config = _defaultConfig with
        {
            Setting = AlwaysNumericSetting("disk-timeout", 0, 24, "Hours"),
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
        var config = _defaultConfig with
        {
            Setting = AlwaysNumericSetting("raw-setting", 0, 1000, null),
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
        var config = _defaultConfig with
        {
            Setting = AlwaysNumericSetting("zero-setting", 0, 120, "Minutes"),
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
        var config = _defaultConfig with
        {
            Setting = PowerCfgSeparateNumericSetting("acdc-numeric", null, null, null, null, units: "Minutes"),
            SettingId = "acdc-numeric",
            InputType = InputType.NumericRange
        };
        var sut = CreateSut(config);

        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, AcValue = 1200, DcValue = 600 });

        sut.AcNumericValue.Should().Be(20); // 1200 / 60
        sut.DcNumericValue.Should().Be(10); // 600 / 60
    }

    [Fact]
    public void UpdateStateFromSystemState_NumericRange_SeparateACDC_MissingDCValue_OnlyUpdatesAC()
    {
        var config = _defaultConfig with
        {
            Setting = PowerCfgSeparateNumericSetting("acdc-ac-only", null, null, null, null, units: "Minutes"),
            SettingId = "acdc-ac-only",
            InputType = InputType.NumericRange
        };
        var sut = CreateSut(config);
        sut.DcNumericValue = 99; // pre-set DC value

        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, AcValue = 1200 });

        sut.AcNumericValue.Should().Be(20); // 1200 / 60
        sut.DcNumericValue.Should().Be(99); // unchanged
    }

    // ── UpdateStateFromSystemState: AC/DC separate value handling for Selection ──

    [Fact]
    public void UpdateStateFromSystemState_Selection_SeparateACDC_UpdatesBothIndices()
    {
        var config = _defaultConfig with
        {
            Setting = PowerCfgSeparateSelectionSetting("acdc-selection", new[] { ("Option A", 10), ("Option B", 20), ("Option C", 30) }),
            SettingId = "acdc-selection",
            InputType = InputType.Selection
        };
        var sut = CreateSut(config);

        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, AcValue = 30, DcValue = 10 });

        sut.AcValue.Should().Be(2); // PowerCfgValue 30 maps to index 2
        sut.DcValue.Should().Be(0); // PowerCfgValue 10 maps to index 0
    }

    [Fact]
    public void UpdateStateFromSystemState_Selection_SeparateACDC_UnknownPowerCfgValue_DefaultsToZero()
    {
        var config = _defaultConfig with
        {
            Setting = PowerCfgSeparateSelectionSetting("acdc-unknown", new[] { ("Option A", 10), ("Option B", 20) }),
            SettingId = "acdc-unknown",
            InputType = InputType.Selection
        };
        var sut = CreateSut(config);

        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, AcValue = 99, DcValue = 10 });

        sut.AcValue.Should().Be(ComboBoxConstants.CustomStateIndex); // 99 not in mappings -> Custom
        sut.DcValue.Should().Be(0); // 10 maps to index 0
    }

    [Fact]
    public void UpdateStateFromSystemState_Selection_NonSeparate_UpdatesSelectedValue()
    {
        var config = _defaultConfig with
        {
            Setting = new Setting { Id = "standard-selection", Display = new() { Name = "Standard Selection", Description = "Non-separate selection" } },
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
        var config = _defaultConfig with
        {
            Setting = AlwaysNumericSetting("fail-numeric", 0, 120, "Minutes"),
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
        var config = _defaultConfig with
        {
            Setting = AlwaysNumericSetting("null-current", 0, 120, null),
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
    public void RequiresAdvancedUnlock_ReturnsTrueWhenSettingRequiresIt()
    {
        var config = _defaultConfig with
        {
            Setting = new Setting { Id = "advanced-setting", Display = new() { Name = "Advanced", Description = "Requires unlock" }, Availability = new Availability { RequiresAdvancedUnlock = true } },
            SettingId = "advanced-setting"
        };
        var sut = CreateSut(config);

        sut.RequiresAdvancedUnlock.Should().BeTrue();
    }

    [Fact]
    public void RequiresAdvancedUnlock_ReturnsFalseWhenSettingDoesNotRequireIt()
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
        var config = ToggleConfig(ToggleSetting("toggle-fax-like", recommendedEnabled: false, defaultEnabled: false));
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
        var sut = CreateSut(ToggleConfig(ToggleSetting("toggle-svc", recommendedEnabled: false, defaultEnabled: false)));
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
        var sut = CreateSut(ToggleConfig(ToggleSetting("security-workplace-join-messages-like", recommendedEnabled: false, defaultEnabled: true)));
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
        var sut = CreateSut(ToggleConfig(ToggleSetting("security-workplace-join-messages-like-on", recommendedEnabled: false, defaultEnabled: true)));
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
    public void BadgeRow_Toggle_BasePlusNullDefaultPolicyEnforcer_ToggleOn_OnlyDefaultLit()
    {
        // privacy-tailored-experiences shape: a base registry with DefaultValue = 1 (Windows default ON)
        // plus a group-policy enforcer reg. The WindowsDefault role comes from the PRIMARY reg's
        // DefaultValue = 1 (= ON), so toggle ON (IsSelected = true) matches Default and the Default badge lights.
        var sut = CreateSut(ToggleConfig(ToggleSetting("tailored-experiences-like", recommendedEnabled: false, defaultEnabled: true)));
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
    public void BadgeRow_Toggle_BasePlusNullDefaultPolicyEnforcer_ToggleOff_RecommendedLit()
    {
        // Same shape as above; toggle OFF is the recommended state. Default must
        // be dim because the base reg's Windows default is ON.
        var sut = CreateSut(ToggleConfig(ToggleSetting("tailored-experiences-like-off", recommendedEnabled: false, defaultEnabled: true)));
        sut.IsSelected = false;
        sut.ComputeBadgeState();

        sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Recommended, true),
            (SettingBadgeKind.Default,     false),
            (SettingBadgeKind.Custom,      false),
        }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void BadgeRow_Selection_Subjective_OnRecommended_PreferenceAndRecommendedLit()
    {
        var sut = CreateSut(SelectionConfig(SelectionSetting("uac-like",
            new[] { ("DefOpt", false, true), ("RecOpt", true, false) }, subjective: true)));
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
        var sut = CreateSut(SelectionConfig(SelectionSetting("uac-like-2",
            new[] { ("DefOpt", false, true), ("RecOpt", true, false) }, subjective: true)));
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
        var sut = CreateSut(SelectionConfig(SelectionSetting("uac-like-3",
            new[] { ("DefOpt", false, true), ("RecOpt", true, false) }, subjective: true)));
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
        var sut = CreateSut(SelectionConfig(SelectionSetting("measurement-like",
            new[] { ("Metric", false, true), ("Imperial", false, true) }, subjective: true)));

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
        var sut = CreateSut(SelectionConfig(SelectionSetting("non-subj-rec",
            new[] { ("Def", false, true), ("Rec", true, false) })));
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
        var sut = CreateSut(SelectionConfig(SelectionSetting("both-flags",
            new[] { ("OnlyOption", true, true) })));
        sut.SelectedValue = 0;
        sut.ComputeBadgeState();

        sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Recommended, true),
            (SettingBadgeKind.Default,     true),
            (SettingBadgeKind.Custom,      false),
        }, opts => opts.WithStrictOrdering());
    }

    // No single-spinner registry NumericRange badge test: that is a verified-nonexistent production shape
    // (every NumericRange setting is powercfg; zero registry numerics), so the badge path is dead and no
    // synthetic fixture builds it. PowerCfg-numeric badge coverage stays in BadgeRow_AcDcSeparate_*.

    [Fact]
    public void BadgeRow_Setting_HasNoRecommendedAtAll_RecommendedPillAbsent()
    {
        var sut = CreateSut(SelectionConfig(SelectionSetting("no-rec",
            new[] { ("A", false, true), ("B", false, true) }, subjective: true)));
        sut.SelectedValue = 0;
        sut.ComputeBadgeState();

        sut.BadgeRow.Should().NotContain(p => p.Kind == SettingBadgeKind.Recommended);
    }

    // ── AC/DC PowerCfg-Separate badge tests (issue #602 fix + Pattern 2 per-mode) ──

    [Fact]
    public void BadgeRow_AcDcSeparate_NoBattery_DcDiffersFromRec_RecommendedStillLit()
    {
        // Regression for #602: on a desktop without a battery, PowerCfgApplier silently
        // skips DC writes. The badge must not treat the unchanged-DC system value as a
        // mismatch — otherwise the user sees Recommended lit after Apply, then a system-
        // state refresh re-syncs the VM and the badge flips to Custom.
        var sut = CreateSut(NumericConfig(PowerCfgSeparateNumericSetting(
            "acdc-no-battery", recAc: 0, recDc: 600, defAc: 1200, defDc: 600)));
        sut.HasBattery = false;
        sut.AcNumericValue = 0;     // matches RecAC
        sut.DcNumericValue = 20;    // differs from RecDC (=600) — but DC must be ignored
        sut.ComputeBadgeState();

        var recommended = sut.BadgeRow.SingleOrDefault(p => p.Kind == SettingBadgeKind.Recommended);
        recommended.Should().NotBeNull();
        recommended!.IsHighlighted.Should().BeTrue(
            because: "on a battery-less system DC isn't writable, so the badge must reflect only AC");
        sut.BadgeRow.Should().NotContain(p => p.Mode == SettingBadgeMode.AC || p.Mode == SettingBadgeMode.DC,
            because: "per-mode pills are reserved for HasBattery==true");
    }

    [Fact]
    public void BadgeRow_AcDcSeparate_WithBattery_AcMatchesRec_DcMatchesDef_BothModeBadgesLitCorrectly()
    {
        // Pattern 2: on a laptop, AC/DC sides each get their own Recommended/Default/Custom
        // pills so partial matches are visible.
        var sut = CreateSut(NumericConfig(PowerCfgSeparateNumericSetting(
            "acdc-with-battery", recAc: 50, recDc: 25, defAc: 0, defDc: 25)));
        sut.HasBattery = true;
        sut.AcNumericValue = 50;    // matches RecAC
        sut.DcNumericValue = 25;    // matches both RecDC AND DefDC (Rec==Def on this side)
        sut.ComputeBadgeState();

        var recAc = sut.BadgeRow.Single(p => p.Kind == SettingBadgeKind.Recommended && p.Mode == SettingBadgeMode.AC);
        var recDc = sut.BadgeRow.Single(p => p.Kind == SettingBadgeKind.Recommended && p.Mode == SettingBadgeMode.DC);
        var defAc = sut.BadgeRow.Single(p => p.Kind == SettingBadgeKind.Default && p.Mode == SettingBadgeMode.AC);
        var defDc = sut.BadgeRow.Single(p => p.Kind == SettingBadgeKind.Default && p.Mode == SettingBadgeMode.DC);

        recAc.IsHighlighted.Should().BeTrue();
        recDc.IsHighlighted.Should().BeTrue();
        defAc.IsHighlighted.Should().BeFalse(because: "AC=50 doesn't match DefAC=0");
        defDc.IsHighlighted.Should().BeTrue(because: "DC=25 also matches DefDC=25");
        recAc.Label.Should().EndWith("(AC)");
        recDc.Label.Should().EndWith("(DC)");
    }

    [Fact]
    public void BadgeRow_AcDcSeparate_WithBattery_DcOnlyRecommendation_NoAcPillEmitted()
    {
        // If a powercfg setting only declares RecommendedValueDC (no AC counterpart), the
        // per-mode emitter must produce only the DC Recommended pill — not a phantom AC pill.
        var sut = CreateSut(NumericConfig(PowerCfgSeparateNumericSetting(
            "acdc-dc-only-rec", recAc: null, recDc: 600, defAc: null, defDc: 1200)));
        sut.HasBattery = true;
        sut.ComputeBadgeState();

        sut.BadgeRow.Should().NotContain(p => p.Kind == SettingBadgeKind.Recommended && p.Mode == SettingBadgeMode.AC);
        sut.BadgeRow.Should().ContainSingle(p => p.Kind == SettingBadgeKind.Recommended && p.Mode == SettingBadgeMode.DC);
    }

    [Fact]
    public void BadgeRow_AcDcSeparate_WithBattery_AcCustom_DcAtRec_OnlyCustomAcLit()
    {
        // Partial-custom case: AC matches neither Rec nor Def, DC matches Rec. We expect
        // Custom (AC) lit, Custom (DC) dim — the lit-state must follow the side, not the row.
        var sut = CreateSut(NumericConfig(PowerCfgSeparateNumericSetting(
            "acdc-partial-custom", recAc: 50, recDc: 25, defAc: 0, defDc: 75)));
        sut.HasBattery = true;
        sut.AcNumericValue = 33;    // matches neither RecAC=50 nor DefAC=0 → AC Custom
        sut.DcNumericValue = 25;    // matches RecDC → DC Recommended
        sut.ComputeBadgeState();

        var customAc = sut.BadgeRow.Single(p => p.Kind == SettingBadgeKind.Custom && p.Mode == SettingBadgeMode.AC);
        var customDc = sut.BadgeRow.Single(p => p.Kind == SettingBadgeKind.Custom && p.Mode == SettingBadgeMode.DC);
        customAc.IsHighlighted.Should().BeTrue();
        customDc.IsHighlighted.Should().BeFalse();
    }

    // ---------------------------------------------------------------------------------------------------
    // Synthetic catalog-Setting fixtures.
    // The SettingItemViewModel reads the PASSED Setting (no live-catalog resolution), so hand-built synthetic
    // Settings carrying exactly the fields the VM reads are correct here -- simpler, non-vacuous, and immune to
    // catalog edits.
    // ---------------------------------------------------------------------------------------------------

    // A toggle Setting: Enabled/Disabled states carrying the Recommended/WindowsDefault roles -- a role lands
    // on the Enabled state when that role's toggle state is enabled, on the Disabled state when it is
    // disabled. null = that role absent.
    private static Setting ToggleSetting(string id, bool? recommendedEnabled, bool? defaultEnabled)
    {
        var enabled = new List<StateRole>();
        var disabled = new List<StateRole>();
        if (recommendedEnabled is bool r) (r ? enabled : disabled).Add(StateRole.Recommended);
        if (defaultEnabled is bool d) (d ? enabled : disabled).Add(StateRole.WindowsDefault);
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

    // A registry SELECTION Setting: one state per option carrying its Recommended/WindowsDefault roles. The badge
    // logic reads only the state Roles + the selected index (never the accept-Set for a non-powercfg selection),
    // so the Set is omitted. subjective -> Display.IsSubjectivePreference (the Preference badge).
    private static Setting SelectionSetting(string id, (string Label, bool Recommended, bool Default)[] options, bool subjective = false)
    {
        var states = new List<SettingState>();
        foreach (var (label, rec, def) in options)
        {
            var roles = new List<StateRole>();
            if (rec) roles.Add(StateRole.Recommended);
            if (def) roles.Add(StateRole.WindowsDefault);
            states.Add(new SettingState { Label = label, Roles = roles });
        }
        return new Setting
        {
            Id = id,
            Display = new() { Name = id, Description = "", IsSubjectivePreference = subjective },
            States = states,
        };
    }

    // A single-spinner (Always-context) numeric Setting: the VM reads Numeric.Units for conversion and the
    // Always-context Recommended/WindowsDefault for the quick-set/badge accessors. Registry single-spinner numerics
    // had no powercfg peer, so this carries only the Always context.
    private static Setting AlwaysNumericSetting(string id, int min, int max, string? units, int? recommended = null, int? windowsDefault = null)
    {
        var rec = recommended is int r ? new[] { new ContextValue(PowerContext.Always, r) } : System.Array.Empty<ContextValue>();
        var def = windowsDefault is int d ? new[] { new ContextValue(PowerContext.Always, d) } : System.Array.Empty<ContextValue>();
        return new Setting
        {
            Id = id,
            Display = new() { Name = id, Description = "" },
            Numeric = new() { Min = min, Max = max, Units = units, Recommended = rec, WindowsDefault = def },
        };
    }

    // A powercfg AC/DC-Separate NUMERIC Setting: per-context Recommended/WindowsDefault ContextValues (only the
    // non-null sides, mirroring ConvertPowerCfg) plus the Separate PowerCfgTarget that drives SupportsSeparateACDC.
    // Units default to "" (the converter's NumericRange.Units ?? pcs.Units ?? ""), so ConvertFrom/ToSystemUnits is 1:1.
    private static Setting PowerCfgSeparateNumericSetting(string id, int? recAc, int? recDc, int? defAc, int? defDc, string? units = "")
    {
        var recommended = new List<ContextValue>();
        if (recAc is int ra) recommended.Add(new ContextValue(PowerContext.AC, ra));
        if (recDc is int rd) recommended.Add(new ContextValue(PowerContext.DC, rd));
        var windowsDefault = new List<ContextValue>();
        if (defAc is int da) windowsDefault.Add(new ContextValue(PowerContext.AC, da));
        if (defDc is int dd) windowsDefault.Add(new ContextValue(PowerContext.DC, dd));
        return new Setting
        {
            Id = id,
            Display = new() { Name = id, Description = "" },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[] { new PowerCfgTarget("Power", "sub", "setting", PowerModeSupport.Separate) },
            Numeric = new() { Min = 0, Max = 100, Units = units, Recommended = recommended, WindowsDefault = windowsDefault },
        };
    }

    // A powercfg AC/DC-Separate SELECTION Setting: one state per option whose Set["Power"] accepts that option's raw
    // powercfg value (how UpdateStateFromSystemState maps an AC/DC reading to an option index), plus the Separate
    // PowerCfgTarget. No roles (the AC/DC selection tests assert index resolution, not badges).
    private static Setting PowerCfgSeparateSelectionSetting(string id, (string Label, int PowerValue)[] options)
    {
        var states = new List<SettingState>();
        foreach (var (label, power) in options)
            states.Add(new SettingState { Label = label, Set = new Dictionary<string, StateValue> { ["Power"] = StateValue.Of(power) } });
        return new Setting
        {
            Id = id,
            Display = new() { Name = id, Description = "" },
            Contexts = new[] { PowerContext.AC, PowerContext.DC },
            Targets = new Target[] { new PowerCfgTarget("Power", "sub", "setting", PowerModeSupport.Separate) },
            States = states,
        };
    }

    private SettingItemViewModelConfig ToggleConfig(Setting setting) =>
        new SettingItemViewModelConfig
        {
            Setting = setting,
            SettingId = setting.Id,
            Name = setting.Display.Name,
            Description = setting.Display.Description,
            InputType = InputType.Toggle,
            IsSelected = false,
        };

    private SettingItemViewModelConfig SelectionConfig(Setting setting) =>
        new SettingItemViewModelConfig
        {
            Setting = setting,
            SettingId = setting.Id,
            Name = setting.Display.Name,
            Description = setting.Display.Description,
            InputType = InputType.Selection,
            IsSelected = false,
        };

    private SettingItemViewModelConfig NumericConfig(Setting setting) =>
        new SettingItemViewModelConfig
        {
            Setting = setting,
            SettingId = setting.Id,
            Name = setting.Display.Name,
            Description = setting.Display.Description,
            InputType = InputType.NumericRange,
            IsSelected = false,
        };

    [Fact]
    public void SetNumericToRecommendedCommand_NumericRange_SetsNumericValueToRecommended()
    {
        _mockSettingApplicationService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var sut = CreateSut(NumericConfig(AlwaysNumericSetting("numeric-rec", 0, 100, null, recommended: 100, windowsDefault: 0)));

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

        var sut = CreateSut(NumericConfig(AlwaysNumericSetting("numeric-def", 0, 100, null, recommended: 100, windowsDefault: 25)));

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

        var sut = CreateSut(NumericConfig(PowerCfgSeparateNumericSetting("acdc-rec", recAc: 50, recDc: 25, defAc: 0, defDc: 0)));
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

        var sut = CreateSut(NumericConfig(PowerCfgSeparateNumericSetting("acdc-def", recAc: 50, recDc: 25, defAc: 100, defDc: 75)));
        sut.AcNumericValue = 10;
        sut.DcNumericValue = 20;

        sut.SetDcNumericToDefaultCommand.Execute(null);

        sut.DcNumericValue.Should().Be(75);
        sut.AcNumericValue.Should().Be(10, because: "the DC quick-set button must never touch the AC value");
    }

    [Fact]
    public void ShowNumericQuickSetButtons_ReflectsIsInfoBadgeGloballyVisible()
    {
        var sut = CreateSut(NumericConfig(AlwaysNumericSetting("numeric-toggle", 0, 100, null, recommended: 100, windowsDefault: 0)));

        sut.IsInfoBadgeGloballyVisible = false;
        sut.ShowNumericQuickSetButtons.Should().BeFalse(
            because: "ShowInfoBadges is off, the quick-set buttons must be hidden");

        sut.IsInfoBadgeGloballyVisible = true;
        sut.ShowNumericQuickSetButtons.Should().BeTrue(
            because: "ShowInfoBadges is on AND the setting has Recommended/Default data");
    }

    // ── Pinned regression tests: real catalog Setting instances ──

    [Fact]
    public void WorkplaceJoinMessages_ToggleOff_RecommendedLit_DefaultDim()
    {
        var setting = SettingCatalog.All.First(s => s.Id == "security-workplace-join-messages");

        var config = new SettingItemViewModelConfig
        {
            Setting = setting,
            SettingId = setting.Id,
            Name = setting.Display.Name,
            Description = setting.Display.Description,
            InputType = InputType.Toggle,
            IsSelected = false, // toggle OFF -> blocking state applied
        };
        var sut = CreateSut(config);
        sut.ComputeBadgeState();

        sut.BadgeRow.Where(p => p.IsHighlighted).Select(p => p.Kind)
            .Should().BeEquivalentTo(new[] { SettingBadgeKind.Recommended });
    }

    [Fact]
    public void WorkplaceJoinMessages_ToggleOn_DefaultLit_RecommendedDim()
    {
        var setting = SettingCatalog.All.First(s => s.Id == "security-workplace-join-messages");

        var config = new SettingItemViewModelConfig
        {
            Setting = setting,
            SettingId = setting.Id,
            Name = setting.Display.Name,
            Description = setting.Display.Description,
            InputType = InputType.Toggle,
            IsSelected = true, // toggle ON -> Windows default
        };
        var sut = CreateSut(config);
        sut.ComputeBadgeState();

        sut.BadgeRow.Where(p => p.IsHighlighted).Select(p => p.Kind)
            .Should().BeEquivalentTo(new[] { SettingBadgeKind.Default });
    }

    [Fact]
    public void BingSearchResults_ToggleOff_RecommendedLit_DefaultDim()
    {
        var setting = SettingCatalog.All.First(s => s.Id == "start-disable-bing-search-results");

        var config = new SettingItemViewModelConfig
        {
            Setting = setting,
            SettingId = setting.Id,
            Name = setting.Display.Name,
            Description = setting.Display.Description,
            InputType = InputType.Toggle,
            IsSelected = false,
        };
        var sut = CreateSut(config);
        sut.ComputeBadgeState();

        sut.BadgeRow.Where(p => p.IsHighlighted).Select(p => p.Kind)
            .Should().BeEquivalentTo(new[] { SettingBadgeKind.Recommended });
    }

    [Fact]
    public void BingSearchResults_ToggleOn_DefaultLit_RecommendedDim()
    {
        var setting = SettingCatalog.All.First(s => s.Id == "start-disable-bing-search-results");

        var config = new SettingItemViewModelConfig
        {
            Setting = setting,
            SettingId = setting.Id,
            Name = setting.Display.Name,
            Description = setting.Display.Description,
            InputType = InputType.Toggle,
            IsSelected = true,
        };
        var sut = CreateSut(config);
        sut.ComputeBadgeState();

        sut.BadgeRow.Where(p => p.IsHighlighted).Select(p => p.Kind)
            .Should().BeEquivalentTo(new[] { SettingBadgeKind.Default });
    }

    // ── Review Mode auto-expand ──

    [Fact]
    public void EnteringReviewMode_ForcesExpanderExpanded()
    {
        // A parent collapsed before a config import would hide its children behind a
        // disabled card. Entering Review Mode must force every expander open so all
        // child diffs are reachable.
        var sut = CreateSut();
        sut.IsExpanderExpanded = false;

        sut.IsInReviewMode = true;

        sut.IsExpanderExpanded.Should().BeTrue();
    }

    [Fact]
    public void ExitingReviewMode_DoesNotCollapseExpander()
    {
        // Leaving Review Mode should not touch the expander state — whatever the user
        // left it at (or whatever auto-expand set it to) stays.
        var sut = CreateSut();
        sut.IsInReviewMode = true;
        sut.IsExpanderExpanded.Should().BeTrue();

        sut.IsInReviewMode = false;

        sut.IsExpanderExpanded.Should().BeTrue();
    }

    [Fact]
    public void EnteringReviewMode_UserCanStillCollapseAfter()
    {
        // The auto-expand only fires on the transition into Review Mode. After that,
        // the user retains full control via the chevron overlay — collapsing is allowed
        // and must stick (the auto-expand doesn't keep re-firing).
        var sut = CreateSut();

        sut.IsInReviewMode = true;
        sut.IsExpanderExpanded.Should().BeTrue();

        sut.IsExpanderExpanded = false;

        sut.IsExpanderExpanded.Should().BeFalse();
    }
}
