using Winhance.TestSupport;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Controls;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

// A detect-only state has no ComboBox item; left alone that shows an EMPTY dropdown. The card draws the state's
// own NAME in the outcome overlay: no synthetic option, no fault icon or banner, because nothing is wrong.
public class SettingItemViewModelDetectOnlyStateTests
{
    private readonly Mock<ISettingApplicationService> _settingAppService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();

    [Fact]
    public void DetectOnlySelectedState_IsTheState_WhenTheSelectionSitsOnOne()
    {
        var vm = CreateSut();

        vm.SelectedValue = 2;

        vm.DetectOnlySelectedState.Should().NotBeNull();
        vm.DetectOnlySelectedState!.Label.Should().Be("Mixed");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(ComboBoxConstants.CustomStateIndex)]
    public void DetectOnlySelectedState_IsNull_ForEveryChoosableOrUnresolvedSelection(int selected)
    {
        var vm = CreateSut();

        vm.SelectedValue = selected;

        vm.DetectOnlySelectedState.Should().BeNull();
    }

    [Fact]
    public void TheComboBox_SelectsNothing_RatherThanAnIndexItHasNoItemFor()
    {
        // Binding the raw state index would point a 2-item ComboBox at position 2.
        var vm = CreateSut();

        vm.SelectedValue = 2;

        vm.ComboIndexForMode(SettingInputMode.Single).Should().Be(ComboBoxConstants.CustomStateIndex);
    }

    [Fact]
    public void TheOverlay_ShowsTheStateName_NotTheNotRecognizedText()
    {
        // The whole point: the card names the state. GetString is unstubbed here, so the lookup misses and
        // the raw catalog Label is the fallback - in the app it resolves Setting_{id}_Option_2.
        var vm = CreateSut();

        vm.SelectedValue = 2;

        vm.OverlayVisibilityForMode(SettingInputMode.Single)
            .Should().Be(Microsoft.UI.Xaml.Visibility.Visible);
        vm.OverlayTextForMode(SettingInputMode.Single).Should().Be("Mixed");
        vm.DetectOnlyStateText.Should().Be("Mixed");
    }

    [Fact]
    public void TheOverlay_DrawsNoOutcomeIconAndNoTooltip_BecauseNothingIsWrong()
    {
        var vm = CreateSut();

        vm.SelectedValue = 2;

        vm.OverlayShowsIconForMode(SettingInputMode.Single).Should().BeFalse();
        vm.OverlayTooltipForMode(SettingInputMode.Single, toggleLike: false).Should().BeEmpty();
    }

    [Fact]
    public void TheOutcome_StaysResolved_SoNoDetectionBannerIsRaised()
    {
        var vm = CreateSut();

        vm.SelectedValue = 2;

        vm.Outcome.Should().Be(SettingDetectionOutcome.Resolved);
        vm.OutcomeForMode(SettingInputMode.Single).Should().Be(SettingDetectionOutcome.Resolved);
    }

    [Fact]
    public void AChoosableSelection_StillGetsTheNormalTreatment()
    {
        // Non-vacuity: the detect-only branch must not swallow the ordinary path.
        var vm = CreateSut();

        vm.SelectedValue = 1;

        vm.ComboIndexForMode(SettingInputMode.Single).Should().Be(1);
        vm.OverlayVisibilityForMode(SettingInputMode.Single)
            .Should().Be(Microsoft.UI.Xaml.Visibility.Collapsed);
        vm.OverlayShowsIconForMode(SettingInputMode.Single).Should().BeTrue();
        vm.DetectOnlyStateText.Should().BeEmpty();
    }

    private SettingItemViewModel CreateSut()
    {
        var config = new SettingItemViewModelConfig
        {
            Setting = new Setting
            {
                Id = "test-master",
                Display = new() { Name = "Test Master", Description = "d" },
                States = new[]
                {
                    new SettingState { Label = "Light Mode" },
                    new SettingState { Label = "Dark Mode" },
                    new SettingState { Label = "Mixed", IsFallback = true, IsDetectOnly = true },
                },
            },
            SettingId = "test-master",
            Name = "Test Master",
            Description = "d",
            InputType = InputType.Selection,
            Outcome = SettingDetectionOutcome.Resolved,
        };

        return new SettingItemViewModel(
            config,
            SettingWriteStrategies.Selector(
                _settingAppService.Object, _dialogService.Object, _localizationService.Object, _logService.Object),
            _logService.Object,
            _dispatcherService.Object,
            _dialogService.Object,
            _localizationService.Object);
    }
}
