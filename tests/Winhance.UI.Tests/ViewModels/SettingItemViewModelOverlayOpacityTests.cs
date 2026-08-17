using Winhance.TestSupport;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

/// <summary>
/// The outcome marker hides for the duration of an apply. Without this the marker stayed on screen for
/// the whole ~1s write, rendering "Not recognized" on top of the option the user had just picked - two
/// dropdowns drawn over each other until the apply landed.
/// </summary>
public class SettingItemViewModelOverlayOpacityTests
{
    private readonly Mock<ISettingApplicationService> _settingAppService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();

    [Fact]
    public void OverlayOpacity_IsOpaque_WhenIdle()
    {
        var vm = CreateSut();

        vm.IsApplying.Should().BeFalse();
        vm.OverlayOpacity.Should().Be(1d);
    }

    [Fact]
    public void OverlayOpacity_IsTransparent_WhileApplying()
    {
        var vm = CreateSut();

        vm.IsApplying = true;

        vm.OverlayOpacity.Should().Be(0d);
    }

    [Fact]
    public void OverlayOpacity_ReturnsToOpaque_WhenTheApplyFinishes()
    {
        var vm = CreateSut();

        vm.IsApplying = true;
        vm.IsApplying = false;

        vm.OverlayOpacity.Should().Be(1d);
    }

    [Fact]
    public void ApplyingDoesNotChangeTheOutcome_SoTheFeatureBannerSurvivesTheApply()
    {
        var vm = CreateSut(SettingDetectionOutcome.Custom);

        vm.IsApplying = true;

        // Hiding the marker is presentation only. The banner keys off Outcome, and Marco wants it to
        // stay until the write actually lands.
        vm.Outcome.Should().Be(SettingDetectionOutcome.Custom);
        vm.OverlayVisibilityFor(vm.Outcome).Should().Be(Microsoft.UI.Xaml.Visibility.Visible);
    }

    private SettingItemViewModel CreateSut(
        SettingDetectionOutcome outcome = SettingDetectionOutcome.Resolved)
    {
        var config = new SettingItemViewModelConfig
        {
            Setting = new Setting
            {
                Id = "test-setting",
                Display = new() { Name = "Test Setting", Description = "d" },
            },
            SettingId = "test-setting",
            Name = "Test Setting",
            Description = "d",
            InputType = InputType.Selection,
            Outcome = outcome,
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
