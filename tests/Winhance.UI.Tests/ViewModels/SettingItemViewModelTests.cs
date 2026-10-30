using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Catalog;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;
using Winhance.TestSupport;

using Winhance.Core.Features.Common.Localization;
namespace Winhance.UI.Tests.ViewModels;

public class SettingItemViewModelTests
{
    private readonly Mock<ISettingApplicationService> _mockSettingApplicationService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IUserPreferencesService> _mockUserPreferencesService = new();
    private readonly Mock<IRegeditLauncher> _mockRegeditLauncher = new();

    private readonly Setting _defaultSetting;
    private readonly SettingItemViewModelConfig _defaultConfig;

    public SettingItemViewModelTests()
    {
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
        // Mirrors the stub above onto TryGetString - an unstubbed Moq answers "missing" for every key.
        _mockLocalizationService.MirrorTryGetString();

        _defaultSetting = new Setting
        {
            Id = "test-setting",
            Display = new() { Name = TestKeys.Of("Test Setting"), Description = TestKeys.Of("A test setting description") },
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
            SettingWriteStrategies.Selector(
                _mockSettingApplicationService.Object, _mockDialogService.Object, _mockLocalizationService.Object, _mockLogService.Object),
            _mockLogService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockUserPreferencesService.Object,
            _mockRegeditLauncher.Object);
    }

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
        sut.TechnicalDetailMatrix.Should().BeNull();
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
            Display = new() { Name = TestKeys.Of("Action"), Description = TestKeys.Of("d") },
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
        // The option-warning banner is computed from config.OptionWarnings, not the Setting, so a minimal synthetic Setting suffices.
        var config = _defaultConfig with
        {
            Setting = new Setting { Id = "gaming-windows-search-service", Display = new() { Name = TestKeys.Of("Windows Search Indexing Service"), Description = TestKeys.Of("desc") } },
            SettingId = "gaming-windows-search-service",
            InputType = InputType.Selection,
            OptionWarnings = new OptionWarning?[] { new("WARNING: Disabling WSearch breaks Outlook search.", false), null },
        };
        var sut = CreateSut(config);

        sut.UpdateStatusBanner(0);

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
            Setting = new Setting { Id = "child-setting", Display = new() { Name = TestKeys.Of("Child"), Description = TestKeys.Of("Child setting") }, UiParentId = "parent-setting" },
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

    [Fact]
    public void UpdateStateFromEvent_ToggleType_UpdatesIsSelected()
    {
        var sut = CreateSut();
        sut.IsSelected.Should().BeFalse();

        sut.UpdateStateFromEvent(true, null);

        sut.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void UpdateStateFromEvent_DuringItsOwnApply_LeavesIsSelectedToTheWriteResult()
    {
        var sut = CreateSut();
        sut.IsApplying = true;

        sut.UpdateStateFromEvent(true, null);

        sut.IsSelected.Should().BeFalse();
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

        sut.IsSelected.Should().BeTrue();
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

        sut.NumericValue.Should().Be(20);
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

        sut.NumericValue.Should().Be(2);
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

        sut.AcNumericValue.Should().Be(20);
        sut.DcNumericValue.Should().Be(10);
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
        sut.DcNumericValue = 99;

        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, AcValue = 1200 });

