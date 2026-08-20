using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Catalog;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.Services;

public class SettingViewModelEnricherTests
{
    private readonly Mock<IHardwareDetectionService> _mockHardwareDetectionService = new();
    private readonly Mock<ISettingReviewDiffApplier> _mockReviewDiffApplier = new();

    private readonly Mock<ISettingApplicationService> _mockSettingApplicationService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IDispatcherService> _mockDispatcher = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();

    public SettingViewModelEnricherTests()
    {
        _mockDispatcher
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(action => action());

        _mockDispatcher
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(action => action());

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        // Mirrors the stub above onto TryGetString - an unstubbed Moq answers "missing" for every key.
        _mockLocalizationService.MirrorTryGetString();
    }

    private SettingViewModelEnricher CreateService()
    {
        return new SettingViewModelEnricher(
            _mockHardwareDetectionService.Object,
            _mockReviewDiffApplier.Object);
    }

    private SettingItemViewModel CreateSettingViewModel(
        string settingId = "test-setting",
        string name = "Test Setting")
    {
        var config = new SettingItemViewModelConfig
        {
            Setting = new Setting { Id = settingId, Display = new() { Name = name, Description = "Test Description" } },
            SettingId = settingId,
            Name = name,
            Description = "Test Description",
            InputType = InputType.Toggle,
            IsSelected = false
        };

        return new SettingItemViewModel(
            config,
            SettingWriteStrategies.Selector(
                _mockSettingApplicationService.Object, _mockDialogService.Object, _mockLocalizationService.Object, _mockLogService.Object),
            _mockLogService.Object,
            _mockDispatcher.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object);
    }

    [Fact]
    public async Task DetectBatteryAsync_WhenHasBattery_SetsHasBatteryToTrue()
    {
        _mockHardwareDetectionService
            .Setup(h => h.HasBattery())
            .Returns(true);

        var vm = CreateSettingViewModel();
        vm.HasBattery.Should().BeFalse();

        var service = CreateService();
        await service.DetectBatteryAsync(vm);

        vm.HasBattery.Should().BeTrue();
    }

    [Fact]
    public async Task DetectBatteryAsync_WhenDetectionFails_ShowsBothAcAndDc()
    {
        // null = the WMI probe could not answer. A card shows both halves rather than hiding DC.
        _mockHardwareDetectionService
            .Setup(h => h.HasBattery())
            .Returns((bool?)null);

        var vm = CreateSettingViewModel();

        var service = CreateService();
        await service.DetectBatteryAsync(vm);

        vm.HasBattery.Should().BeTrue();
    }

    [Fact]
    public async Task DetectBatteryAsync_WhenNoBattery_SetsHasBatteryToFalse()
    {
        _mockHardwareDetectionService
            .Setup(h => h.HasBattery())
            .Returns(false);

        var vm = CreateSettingViewModel();

        var service = CreateService();
        await service.DetectBatteryAsync(vm);

        vm.HasBattery.Should().BeFalse();
    }

    [Fact]
    public async Task DetectBatteryAsync_CallsHardwareDetectionService()
    {
        _mockHardwareDetectionService
            .Setup(h => h.HasBattery())
            .Returns(true);

        var vm = CreateSettingViewModel();

        var service = CreateService();
        await service.DetectBatteryAsync(vm);

        _mockHardwareDetectionService.Verify(
            h => h.HasBattery(),
            Times.Once);
    }

    [Fact]
    public async Task DetectBatteryAsync_UpdatesSpecificViewModel()
    {
        _mockHardwareDetectionService
            .Setup(h => h.HasBattery())
            .Returns(true);

        var vm1 = CreateSettingViewModel(settingId: "setting-1", name: "Setting 1");
        var vm2 = CreateSettingViewModel(settingId: "setting-2", name: "Setting 2");

        var service = CreateService();
        await service.DetectBatteryAsync(vm1);

        vm1.HasBattery.Should().BeTrue();
        vm2.HasBattery.Should().BeFalse();
    }

    [Fact]
    public void ApplyReviewDiff_DelegatesToReviewDiffApplier()
    {
        var vm = CreateSettingViewModel();
        var state = new SettingStateResult { IsEnabled = true };

        var service = CreateService();
        service.ApplyReviewDiff(vm, state);

        _mockReviewDiffApplier.Verify(
            a => a.ApplyReviewDiffToViewModel(vm, state),
            Times.Once);
    }

    [Fact]
    public void ApplyReviewDiff_PassesExactViewModelAndState()
    {
        var vm = CreateSettingViewModel(settingId: "specific-setting", name: "Specific");
        var state = new SettingStateResult
        {
            IsEnabled = false,
            CurrentValue = 42
        };

        SettingItemViewModel? capturedVm = null;
        SettingStateResult? capturedState = null;

        _mockReviewDiffApplier
            .Setup(a => a.ApplyReviewDiffToViewModel(
                It.IsAny<SettingItemViewModel>(),
                It.IsAny<SettingStateResult>()))
            .Callback<SettingItemViewModel, SettingStateResult>((v, s) =>
            {
                capturedVm = v;
                capturedState = s;
            });

        var service = CreateService();
        service.ApplyReviewDiff(vm, state);

        capturedVm.Should().BeSameAs(vm);
        capturedState.Should().BeSameAs(state);
    }

    [Fact]
    public void ApplyReviewDiff_WithDisabledState_DelegatesToApplier()
    {
        var vm = CreateSettingViewModel();
        var state = new SettingStateResult { IsEnabled = false };

        var service = CreateService();
        service.ApplyReviewDiff(vm, state);

        _mockReviewDiffApplier.Verify(
            a => a.ApplyReviewDiffToViewModel(vm, state),
            Times.Once);
    }

    [Fact]
    public void ApplyReviewDiff_WithEnabledState_DelegatesToApplier()
    {
        var vm = CreateSettingViewModel();
        var state = new SettingStateResult { IsEnabled = true };

        var service = CreateService();
        service.ApplyReviewDiff(vm, state);

        _mockReviewDiffApplier.Verify(
            a => a.ApplyReviewDiffToViewModel(vm, state),
            Times.Once);
    }
}
