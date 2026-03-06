using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class SettingViewModelEnricherTests
{
    private readonly Mock<IHardwareDetectionService> _mockHardwareDetectionService = new();
    private readonly Mock<ISettingLocalizationService> _mockSettingLocalizationService = new();
    private readonly Mock<ISettingReviewDiffApplier> _mockReviewDiffApplier = new();

    // Dependencies for constructing SettingItemViewModel
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
    }

    private SettingViewModelEnricher CreateService()
    {
        return new SettingViewModelEnricher(
            _mockHardwareDetectionService.Object,
            _mockSettingLocalizationService.Object,
            _mockReviewDiffApplier.Object);
    }

    private SettingItemViewModel CreateSettingViewModel(
        string settingId = "test-setting",
        string name = "Test Setting")
    {
        var settingDef = new SettingDefinition
        {
            Id = settingId,
            Name = name,
            Description = "Test Description"
        };

        var config = new SettingItemViewModelConfig
        {
            SettingDefinition = settingDef,
            SettingId = settingId,
            Name = name,
            Description = "Test Description",
            InputType = InputType.Toggle,
            IsSelected = false
        };

        return new SettingItemViewModel(
            config,
            _mockSettingApplicationService.Object,
            _mockLogService.Object,
            _mockDispatcher.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object);
    }

    // -------------------------------------------------------
    // DetectBatteryAsync
    // -------------------------------------------------------

    [Fact]
    public async Task DetectBatteryAsync_WhenHasBattery_SetsHasBatteryToTrue()
    {
        _mockHardwareDetectionService
            .Setup(h => h.HasBatteryAsync())
            .ReturnsAsync(true);

        var vm = CreateSettingViewModel();
        vm.HasBattery.Should().BeFalse(); // default state

        var service = CreateService();
        await service.DetectBatteryAsync(vm);

        vm.HasBattery.Should().BeTrue();
    }

    [Fact]
    public async Task DetectBatteryAsync_WhenNoBattery_SetsHasBatteryToFalse()
    {
        _mockHardwareDetectionService
            .Setup(h => h.HasBatteryAsync())
            .ReturnsAsync(false);

        var vm = CreateSettingViewModel();

        var service = CreateService();
        await service.DetectBatteryAsync(vm);

        vm.HasBattery.Should().BeFalse();
    }

    [Fact]
    public async Task DetectBatteryAsync_CallsHardwareDetectionService()
    {
        _mockHardwareDetectionService
            .Setup(h => h.HasBatteryAsync())
            .ReturnsAsync(true);

        var vm = CreateSettingViewModel();

        var service = CreateService();
        await service.DetectBatteryAsync(vm);

        _mockHardwareDetectionService.Verify(
            h => h.HasBatteryAsync(),
            Times.Once);
    }

    [Fact]
    public async Task DetectBatteryAsync_UpdatesSpecificViewModel()
    {
        _mockHardwareDetectionService
            .Setup(h => h.HasBatteryAsync())
            .ReturnsAsync(true);

        var vm1 = CreateSettingViewModel(settingId: "setting-1", name: "Setting 1");
        var vm2 = CreateSettingViewModel(settingId: "setting-2", name: "Setting 2");

        var service = CreateService();
        await service.DetectBatteryAsync(vm1);

        vm1.HasBattery.Should().BeTrue();
        vm2.HasBattery.Should().BeFalse();
    }

    // -------------------------------------------------------
    // SetCrossGroupInfoMessage
    // -------------------------------------------------------

    [Fact]
    public void SetCrossGroupInfoMessage_SetsMessageOnViewModel()
    {
        var setting = new SettingDefinition
        {
            Id = "cross-group-setting",
            Name = "Cross Group",
            Description = "Desc"
        };

        _mockSettingLocalizationService
            .Setup(l => l.BuildCrossGroupInfoMessage(setting))
            .Returns("This setting also affects: Gaming > Performance");

        var vm = CreateSettingViewModel();

        var service = CreateService();
        service.SetCrossGroupInfoMessage(vm, setting);

        vm.CrossGroupInfoMessage.Should().Be("This setting also affects: Gaming > Performance");
    }

    [Fact]
    public void SetCrossGroupInfoMessage_WhenNullMessage_SetsNullOnViewModel()
    {
        var setting = new SettingDefinition
        {
            Id = "no-cross-group",
            Name = "No Cross Group",
            Description = "Desc"
        };

        _mockSettingLocalizationService
            .Setup(l => l.BuildCrossGroupInfoMessage(setting))
            .Returns((string?)null);

        var vm = CreateSettingViewModel();

        var service = CreateService();
        service.SetCrossGroupInfoMessage(vm, setting);

        vm.CrossGroupInfoMessage.Should().BeNull();
    }

    [Fact]
    public void SetCrossGroupInfoMessage_CallsLocalizationServiceWithCorrectSetting()
    {
        var setting = new SettingDefinition
        {
            Id = "my-setting",
            Name = "My Setting",
            Description = "Desc"
        };

        _mockSettingLocalizationService
            .Setup(l => l.BuildCrossGroupInfoMessage(It.IsAny<SettingDefinition>()))
            .Returns("msg");

        var vm = CreateSettingViewModel();

        var service = CreateService();
        service.SetCrossGroupInfoMessage(vm, setting);

        _mockSettingLocalizationService.Verify(
            l => l.BuildCrossGroupInfoMessage(setting),
            Times.Once);
    }

    [Fact]
    public void SetCrossGroupInfoMessage_OverwritesPreviousMessage()
    {
        var setting1 = new SettingDefinition
        {
            Id = "setting-1",
            Name = "Setting 1",
            Description = "Desc"
        };
        var setting2 = new SettingDefinition
        {
            Id = "setting-2",
            Name = "Setting 2",
            Description = "Desc"
        };

        _mockSettingLocalizationService
            .Setup(l => l.BuildCrossGroupInfoMessage(setting1))
            .Returns("First message");
        _mockSettingLocalizationService
            .Setup(l => l.BuildCrossGroupInfoMessage(setting2))
            .Returns("Second message");

        var vm = CreateSettingViewModel();

        var service = CreateService();
        service.SetCrossGroupInfoMessage(vm, setting1);
        vm.CrossGroupInfoMessage.Should().Be("First message");

        service.SetCrossGroupInfoMessage(vm, setting2);
        vm.CrossGroupInfoMessage.Should().Be("Second message");
    }

    // -------------------------------------------------------
    // ApplyReviewDiff
    // -------------------------------------------------------

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
