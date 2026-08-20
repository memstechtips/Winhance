using FluentAssertions;
using Moq;
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

public class SettingViewModelFactoryTests
{
    private readonly Mock<ISettingApplicationService> _mockSettingApplicationService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<IRegeditLauncher> _mockRegeditLauncher = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IUserPreferencesService> _mockUserPreferencesService = new();
    private readonly Mock<INewBadgeService> _mockNewBadgeService = new();
    private readonly Mock<ISettingViewModelEnricher> _mockEnricher = new();
    private readonly Mock<IApplicationModeService> _mockApplicationModeService = new();

    private readonly SettingViewModelDependencies _deps;
    private readonly SettingViewModelFactory _sut;

    public SettingViewModelFactoryTests()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        // Mirrors the stub above onto TryGetString - an unstubbed Moq answers "missing" for every key.
        _mockLocalizationService.MirrorTryGetString();

        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());

        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(a => a().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        _deps = new SettingViewModelDependencies(
            SettingWriteStrategies.Selector(
                _mockSettingApplicationService.Object,
                _mockDialogService.Object,
                _mockLocalizationService.Object,
                _mockLogService.Object,
                _mockApplicationModeService.Object),
            _mockLogService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockRegeditLauncher.Object,
            _mockApplicationModeService.Object);

        _sut = new SettingViewModelFactory(
            _deps,
            _mockLogService.Object,
            _mockLocalizationService.Object,
            _mockUserPreferencesService.Object,
            _mockNewBadgeService.Object,
            _mockEnricher.Object);
    }

    [Fact]
    public async Task CreateAsync_ReturnsNonNullViewModel()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { IsEnabled = true, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null, null);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_SetsSettingId()
    {
        var setting = CreateToggleSetting("MySetting");
        var state = new SettingStateResult { Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null, null);

        result.SettingId.Should().Be("MySetting");
    }

    [Fact]
    public async Task CreateAsync_SetsNameAndDescription()
    {
        var setting = CreateToggleSetting("TestSetting", "Test Name", "Test Description");
        var state = new SettingStateResult { Success = true };
        // The factory localizes Name/Description via the canonical Setting_{id}_* keys.
        _mockLocalizationService.Setup(l => l.GetString("Setting_TestSetting_Name")).Returns("Localized Name");
        _mockLocalizationService.Setup(l => l.GetString("Setting_TestSetting_Description")).Returns("Localized Description");

        var result = await _sut.CreateAsync(setting, state, null, null, null, null);

        result.Name.Should().Be("Localized Name");
        result.Description.Should().Be("Localized Description");
    }

    [Fact]
    public async Task CreateAsync_SetsGroupName()
    {
        var setting = CreateToggleSetting("TestSetting", groupName: "Privacy Settings");
        var state = new SettingStateResult { Success = true };
        // The factory localizes GroupName via the compacted SettingGroup_ key (spaces/ampersands stripped).
        _mockLocalizationService.Setup(l => l.GetString("SettingGroup_PrivacySettings")).Returns("Localized Group");

        var result = await _sut.CreateAsync(setting, state, null, null, null, null);

        result.GroupName.Should().Be("Localized Group");
    }

    [Fact]
    public async Task CreateAsync_SetsIsSelectedFromCurrentState()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { IsEnabled = true, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null, null);

        result.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenNotEnabled_SetsIsSelectedToFalse()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { IsEnabled = false, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null, null);

        result.IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WhenRequiresAdvancedUnlock_SetsIsLocked()
    {
        var setting = CreateToggleSetting("AdvancedSetting", requiresAdvancedUnlock: true);
        var state = new SettingStateResult { Success = true };

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("AdvancedPowerSettingsUnlocked", false))
            .ReturnsAsync(false);

        var result = await _sut.CreateAsync(setting, state, null, null, null, null);

        result.IsLocked.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenAdvancedUnlocked_SetsIsLockedToFalse()
    {
        var setting = CreateToggleSetting("AdvancedSetting", requiresAdvancedUnlock: true);
        var state = new SettingStateResult { Success = true };

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync("AdvancedPowerSettingsUnlocked", false))
            .ReturnsAsync(true);

        var result = await _sut.CreateAsync(setting, state, null, null, null, null);

        result.IsLocked.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_NumericRangeSetting_SetsMinMaxValues()
    {
        var setting = CreateNumericRangeSetting("NumericSetting", 0, 100, "ms");
        var state = new SettingStateResult { CurrentValue = 50, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null, null);

        result.MinValue.Should().Be(0);
        result.MaxValue.Should().Be(100);
        result.Units.Should().Be("ms");
    }

    [Fact]
    public async Task CreateAsync_NumericRangeSetting_SetsNumericValue()
    {
        var setting = CreateNumericRangeSetting("NumericSetting", 0, 100, "ms");
        var state = new SettingStateResult { CurrentValue = 42, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null, null);

        result.NumericValue.Should().Be(42);
    }

    [Fact]
    public async Task CreateAsync_SelectionSetting_PopulatesComboBoxOptions()
    {
        var setting = CreateSelectionSetting("SelectionSetting");
        var state = new SettingStateResult { CurrentValue = 1, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null, null);

        result.ComboBoxOptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_SelectionSetting_SetsSelectedValue()
    {
        var setting = CreateSelectionSetting("SelectionSetting");
        var state = new SettingStateResult { CurrentValue = 1, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null, null);

        result.SelectedValue.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_SelectionSetting_LeavesDetectOnlyStatesOutOfTheOptionList()
    {
        // A detect-only state is one detection can resolve to but the user cannot pick. Offering it as a
        // dropdown entry would offer a choice that writes nothing.
        var setting = CreateSelectionSettingWithDetectOnlyState("DetectOnlySetting");
        var state = new SettingStateResult { CurrentValue = 0, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null, null);

        result.ComboBoxOptions.Should().HaveCount(2);
        result.ComboBoxOptions.Select(o => o.DisplayText).Should().NotContain("Setting_DetectOnlySetting_Option_1");
    }

    [Fact]
    public async Task CreateAsync_SelectionSetting_SurvivingOptionsKeepTheirOwnStateIndex()
    {
        // THE invariant behind the skip. Option Value == state index is what the drop-down-closed handler
        // applies, what a saved .winhance config persists, and what the review diff compares - so a skipped
        // state must NOT pull the states after it down by one. Here the skipped state sits in the MIDDLE,
        // so a renumbering filter would produce 0,1 and this test would fail.
        var setting = CreateSelectionSettingWithDetectOnlyState("DetectOnlySetting");
        var state = new SettingStateResult { CurrentValue = 0, Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null, null);

        result.ComboBoxOptions.Select(o => (int)o.Value).Should().Equal(0, 2);
    }

    [Fact]
    public async Task CreateAsync_CallsApplyReviewDiff()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { Success = true };

        await _sut.CreateAsync(setting, state, null, null, null, null);

        _mockEnricher.Verify(e => e.ApplyReviewDiff(It.IsAny<SettingItemViewModel>(), state), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_NonSelectionType_SetsSelectedValueFromCurrentState()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { CurrentValue = "SomeValue", Success = true };

        var result = await _sut.CreateAsync(setting, state, null, null, null, null);

        result.SelectedValue.Should().Be("SomeValue");
    }

    [Fact]
    public async Task CreateAsync_PassesParentViewModelToConfig()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { Success = true };
        var parentVm = new Mock<ISettingsFeatureViewModel>().Object;

        var result = await _sut.CreateAsync(setting, state, parentVm, null, null, null);

        result.Should().NotBeNull();
    }

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

        var result = await _sut.CreateAsync(setting, state, null, null, null, null);

        result.OnText.Should().Be("Enabled");
        result.OffText.Should().Be("Disabled");
    }

    // Synthetic catalog Setting fixtures. The factory reads the passed Setting; these hand-built Settings
    // carry exactly the fields CreateAsync reads (Control -> InputType, Display, Availability, Numeric, States).
    private static Setting CreateToggleSetting(
        string id, string name = "Test", string description = "Test Description",
        string groupName = "", bool requiresAdvancedUnlock = false) =>
        new Setting
        {
            Id = id,
            Display = new() { Name = name, Description = description, GroupName = groupName },
            Availability = requiresAdvancedUnlock ? new Availability { RequiresAdvancedUnlock = true } : Availability.Everywhere,
            States = new[]
            {
                new SettingState { Label = "Enabled" },
                new SettingState { Label = "Disabled" },
            },
        };

    private static Setting CreateNumericRangeSetting(string id, int min, int max, string units) =>
        new Setting
        {
            Id = id,
            Display = new() { Name = "Numeric", Description = "Numeric setting", GroupName = "" },
            Numeric = new() { Min = min, Max = max, Units = units },
        };

    private static Setting CreateSelectionSetting(string id) =>
        new Setting
        {
            Id = id,
            Display = new() { Name = "Selection", Description = "Selection setting", GroupName = "" },
            States = new[]
            {
                new SettingState { Label = "Option A" },
                new SettingState { Label = "Option B" },
            },
        };

    // The detect-only state sits at index 1, DELIBERATELY not last, so the skip-not-renumber assertion
    // cannot pass by accident the way a trailing skip would.
    private static Setting CreateSelectionSettingWithDetectOnlyState(string id) =>
        new Setting
        {
            Id = id,
            Display = new() { Name = "Selection", Description = "Selection setting", GroupName = "" },
            States = new[]
            {
                new SettingState { Label = "Option A" },
                new SettingState { Label = "Neutral", IsFallback = true, IsDetectOnly = true },
                new SettingState { Label = "Option C" },
            },
        };
}
