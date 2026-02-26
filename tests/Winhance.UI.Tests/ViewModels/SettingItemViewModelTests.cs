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
}
