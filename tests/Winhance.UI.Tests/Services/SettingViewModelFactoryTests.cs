using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class SettingViewModelFactoryTests
{
    private readonly Mock<ISettingApplicationService> _mockSettingApplicationService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<IRegeditLauncher> _mockRegeditLauncher = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IUserPreferencesService> _mockUserPreferencesService = new();
    private readonly Mock<IComboBoxSetupService> _mockComboBoxSetupService = new();
    private readonly Mock<IComboBoxResolver> _mockComboBoxResolver = new();
    private readonly Mock<ISettingViewModelEnricher> _mockEnricher = new();

    private readonly SettingViewModelDependencies _deps;
    private readonly SettingViewModelFactory _sut;

    public SettingViewModelFactoryTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());

        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(a => a().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        _deps = new SettingViewModelDependencies(
            _mockSettingApplicationService.Object,
            _mockLogService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockEventBus.Object,
            _mockRegeditLauncher.Object);

        _sut = new SettingViewModelFactory(
            _deps,
            _mockLogService.Object,
            _mockLocalizationService.Object,
            _mockUserPreferencesService.Object,
            _mockComboBoxSetupService.Object,
            _mockComboBoxResolver.Object,
            _mockEnricher.Object);
    }

    // ── CreateAsync basics ──

    [Fact]
    public async Task CreateAsync_ReturnsNonNullViewModel()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { IsEnabled = true, Success = true };

        var result = await _sut.CreateAsync(setting, state, null);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_SetsSettingId()
    {
        var setting = CreateToggleSetting("MySetting");
        var state = new SettingStateResult { Success = true };

        var result = await _sut.CreateAsync(setting, state, null);

        result.SettingId.Should().Be("MySetting");
    }

    [Fact]
    public async Task CreateAsync_SetsNameAndDescription()
    {
        var setting = CreateToggleSetting("TestSetting", "Test Name", "Test Description");
        var state = new SettingStateResult { Success = true };

        var result = await _sut.CreateAsync(setting, state, null);

        result.Name.Should().Be("Test Name");
        result.Description.Should().Be("Test Description");
    }

    [Fact]
    public async Task CreateAsync_SetsGroupName()
    {
        var setting = CreateToggleSetting("TestSetting") with { GroupName = "Privacy Settings" };
        var state = new SettingStateResult { Success = true };

        var result = await _sut.CreateAsync(setting, state, null);

        result.GroupName.Should().Be("Privacy Settings");
    }

    [Fact]
    public async Task CreateAsync_SetsIsSelectedFromCurrentState()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { IsEnabled = true, Success = true };

        var result = await _sut.CreateAsync(setting, state, null);

        result.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenNotEnabled_SetsIsSelectedToFalse()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { IsEnabled = false, Success = true };

        var result = await _sut.CreateAsync(setting, state, null);

        result.IsSelected.Should().BeFalse();
    }

    // ── Advanced unlock settings ──

    [Fact]
    public async Task CreateAsync_WhenRequiresAdvancedUnlock_SetsIsLocked()
    {
        var setting = CreateToggleSetting("AdvancedSetting") with { RequiresAdvancedUnlock = true };
        var state = new SettingStateResult { Success = true };

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("AdvancedPowerSettingsUnlocked", false))
            .ReturnsAsync(false);

        var result = await _sut.CreateAsync(setting, state, null);

        result.IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenAdvancedUnlocked_SetsIsLockedToFalse()
    {
        var setting = CreateToggleSetting("AdvancedSetting") with { RequiresAdvancedUnlock = true };
        var state = new SettingStateResult { Success = true };

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("AdvancedPowerSettingsUnlocked", false))
            .ReturnsAsync(true);

        var result = await _sut.CreateAsync(setting, state, null);

        result.IsLocked.Should().BeFalse();
    }

    // ── Numeric range settings ──

    [Fact]
    public async Task CreateAsync_NumericRangeSetting_SetsMinMaxValues()
    {
        var setting = CreateNumericRangeSetting("NumericSetting", 0, 100, "ms");
        var state = new SettingStateResult { CurrentValue = 50, Success = true };

        var result = await _sut.CreateAsync(setting, state, null);

        result.MinValue.Should().Be(0);
        result.MaxValue.Should().Be(100);
        result.Units.Should().Be("ms");
    }

    [Fact]
    public async Task CreateAsync_NumericRangeSetting_SetsNumericValue()
    {
        var setting = CreateNumericRangeSetting("NumericSetting", 0, 100, "ms");
        var state = new SettingStateResult { CurrentValue = 42, Success = true };

        var result = await _sut.CreateAsync(setting, state, null);

        result.NumericValue.Should().Be(42);
    }

    // ── Selection settings ──

    [Fact]
    public async Task CreateAsync_SelectionSetting_PopulatesComboBoxOptions()
    {
        var setting = CreateSelectionSetting("SelectionSetting");
        var state = new SettingStateResult { CurrentValue = 1, Success = true };

        var comboBoxResult = new ComboBoxSetupResult
        {
            Options = new ObservableCollection<ComboBoxOption>
            {
                new ComboBoxOption("Option A", 0),
                new ComboBoxOption("Option B", 1)
            },
            SelectedValue = 1,
            Success = true
        };

        _mockComboBoxSetupService
            .Setup(s => s.SetupComboBoxOptionsAsync(setting, It.IsAny<object?>()))
            .ReturnsAsync(comboBoxResult);

        var result = await _sut.CreateAsync(setting, state, null);

        result.ComboBoxOptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_SelectionSetting_SetsSelectedValue()
    {
        var setting = CreateSelectionSetting("SelectionSetting");
        var state = new SettingStateResult { CurrentValue = 1, Success = true };

        var comboBoxResult = new ComboBoxSetupResult
        {
            Options = new ObservableCollection<ComboBoxOption>
            {
                new ComboBoxOption("Option A", 0),
                new ComboBoxOption("Option B", 1)
            },
            SelectedValue = 1,
            Success = true
        };

        _mockComboBoxSetupService
            .Setup(s => s.SetupComboBoxOptionsAsync(setting, It.IsAny<object?>()))
            .ReturnsAsync(comboBoxResult);

        var result = await _sut.CreateAsync(setting, state, null);

        result.SelectedValue.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_SelectionSetting_WhenSetupFails_LogsWarning()
    {
        var setting = CreateSelectionSetting("SelectionSetting");
        var state = new SettingStateResult { CurrentValue = 1, Success = true };

        _mockComboBoxSetupService
            .Setup(s => s.SetupComboBoxOptionsAsync(setting, It.IsAny<object?>()))
            .ThrowsAsync(new Exception("Setup failed"));

        var result = await _sut.CreateAsync(setting, state, null);

        _mockLogService.Verify(l => l.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("SelectionSetting"))), Times.Once);
    }

    // ── Review diff ──

    [Fact]
    public async Task CreateAsync_CallsApplyReviewDiff()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { Success = true };

        await _sut.CreateAsync(setting, state, null);

        _mockEnricher.Verify(e => e.ApplyReviewDiff(It.IsAny<SettingItemViewModel>(), state), Times.Once);
    }

    // ── Non-selection types call InitializeCompatibilityBanner ──

    [Fact]
    public async Task CreateAsync_NonSelectionType_SetsSelectedValueFromCurrentState()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { CurrentValue = "SomeValue", Success = true };

        var result = await _sut.CreateAsync(setting, state, null);

        result.SelectedValue.Should().Be("SomeValue");
    }

    // ── Parent VM ──

    [Fact]
    public async Task CreateAsync_PassesParentViewModelToConfig()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { Success = true };
        var parentVm = new Mock<ISettingsFeatureViewModel>().Object;

        var result = await _sut.CreateAsync(setting, state, parentVm);

        // The VM was created successfully with the parent reference
        result.Should().NotBeNull();
    }

    // ── Localization ──

    [Fact]
    public async Task CreateAsync_SetsOnAndOffTextFromLocalization()
    {
        _mockLocalizationService
            .Setup(l => l.GetString("Common_On"))
            .Returns("Enabled");

        _mockLocalizationService
            .Setup(l => l.GetString("Common_Off"))
            .Returns("Disabled");

        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { Success = true };

        var result = await _sut.CreateAsync(setting, state, null);

        result.OnText.Should().Be("Enabled");
        result.OffText.Should().Be("Disabled");
    }

    // ── Helper methods ──

    private static SettingDefinition CreateToggleSetting(
        string id, string name = "Test", string description = "Test Description")
    {
        return new SettingDefinition
        {
            Id = id,
            Name = name,
            Description = description,
            InputType = InputType.Toggle,
            GroupName = string.Empty,
            Icon = "TestIcon",
            IconPack = "Material"
        };
    }

    private static SettingDefinition CreateNumericRangeSetting(
        string id, int min, int max, string units)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = "Numeric",
            Description = "Numeric setting",
            InputType = InputType.NumericRange,
            GroupName = string.Empty,
            CustomProperties = new Dictionary<string, object>
            {
                { "MinValue", min },
                { "MaxValue", max },
                { "Units", units }
            }
        };
    }

    private static SettingDefinition CreateSelectionSetting(string id)
    {
        return new SettingDefinition
        {
            Id = id,
            Name = "Selection",
            Description = "Selection setting",
            InputType = InputType.Selection,
            GroupName = string.Empty
        };
    }
}