        sut.AcNumericValue.Should().Be(20);
        sut.DcNumericValue.Should().Be(99);
    }

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

        sut.AcValue.Should().Be(2);
        sut.DcValue.Should().Be(0);
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

        sut.AcValue.Should().Be(ComboBoxConstants.CustomStateIndex);
        sut.DcValue.Should().Be(0);
    }

    [Fact]
    public void UpdateStateFromSystemState_Selection_NonSeparate_UpdatesSelectedValue()
    {
        var config = _defaultConfig with
        {
            Setting = new Setting { Id = "standard-selection", Display = new() { Name = TestKeys.Of("Standard Selection"), Description = TestKeys.Of("Non-separate selection") } },
            SettingId = "standard-selection",
            InputType = InputType.Selection
        };
        var sut = CreateSut(config);

        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, CurrentValue = 2 });

        sut.SelectedValue.Should().Be(2);
    }

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

        sut.NumericValue.Should().Be(42);
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

        sut.UpdateStateFromSystemState(new SettingStateResult { Success = true, CurrentValue = null });

        sut.NumericValue.Should().Be(55);
    }

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
        sut.IsInReviewMode = true;
        sut.IsReviewRejected = true;

        sut.IsReviewApproved = true;

        sut.IsReviewApproved.Should().BeTrue();
        sut.IsReviewRejected.Should().BeFalse();
    }

    [Fact]
    public void IsReviewRejected_SettingTrue_ClearsIsReviewApproved()
    {
        var sut = CreateSut();
        sut.IsInReviewMode = true;
        sut.IsReviewApproved = true;

        sut.IsReviewRejected = true;

        sut.IsReviewRejected.Should().BeTrue();
        sut.IsReviewApproved.Should().BeFalse();
    }

    [Fact]
    public void IsReviewDecisionMade_ReturnsTrueWhenApproved()
    {
        var sut = CreateSut();
        sut.IsInReviewMode = true;

        sut.IsReviewApproved = true;

        sut.IsReviewDecisionMade.Should().BeTrue();
    }

    [Fact]
    public void IsReviewDecisionMade_ReturnsTrueWhenRejected()
    {
        var sut = CreateSut();
        sut.IsInReviewMode = true;

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
        sut.IsInReviewMode = true;
        bool? receivedApproval = null;
        sut.ReviewApprovalChanged += (_, approved) => receivedApproval = approved;

        sut.IsReviewApproved = true;

        receivedApproval.Should().BeTrue();
    }

    [Fact]
    public void ReviewApprovalChanged_RaisedWithFalseWhenRejected()
    {
        var sut = CreateSut();
        sut.IsInReviewMode = true;
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

    [Fact]
    public void RequiresAdvancedUnlock_ReturnsTrueWhenSettingRequiresIt()
    {
        var config = _defaultConfig with
        {
            Setting = new Setting { Id = "advanced-setting", Display = new() { Name = TestKeys.Of("Advanced"), Description = TestKeys.Of("Requires unlock") }, Availability = new Availability { RequiresAdvancedUnlock = true } },
            SettingId = "advanced-setting"
        };
        var sut = CreateSut(config);

        sut.RequiresAdvancedUnlock.Should().BeTrue();
    }

    private static Setting FieldsSetting() => new()
    {
        Id = "dns",
        Display = new() { Name = TestKeys.Of("DNS"), Description = TestKeys.Of("d") },
        Targets = new Target[]
        {
            new DnsTarget("primary", DnsSlot.Preferred),
            new DnsTarget("primaryDoh", DnsSlot.Preferred, DnsPart.Encryption),
            new DnsTarget("secondary", DnsSlot.Alternate),
        },
        States = new[]
        {
            new SettingState { Label = TestKeys.Of("Automatic"), Set = new Dictionary<string, StateValue> { ["primary"] = StateValue.Absent, ["secondary"] = StateValue.Absent } },
            new SettingState { Label = TestKeys.Of("Cloudflare"), Set = new Dictionary<string, StateValue> { ["primary"] = StateValue.Of("1.1.1.1"), ["secondary"] = StateValue.Of("1.0.0.1") } },
        },
        Fields = new[]
        {
            new[]
            {
                new Field("primary", FieldKind.Text, TestKeys.Of("Preferred"), Rule: new TextRule(@"^\d+\.\d+\.\d+\.\d+$", false, TestKeys.Of("Bad"))),
                new Field("primaryDoh", FieldKind.Selection, TestKeys.Of("DoH"), Options: new[] { TestKeys.Of("Off"), TestKeys.Of("Auto"), TestKeys.Of("Manual") },
                    Builds: new[] { BuildRange.Windows11 }),
            },
            new[]
            {
                new Field("secondary", FieldKind.Text, TestKeys.Of("Alternate"), Rule: new TextRule(@"^\d+\.\d+\.\d+\.\d+$", false, TestKeys.Of("Bad"))),
            },
        },
    };

    private static Setting FieldsSettingWithIPv6()
    {
        var setting = FieldsSetting();
        return setting with
        {
            Targets = [.. setting.Targets, new DnsTarget("primary6", DnsSlot.Preferred6)],
            Fields = [.. setting.Fields, [new Field("primary6", FieldKind.Text, TestKeys.Of("Preferred6"))]],
        };
    }

    private SettingItemViewModel LockedFieldsSut(int selected, int build = 26100, Setting? setting = null)
    {
        _mockSettingApplicationService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());
        var sut = CreateSut(SelectionConfig(setting ?? FieldsSetting()) with { Build = new WinBuild(build) });
        sut.IsLocked = true;
        sut.SelectedValue = selected;
        sut.RecordKnownState(new SettingStateResult { Success = true });
        return sut;
    }

    private static ListFieldViewModel Box(SettingItemViewModel sut, string key) =>
        sut.FieldRows.SelectMany(r => r.Fields).Single(f => f.Key == key);

    private static IEnumerable<string> Texts(SettingItemViewModel sut) =>
        sut.FieldRows.SelectMany(r => r.Fields).Where(f => f.Kind == FieldKind.Text).Select(f => f.Text);

    private static bool CarriesEncryptedPreferred(ApplySettingRequest request, string primary) =>
        request.Value is Dictionary<string, object> values
        && Equals(values["primary"], primary) && Equals(values["primaryDoh"], "1");

    private static bool CarriesServers(ApplySettingRequest request, string primary, string secondary) =>
        request.Value is Dictionary<string, object> values
        && Equals(values["primary"], primary) && Equals(values["secondary"], secondary);

    [Fact]
    public void Locked_fields_show_the_selected_presets_servers()
        => Texts(LockedFieldsSut(1)).Should().Equal("1.1.1.1", "1.0.0.1");

    [Fact]
    public void Each_row_holds_one_server()
        => LockedFieldsSut(1).FieldRows.Select(r => r.Fields.Count).Should().Equal(2, 1);

    // Windows 10 has no DNS over HTTPS, so its box is left out there.
    [Fact]
    public void A_box_for_a_later_Windows_is_left_out()
        => LockedFieldsSut(1, build: 19045).FieldRows.Select(r => r.Fields.Count).Should().Equal(1, 1);

    [Fact]
    public void A_custom_reading_shows_the_machines_own_servers()
    {
        var sut = LockedFieldsSut(0);

        sut.RecordKnownState(new SettingStateResult
        {
            Success = true,
            CurrentValue = ComboBoxConstants.CustomStateIndex,
            Readings = new Dictionary<string, object?> { ["primary"] = "10.0.0.1", ["secondary"] = null },
        });
        sut.SelectedValue = ComboBoxConstants.CustomStateIndex;

        Texts(sut).Should().Equal("10.0.0.1", "");
    }

    // Typed servers are a choice the card offers: named, with no fault icon or banner.
    [Fact]
    public void Typed_servers_read_as_custom_rather_than_unrecognized()
    {
        var sut = LockedFieldsSut(0);

        sut.SelectedValue = ComboBoxConstants.CustomStateIndex;
        sut.Outcome = SettingDetectionOutcome.Custom;

        sut.Outcome.Should().Be(SettingDetectionOutcome.Resolved);
        sut.OverlayShowsIconForMode(SettingInputMode.Single).Should().BeFalse();
        sut.OverlayVisibilityForMode(SettingInputMode.Single).Should().Be(Microsoft.UI.Xaml.Visibility.Visible);
    }

    // The preset names only the servers, so the encryption shown comes from the machine while it has that preset.
    [Fact]
    public void An_encrypted_preset_shows_the_machines_encryption()
    {
        var sut = LockedFieldsSut(0);

        sut.RecordKnownState(new SettingStateResult
        {
            Success = true,
            Readings = new Dictionary<string, object?> { ["primary"] = "1.1.1.1", ["primaryDoh"] = 1, ["secondary"] = "1.0.0.1" },
        });
        sut.SelectedValue = 1;

        Box(sut, "primaryDoh").OptionIndex.Should().Be(1);
    }

    [Fact]
    public async Task The_pencil_opens_the_fields_without_the_advanced_warning()
    {
        var sut = LockedFieldsSut(1);

        await sut.UnlockCommand.ExecuteAsync(null);

        sut.IsLocked.Should().BeFalse();
        sut.ShowFieldButtons.Should().BeTrue();
        _mockDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
    }

    [Fact]
    public async Task Typed_servers_apply_as_custom_values()
    {
        var sut = LockedFieldsSut(0);
        await sut.UnlockCommand.ExecuteAsync(null);
        Box(sut, "primary").Text = "10.0.0.1";
        Box(sut, "secondary").Text = "10.0.0.2";

        await sut.ApplyFieldsCommand.ExecuteAsync(null);

        _mockSettingApplicationService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r => CarriesServers(r, "10.0.0.1", "10.0.0.2"))));
        sut.IsLocked.Should().BeTrue();
        sut.SelectedValue.Should().Be(ComboBoxConstants.CustomStateIndex);
    }

    [Fact]
    public async Task Typed_servers_stay_in_the_fields_after_apply_over_a_live_reading()
    {
        var sut = LockedFieldsSut(1);
        sut.RecordKnownState(new SettingStateResult
        {
            Success = true,
            Readings = new Dictionary<string, object?> { ["primary"] = "1.1.1.1", ["secondary"] = "1.0.0.1" },
        });
        await sut.UnlockCommand.ExecuteAsync(null);
        Box(sut, "primary").Text = "10.0.0.1";
        Box(sut, "secondary").Text = "";

        await sut.ApplyFieldsCommand.ExecuteAsync(null);

        Texts(sut).Should().Equal("10.0.0.1", "");
    }

    [Fact]
    public async Task A_presets_servers_typed_out_apply_the_preset()
    {
        var sut = LockedFieldsSut(0);
        await sut.UnlockCommand.ExecuteAsync(null);
        Box(sut, "primary").Text = "1.1.1.1";
        Box(sut, "secondary").Text = "1.0.0.1";

        await sut.ApplyFieldsCommand.ExecuteAsync(null);

        _mockSettingApplicationService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r => Equals(r.Value, 1))));
    }

    // An index carries no encryption, so a preset with encryption on applies as the full set of values.
    [Fact]
    public async Task An_encrypted_preset_applies_as_custom_values()
    {
        var sut = LockedFieldsSut(1);
        await sut.UnlockCommand.ExecuteAsync(null);
        Box(sut, "primaryDoh").OptionIndex = 1;

        await sut.ApplyFieldsCommand.ExecuteAsync(null);

        _mockSettingApplicationService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r => CarriesEncryptedPreferred(r, "1.1.1.1"))));
    }

    private static readonly Dictionary<string, object?> EncryptedCloudflare =
        new() { ["primary"] = "1.1.1.1", ["primaryDoh"] = 1, ["secondary"] = "1.0.0.1" };

    // The card is already on the preset, and the preset's index is the write that removes the encryption.
    [Fact]
    public async Task Turning_encryption_off_on_the_machines_preset_writes_the_preset()
    {
        var sut = LockedFieldsSut(1);
        sut.RecordKnownState(new SettingStateResult { Success = true, Readings = EncryptedCloudflare });
        await sut.UnlockCommand.ExecuteAsync(null);
        Box(sut, "primaryDoh").OptionIndex = 0;

        await sut.ApplyFieldsCommand.ExecuteAsync(null);

        _mockSettingApplicationService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r => Equals(r.Value, 1))), Times.Once);
        sut.SelectedValue.Should().Be(1);
        Box(sut, "primaryDoh").OptionIndex.Should().Be(0);
    }

    [Fact]
    public async Task A_rejected_preset_write_leaves_the_machines_values_in_the_fields()
    {
        var sut = LockedFieldsSut(1);
        sut.RecordKnownState(new SettingStateResult { Success = true, Readings = EncryptedCloudflare });
        _mockSettingApplicationService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Failed("refused"));
        await sut.UnlockCommand.ExecuteAsync(null);
        Box(sut, "primaryDoh").OptionIndex = 0;

        await sut.ApplyFieldsCommand.ExecuteAsync(null);

        sut.IsLocked.Should().BeTrue();
        Box(sut, "primaryDoh").OptionIndex.Should().Be(1);
    }

    [Fact]
    public async Task Clearing_an_ipv6_server_on_the_machines_preset_writes_the_preset()
    {
        var sut = LockedFieldsSut(1, setting: FieldsSettingWithIPv6());
        sut.RecordKnownState(new SettingStateResult
        {
            Success = true,
            Readings = new Dictionary<string, object?> { ["primary"] = "1.1.1.1", ["secondary"] = "1.0.0.1", ["primary6"] = "2606:4700:4700::1111" },
        });
        await sut.UnlockCommand.ExecuteAsync(null);
        Box(sut, "primary6").Text = "";

        await sut.ApplyFieldsCommand.ExecuteAsync(null);

        _mockSettingApplicationService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r => Equals(r.Value, 1))), Times.Once);
        Box(sut, "primary6").Text.Should().BeEmpty();
    }

    [Fact]
    public async Task Picking_a_preset_fills_its_servers_and_empties_the_rest()
    {
        var sut = LockedFieldsSut(0);
        await sut.UnlockCommand.ExecuteAsync(null);
        Box(sut, "primaryDoh").OptionIndex = 1;

        sut.FillFieldsFromPreset(1);

        Texts(sut).Should().Equal("1.1.1.1", "1.0.0.1");
        Box(sut, "primaryDoh").OptionIndex.Should().Be(0);
    }

    [Fact]
    public async Task Picking_Automatic_empties_every_box()
    {
        var sut = LockedFieldsSut(1);
        await sut.UnlockCommand.ExecuteAsync(null);
        Box(sut, "primaryDoh").OptionIndex = 1;

        sut.FillFieldsFromPreset(0);

        Texts(sut).Should().Equal("", "");
        Box(sut, "primaryDoh").OptionIndex.Should().Be(0);
    }

    [Fact]
    public async Task Cancel_puts_back_what_the_card_showed()
    {
        var sut = LockedFieldsSut(1);
        await sut.UnlockCommand.ExecuteAsync(null);
        Box(sut, "primary").Text = "10.9.9.9";

        sut.CancelFieldsCommand.Execute(null);

        sut.IsLocked.Should().BeTrue();
        Texts(sut).Should().Equal("1.1.1.1", "1.0.0.1");
        _mockSettingApplicationService.Verify(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()), Times.Never);
    }

    [Theory]
    [InlineData("", "10.0.0.2", false)]
    [InlineData("10.0.0", "", false)]
    [InlineData("", "", true)]
    [InlineData("10.0.0.1", "", true)]
    public async Task Apply_needs_valid_servers_and_a_preferred_one_first(string preferred, string alternate, bool canApply)
    {
        var sut = LockedFieldsSut(1);
        await sut.UnlockCommand.ExecuteAsync(null);

        Box(sut, "primary").Text = preferred;
        Box(sut, "secondary").Text = alternate;

        sut.CanApplyFields.Should().Be(canApply);
    }

    // A mistyped box says so itself; the line under the fields is for what no single box is wrong about.
    [Theory]
    [InlineData("", "10.0.0.2", true)]
    [InlineData("10.0.0", "", false)]
    [InlineData("10.0.0.1", "10.0.0.2", false)]
    public async Task A_refusal_no_box_explains_shows_under_the_fields(string preferred, string alternate, bool shown)
    {
        var sut = LockedFieldsSut(1);
        sut.ShowFieldsError.Should().BeFalse();
        await sut.UnlockCommand.ExecuteAsync(null);

        Box(sut, "primary").Text = preferred;
        Box(sut, "secondary").Text = alternate;

        sut.ShowFieldsError.Should().Be(shown);
    }

    [Fact]
    public void RequiresAdvancedUnlock_ReturnsFalseWhenSettingDoesNotRequireIt()
    {
        var sut = CreateSut();
        sut.RequiresAdvancedUnlock.Should().BeFalse();
    }

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

    [Fact]
    public void TechnicalDetailsLabel_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();

        sut.TechnicalDetailsLabel.Should().Be("Technical Details");
    }

    [Fact]
    public void ClickToUnlockText_ReturnsFallbackWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();

        sut.ClickToUnlockText.Should().Be("Click to unlock");
    }

    [Fact]
    public void ReviewChoiceLabels_ReturnFallbacksWhenLocalizationReturnsNull()
    {
        var sut = CreateSut();

        sut.ReviewApplyLabel.Should().Be("Apply");
        sut.ReviewDontApplyLabel.Should().Be("Don't apply");
    }

    [Fact]
    public void LanguageChanged_RaisesTheReviewChoiceLabels()
    {
        var sut = CreateSut();
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        _mockLocalizationService.Raise(l => l.LanguageChanged += null, EventArgs.Empty);

        raised.Should().Contain(nameof(sut.ReviewApplyLabel)).And.Contain(nameof(sut.ReviewDontApplyLabel));
    }

    [Fact]
    public void BadgeRow_Toggle_NonSubjective_DisabledMatchesBothRecommendedAndDefault_BothLit()
    {
        var config = ToggleConfig(ToggleSetting("toggle-fax-like", recommendedEnabled: false, defaultEnabled: false));
        var sut = CreateSut(config);
        sut.IsSelected = false;
        sut.ComputeBadgeState();

        var row = sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).ToArray();
        row.Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Recommended, true),
            (SettingBadgeKind.Default,     true),
        }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void WouldQuickSetChange_ToggleAlreadyAtTarget_IsFalse()
    {
        var sut = CreateSut(ToggleConfig(ToggleSetting("toggle-svc", recommendedEnabled: false, defaultEnabled: true)));
        sut.IsSelected = false;
        sut.ComputeBadgeState();

        sut.WouldQuickSetChange(recommended: true).Should().BeFalse();
        sut.WouldQuickSetChange(recommended: false).Should().BeTrue();
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
        }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void BadgeRow_Toggle_InvertedPolicy_ToggleOff_OnlyRecommendedLit()
    {
        // Inverted policy: EnabledValue=[null], DisabledValue=[1],
        // RecommendedToggleState=false (recommend the blocking state).
        // Toggle OFF means user has the recommended blocking state applied.
        var sut = CreateSut(ToggleConfig(ToggleSetting("security-workplace-join-messages-like", recommendedEnabled: false, defaultEnabled: true)));
        sut.IsSelected = false;
        sut.ComputeBadgeState();

        sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Recommended, true),
            (SettingBadgeKind.Default,     false),
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
        }, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void BadgeRow_Toggle_BasePlusNullDefaultPolicyEnforcer_ToggleOff_RecommendedLit()
    {
        var sut = CreateSut(ToggleConfig(ToggleSetting("tailored-experiences-like-off", recommendedEnabled: false, defaultEnabled: true)));
        sut.IsSelected = false;
        sut.ComputeBadgeState();

        sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Recommended, true),
            (SettingBadgeKind.Default,     false),
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
        }, opts => opts.WithStrictOrdering());

        sut.SelectedValue = 1;
        sut.ComputeBadgeState();
        sut.BadgeRow.Select(p => (p.Kind, p.IsHighlighted)).Should().BeEquivalentTo(new[]
        {
            (SettingBadgeKind.Preference, true),
            (SettingBadgeKind.Default,    true),
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
        sut.AcNumericValue = 0;
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
        sut.AcNumericValue = 50;
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
        var sut = CreateSut(NumericConfig(PowerCfgSeparateNumericSetting(
            "acdc-dc-only-rec", recAc: null, recDc: 600, defAc: null, defDc: 1200)));
        sut.HasBattery = true;
        sut.ComputeBadgeState();

        sut.BadgeRow.Should().NotContain(p => p.Kind == SettingBadgeKind.Recommended && p.Mode == SettingBadgeMode.AC);
        sut.BadgeRow.Should().ContainSingle(p => p.Kind == SettingBadgeKind.Recommended && p.Mode == SettingBadgeMode.DC);
    }

    // The SettingItemViewModel reads the PASSED Setting (no live-catalog resolution), so hand-built synthetic
    // Settings carrying exactly the fields the VM reads are correct here -- simpler, non-vacuous, and immune to
    // catalog edits.

    private static Setting ToggleSetting(string id, bool? recommendedEnabled, bool? defaultEnabled)
    {
        var enabled = new List<StateRole>();
        var disabled = new List<StateRole>();
        if (recommendedEnabled is bool r) (r ? enabled : disabled).Add(StateRole.Recommended);
        if (defaultEnabled is bool d) (d ? enabled : disabled).Add(StateRole.WindowsDefault);
        return new Setting
        {
            Id = id,
            Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of("") },
            States = new[]
            {
                new SettingState { Label = LocKey.Common.Enabled, Roles = enabled },
                new SettingState { Label = LocKey.Common.Disabled, Roles = disabled },
            },
        };
    }

    // The badge logic reads only the state Roles + the selected index (never the accept-Set for a non-powercfg
    // selection), so the Set is omitted.
    private static Setting SelectionSetting(string id, (string Label, bool Recommended, bool Default)[] options, bool subjective = false)
    {
        var states = new List<SettingState>();
        foreach (var (label, rec, def) in options)
        {
            var roles = new List<StateRole>();
            if (rec) roles.Add(StateRole.Recommended);
            if (def) roles.Add(StateRole.WindowsDefault);
            states.Add(new SettingState { Label = TestKeys.Of(label), Roles = roles });
        }
        return new Setting
        {
            Id = id,
            Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of(""), IsSubjectivePreference = subjective },
            States = states,
        };
    }

    // Carries only the Always context: registry single-spinner numerics have no powercfg peer.
    private static Setting AlwaysNumericSetting(string id, int min, int max, string? units, int? recommended = null, int? windowsDefault = null)
    {
        var rec = recommended is int r ? new[] { new ContextValue(PowerContext.Always, r) } : System.Array.Empty<ContextValue>();
        var def = windowsDefault is int d ? new[] { new ContextValue(PowerContext.Always, d) } : System.Array.Empty<ContextValue>();
        return new Setting
        {
            Id = id,
            Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of("") },
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
            Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of("") },
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
            states.Add(new SettingState { Label = TestKeys.Of(label), Set = new Dictionary<string, StateValue> { ["Power"] = StateValue.Of(power) } });
        return new Setting
        {
            Id = id,
            Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of("") },
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
            Name = setting.Display.Name.Value,
            Description = setting.Display.Description.Value,
            InputType = InputType.Toggle,
            IsSelected = false,
        };

    private SettingItemViewModelConfig SelectionConfig(Setting setting) =>
        new SettingItemViewModelConfig
        {
            Setting = setting,
            SettingId = setting.Id,
            Name = setting.Display.Name.Value,
            Description = setting.Display.Description.Value,
            InputType = InputType.Selection,
            IsSelected = false,
        };

    private SettingItemViewModelConfig NumericConfig(Setting setting) =>
        new SettingItemViewModelConfig
        {
            Setting = setting,
            SettingId = setting.Id,
            Name = setting.Display.Name.Value,
            Description = setting.Display.Description.Value,
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
    public void AcDcNumericInput_RepeatingTheDisplayedValue_DoesNotWrite()
    {
        _mockSettingApplicationService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var sut = CreateSut(NumericConfig(PowerCfgSeparateNumericSetting("acdc-rebind", recAc: 50, recDc: 25, defAc: 0, defDc: 0)));
        sut.AcNumericValue = 10;
        sut.DcNumericValue = 20;

        // What the NumberBox raises when its binding re-pushes the value it already holds - which is what
        // revealing the DC input does. Builder mode recorded these as edits the user never made.
        sut.AcNumericInputChanged(10);
        sut.DcNumericInputChanged(20);

        _mockSettingApplicationService.Verify(
            s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()), Times.Never);
    }

    [Fact]
    public void AcDcNumericInput_ANewValue_StillWrites()
    {
        _mockSettingApplicationService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        var sut = CreateSut(NumericConfig(PowerCfgSeparateNumericSetting("acdc-edit", recAc: 50, recDc: 25, defAc: 0, defDc: 0)));
        sut.AcNumericValue = 10;
        sut.DcNumericValue = 20;

        sut.AcNumericInputChanged(35);

        sut.AcNumericValue.Should().Be(35);
        _mockSettingApplicationService.Verify(
            s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "acdc-edit")),
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

    [Fact]
    public void WorkplaceJoinMessages_ToggleOff_RecommendedLit_DefaultDim()
    {
        var setting = SettingCatalog.All.First(s => s.Id == "security-workplace-join-messages");

        var config = new SettingItemViewModelConfig
        {
            Setting = setting,
            SettingId = setting.Id,
            Name = setting.Display.Name.Value,
            Description = setting.Display.Description.Value,
            InputType = InputType.Toggle,
            IsSelected = false,
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
            Name = setting.Display.Name.Value,
            Description = setting.Display.Description.Value,
            InputType = InputType.Toggle,
            IsSelected = true,
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
            Name = setting.Display.Name.Value,
            Description = setting.Display.Description.Value,
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
            Name = setting.Display.Name.Value,
            Description = setting.Display.Description.Value,
            InputType = InputType.Toggle,
            IsSelected = true,
        };
        var sut = CreateSut(config);
        sut.ComputeBadgeState();

        sut.BadgeRow.Where(p => p.IsHighlighted).Select(p => p.Kind)
            .Should().BeEquivalentTo(new[] { SettingBadgeKind.Default });
    }

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
        var sut = CreateSut();
        sut.IsInReviewMode = true;
        sut.IsExpanderExpanded.Should().BeTrue();

        sut.IsInReviewMode = false;

        sut.IsExpanderExpanded.Should().BeTrue();
    }

    [Fact]
    public void EnteringReviewMode_UserCanStillCollapseAfter()
    {
        var sut = CreateSut();

        sut.IsInReviewMode = true;
        sut.IsExpanderExpanded.Should().BeTrue();

        sut.IsExpanderExpanded = false;

        sut.IsExpanderExpanded.Should().BeFalse();
    }

    // A regional format writes date pictures no option list can cover, so the card has to say what is actually
    // there rather than only that it does not know it.
    [Fact]
    public void OverlayStateTextFor_OneKeyUnrecognized_NamesTheValue()
    {
        var sut = CreateSut();
        sut.CapturedCustomStateValues = new Dictionary<string, object> { ["sShortDate"] = "dd.MM.yyyy" };

        sut.OverlayStateTextFor(SettingDetectionOutcome.Custom).Should().Contain("dd.MM.yyyy");
    }

    [Fact]
    public void OverlayStateTextFor_SeveralKeys_KeepsThePlainWording()
    {
        var sut = CreateSut();
        sut.CapturedCustomStateValues = new Dictionary<string, object>
        {
            ["primary"] = "10.0.0.1",
            ["secondary"] = "10.0.0.2",
        };

        sut.OverlayStateTextFor(SettingDetectionOutcome.Custom).Should().NotContain("10.0.0.1");
    }
}
