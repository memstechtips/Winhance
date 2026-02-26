using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Optimize.ViewModels;
using System.Collections.ObjectModel;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class SettingReviewDiffApplierTests
{
    private readonly Mock<IConfigReviewModeService> _mockConfigReviewModeService = new();
    private readonly Mock<IConfigReviewDiffService> _mockConfigReviewDiffService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<ISettingApplicationService> _mockSettingApplicationService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IDispatcherService> _mockDispatcher = new();
    private readonly Mock<IDialogService> _mockDialogService = new();

    public SettingReviewDiffApplierTests()
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

        _mockLocalizationService
            .Setup(l => l.GetString("Review_Mode_Diff_Toggle"))
            .Returns("Current: {0} -> Config: {1}");

        _mockLocalizationService
            .Setup(l => l.GetString("Common_On"))
            .Returns("On");

        _mockLocalizationService
            .Setup(l => l.GetString("Common_Off"))
            .Returns("Off");
    }

    private SettingReviewDiffApplier CreateService()
    {
        return new SettingReviewDiffApplier(
            _mockConfigReviewModeService.Object,
            _mockConfigReviewDiffService.Object,
            _mockLocalizationService.Object);
    }

    private SettingItemViewModel CreateSettingViewModel(
        string settingId = "test-setting",
        string name = "Test Setting",
        InputType inputType = InputType.Toggle,
        bool isSelected = false,
        object? selectedValue = null)
    {
        var settingDef = new SettingDefinition
        {
            Id = settingId,
            Name = name,
            Description = "Test",
            InputType = inputType
        };

        var config = new SettingItemViewModelConfig
        {
            SettingDefinition = settingDef,
            SettingId = settingId,
            Name = name,
            Description = "Test",
            InputType = inputType,
            IsSelected = isSelected
        };

        var vm = new SettingItemViewModel(
            config,
            _mockSettingApplicationService.Object,
            _mockLogService.Object,
            _mockDispatcher.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object);

        if (selectedValue != null)
            vm.SelectedValue = selectedValue;

        return vm;
    }

    // -------------------------------------------------------
    // ApplyReviewDiffToViewModel - No active config
    // -------------------------------------------------------

    [Fact]
    public void ApplyReviewDiffToViewModel_WhenNoActiveConfig_DoesNothing()
    {
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig)
            .Returns((UnifiedConfigurationFile?)null);

        var vm = CreateSettingViewModel();
        var state = new SettingStateResult { IsEnabled = true };

        var service = CreateService();
        service.ApplyReviewDiffToViewModel(vm, state);

        vm.IsInReviewMode.Should().BeFalse();
        vm.HasReviewDiff.Should().BeFalse();
    }

    // -------------------------------------------------------
    // ApplyReviewDiffToViewModel - With existing eager diff
    // -------------------------------------------------------

    [Fact]
    public void ApplyReviewDiffToViewModel_WithExistingDiff_SetsReviewModeProperties()
    {
        var activeConfig = new UnifiedConfigurationFile();
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(activeConfig);

        var diff = new ConfigReviewDiff
        {
            SettingId = "test-setting",
            CurrentValueDisplay = "Off",
            ConfigValueDisplay = "On",
            IsReviewed = false,
            IsApproved = false
        };
        _mockConfigReviewDiffService
            .Setup(d => d.GetDiffForSetting("test-setting"))
            .Returns(diff);

        var vm = CreateSettingViewModel();
        var state = new SettingStateResult { IsEnabled = false };

        var service = CreateService();
        service.ApplyReviewDiffToViewModel(vm, state);

        vm.IsInReviewMode.Should().BeTrue();
        vm.HasReviewDiff.Should().BeTrue();
        vm.ReviewDiffMessage.Should().Contain("Off").And.Contain("On");
    }

    [Fact]
    public void ApplyReviewDiffToViewModel_WithExistingDiff_ActionSetting_ShowsActionMessage()
    {
        var activeConfig = new UnifiedConfigurationFile();
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(activeConfig);

        var diff = new ConfigReviewDiff
        {
            SettingId = "taskbar-clean",
            IsActionSetting = true,
            ActionConfirmationMessage = "Clean the taskbar?",
            CurrentValueDisplay = "",
            ConfigValueDisplay = ""
        };
        _mockConfigReviewDiffService
            .Setup(d => d.GetDiffForSetting("taskbar-clean"))
            .Returns(diff);

        var vm = CreateSettingViewModel(settingId: "taskbar-clean", name: "Clean Taskbar");
        var state = new SettingStateResult();

        var service = CreateService();
        service.ApplyReviewDiffToViewModel(vm, state);

        vm.HasReviewDiff.Should().BeTrue();
        vm.ReviewDiffMessage.Should().Be("Clean the taskbar?");
    }

    [Fact]
    public void ApplyReviewDiffToViewModel_WithReviewedApprovedDiff_RestoresDecisionState()
    {
        var activeConfig = new UnifiedConfigurationFile();
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(activeConfig);

        var diff = new ConfigReviewDiff
        {
            SettingId = "test-setting",
            CurrentValueDisplay = "Off",
            ConfigValueDisplay = "On",
            IsReviewed = true,
            IsApproved = true
        };
        _mockConfigReviewDiffService
            .Setup(d => d.GetDiffForSetting("test-setting"))
            .Returns(diff);

        var vm = CreateSettingViewModel();
        var state = new SettingStateResult { IsEnabled = false };

        var service = CreateService();
        service.ApplyReviewDiffToViewModel(vm, state);

        vm.IsReviewApproved.Should().BeTrue();
        vm.IsReviewRejected.Should().BeFalse();
    }

    [Fact]
    public void ApplyReviewDiffToViewModel_WithReviewedRejectedDiff_RestoresDecisionState()
    {
        var activeConfig = new UnifiedConfigurationFile();
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(activeConfig);

        var diff = new ConfigReviewDiff
        {
            SettingId = "test-setting",
            CurrentValueDisplay = "Off",
            ConfigValueDisplay = "On",
            IsReviewed = true,
            IsApproved = false
        };
        _mockConfigReviewDiffService
            .Setup(d => d.GetDiffForSetting("test-setting"))
            .Returns(diff);

        var vm = CreateSettingViewModel();
        var state = new SettingStateResult { IsEnabled = false };

        var service = CreateService();
        service.ApplyReviewDiffToViewModel(vm, state);

        vm.IsReviewRejected.Should().BeTrue();
    }

    // -------------------------------------------------------
    // ApplyReviewDiffToViewModel - No eager diff, setting not in config
    // -------------------------------------------------------

    [Fact]
    public void ApplyReviewDiffToViewModel_SettingNotInConfig_OnlySetsReviewMode()
    {
        var activeConfig = new UnifiedConfigurationFile();
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(activeConfig);
        _mockConfigReviewDiffService
            .Setup(d => d.GetDiffForSetting("test-setting"))
            .Returns((ConfigReviewDiff?)null);

        var vm = CreateSettingViewModel();
        var state = new SettingStateResult { IsEnabled = true };

        var service = CreateService();
        service.ApplyReviewDiffToViewModel(vm, state);

        vm.IsInReviewMode.Should().BeTrue();
        vm.HasReviewDiff.Should().BeFalse();
    }

    // -------------------------------------------------------
    // ApplyReviewDiffToViewModel - No eager diff, toggle diff computed
    // -------------------------------------------------------

    [Fact]
    public void ApplyReviewDiffToViewModel_Toggle_WithDiff_RegistersDiff()
    {
        var activeConfig = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Privacy"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "test-setting",
                                Name = "Test",
                                IsSelected = true,
                                InputType = InputType.Toggle
                            }
                        }
                    }
                }
            }
        };
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(activeConfig);
        _mockConfigReviewDiffService
            .Setup(d => d.GetDiffForSetting("test-setting"))
            .Returns((ConfigReviewDiff?)null);

        var vm = CreateSettingViewModel();
        var state = new SettingStateResult { IsEnabled = false };

        var service = CreateService();
        service.ApplyReviewDiffToViewModel(vm, state);

        vm.HasReviewDiff.Should().BeTrue();
        vm.ReviewDiffMessage.Should().Contain("Off").And.Contain("On");

        _mockConfigReviewDiffService.Verify(
            d => d.RegisterDiff(It.Is<ConfigReviewDiff>(diff =>
                diff.SettingId == "test-setting" &&
                diff.FeatureModuleId == "Privacy")),
            Times.Once);
    }

    [Fact]
    public void ApplyReviewDiffToViewModel_Toggle_NoDiff_DoesNotRegister()
    {
        var activeConfig = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Privacy"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "test-setting",
                                Name = "Test",
                                IsSelected = true,
                                InputType = InputType.Toggle
                            }
                        }
                    }
                }
            }
        };
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(activeConfig);
        _mockConfigReviewDiffService
            .Setup(d => d.GetDiffForSetting("test-setting"))
            .Returns((ConfigReviewDiff?)null);

        var vm = CreateSettingViewModel(isSelected: true);
        var state = new SettingStateResult { IsEnabled = true };

        var service = CreateService();
        service.ApplyReviewDiffToViewModel(vm, state);

        vm.HasReviewDiff.Should().BeFalse();
        _mockConfigReviewDiffService.Verify(
            d => d.RegisterDiff(It.IsAny<ConfigReviewDiff>()),
            Times.Never);
    }

    // -------------------------------------------------------
    // ApplyReviewDiffToViewModel - Selection type
    // -------------------------------------------------------

    [Fact]
    public void ApplyReviewDiffToViewModel_Selection_WithDifferentIndex_RegistersDiff()
    {
        var activeConfig = new UnifiedConfigurationFile
        {
            Customize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Taskbar"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "selection-setting",
                                Name = "Selection",
                                InputType = InputType.Selection,
                                SelectedIndex = 2
                            }
                        }
                    }
                }
            }
        };
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(activeConfig);
        _mockConfigReviewDiffService
            .Setup(d => d.GetDiffForSetting("selection-setting"))
            .Returns((ConfigReviewDiff?)null);

        var vm = CreateSettingViewModel(
            settingId: "selection-setting",
            name: "Selection",
            inputType: InputType.Selection,
            selectedValue: 0);

        vm.ComboBoxOptions = new ObservableCollection<ComboBoxOption>
        {
            new ComboBoxOption("Option A", 0),
            new ComboBoxOption("Option B", 1),
            new ComboBoxOption("Option C", 2)
        };

        var state = new SettingStateResult { CurrentValue = 0 };

        var service = CreateService();
        service.ApplyReviewDiffToViewModel(vm, state);

        vm.HasReviewDiff.Should().BeTrue();
        _mockConfigReviewDiffService.Verify(
            d => d.RegisterDiff(It.Is<ConfigReviewDiff>(diff =>
                diff.SettingId == "selection-setting")),
            Times.Once);
    }

    [Fact]
    public void ApplyReviewDiffToViewModel_Selection_SameIndex_NoDiff()
    {
        var activeConfig = new UnifiedConfigurationFile
        {
            Customize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Taskbar"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "selection-setting",
                                Name = "Selection",
                                InputType = InputType.Selection,
                                SelectedIndex = 1
                            }
                        }
                    }
                }
            }
        };
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(activeConfig);
        _mockConfigReviewDiffService
            .Setup(d => d.GetDiffForSetting("selection-setting"))
            .Returns((ConfigReviewDiff?)null);

        var vm = CreateSettingViewModel(
            settingId: "selection-setting",
            name: "Selection",
            inputType: InputType.Selection,
            selectedValue: 1);

        vm.ComboBoxOptions = new ObservableCollection<ComboBoxOption>
        {
            new ComboBoxOption("Option A", 0),
            new ComboBoxOption("Option B", 1)
        };

        var state = new SettingStateResult { CurrentValue = 1 };

        var service = CreateService();
        service.ApplyReviewDiffToViewModel(vm, state);

        vm.HasReviewDiff.Should().BeFalse();
    }

    [Fact]
    public void ApplyReviewDiffToViewModel_Selection_WithNullSelectedIndex_NoDiff()
    {
        var activeConfig = new UnifiedConfigurationFile
        {
            Optimize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["Power"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "selection-setting",
                                Name = "Selection",
                                InputType = InputType.Selection,
                                SelectedIndex = null
                            }
                        }
                    }
                }
            }
        };
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(activeConfig);
        _mockConfigReviewDiffService
            .Setup(d => d.GetDiffForSetting("selection-setting"))
            .Returns((ConfigReviewDiff?)null);

        var vm = CreateSettingViewModel(
            settingId: "selection-setting",
            name: "Selection",
            inputType: InputType.Selection,
            selectedValue: 0);

        var state = new SettingStateResult { CurrentValue = 0 };

        var service = CreateService();
        service.ApplyReviewDiffToViewModel(vm, state);

        vm.HasReviewDiff.Should().BeFalse();
    }

    // -------------------------------------------------------
    // ApplyReviewDiffToViewModel - Searches both Optimize and Customize
    // -------------------------------------------------------

    [Fact]
    public void ApplyReviewDiffToViewModel_FindsSettingInCustomizeSection()
    {
        var activeConfig = new UnifiedConfigurationFile
        {
            Customize = new FeatureGroupSection
            {
                Features = new Dictionary<string, ConfigSection>
                {
                    ["WindowsTheme"] = new ConfigSection
                    {
                        Items = new List<ConfigurationItem>
                        {
                            new ConfigurationItem
                            {
                                Id = "theme-setting",
                                Name = "Theme",
                                IsSelected = true,
                                InputType = InputType.Toggle
                            }
                        }
                    }
                }
            }
        };
        _mockConfigReviewModeService.Setup(r => r.ActiveConfig).Returns(activeConfig);
        _mockConfigReviewDiffService
            .Setup(d => d.GetDiffForSetting("theme-setting"))
            .Returns((ConfigReviewDiff?)null);

        var vm = CreateSettingViewModel(settingId: "theme-setting", name: "Theme");
        var state = new SettingStateResult { IsEnabled = false };

        var service = CreateService();
        service.ApplyReviewDiffToViewModel(vm, state);

        vm.HasReviewDiff.Should().BeTrue();
        _mockConfigReviewDiffService.Verify(
            d => d.RegisterDiff(It.Is<ConfigReviewDiff>(diff =>
                diff.FeatureModuleId == "WindowsTheme")),
            Times.Once);
    }
}
