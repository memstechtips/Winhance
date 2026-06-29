using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Catalog.Migration;
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

    // ── CreateAsync basics ──

    [Fact]
    public async Task CreateAsync_ReturnsNonNullViewModel()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { IsEnabled = true, Success = true };

        var result = await _sut.CreateAsync(PairFor(setting) ?? SyntheticAlwaysNumericSetting(setting), setting.InputType, state, null, null, null, null, null);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_SetsSettingId()
    {
        var setting = CreateToggleSetting("MySetting");
        var state = new SettingStateResult { Success = true };

        var result = await _sut.CreateAsync(PairFor(setting) ?? SyntheticAlwaysNumericSetting(setting), setting.InputType, state, null, null, null, null, null);

        result.SettingId.Should().Be("MySetting");
    }

    [Fact]
    public async Task CreateAsync_SetsNameAndDescription()
    {
        var setting = CreateToggleSetting("TestSetting", "Test Name", "Test Description");
        var state = new SettingStateResult { Success = true };

        var result = await _sut.CreateAsync(PairFor(setting) ?? SyntheticAlwaysNumericSetting(setting), setting.InputType, state, null, null, null, null, null);

        result.Name.Should().Be("Test Name");
        result.Description.Should().Be("Test Description");
    }

    [Fact]
    public async Task CreateAsync_SetsGroupName()
    {
        var setting = CreateToggleSetting("TestSetting") with { GroupName = "Privacy Settings" };
        var state = new SettingStateResult { Success = true };

        var result = await _sut.CreateAsync(PairFor(setting) ?? SyntheticAlwaysNumericSetting(setting), setting.InputType, state, null, null, null, null, null);

        result.GroupName.Should().Be("Privacy Settings");
    }

    [Fact]
    public async Task CreateAsync_SetsIsSelectedFromCurrentState()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { IsEnabled = true, Success = true };

        var result = await _sut.CreateAsync(PairFor(setting) ?? SyntheticAlwaysNumericSetting(setting), setting.InputType, state, null, null, null, null, null);

        result.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_WhenNotEnabled_SetsIsSelectedToFalse()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { IsEnabled = false, Success = true };

        var result = await _sut.CreateAsync(PairFor(setting) ?? SyntheticAlwaysNumericSetting(setting), setting.InputType, state, null, null, null, null, null);

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

        var result = await _sut.CreateAsync(PairFor(setting) ?? SyntheticAlwaysNumericSetting(setting), setting.InputType, state, null, null, null, null, null);

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

        var result = await _sut.CreateAsync(PairFor(setting) ?? SyntheticAlwaysNumericSetting(setting), setting.InputType, state, null, null, null, null, null);

        result.IsLocked.Should().BeFalse();
    }

    // ── Numeric range settings ──

    [Fact]
    public async Task CreateAsync_NumericRangeSetting_SetsMinMaxValues()
    {
        var setting = CreateNumericRangeSetting("NumericSetting", 0, 100, "ms");
        var state = new SettingStateResult { CurrentValue = 50, Success = true };

        var result = await _sut.CreateAsync(PairFor(setting) ?? SyntheticAlwaysNumericSetting(setting), setting.InputType, state, null, null, null, null, null);

        result.MinValue.Should().Be(0);
        result.MaxValue.Should().Be(100);
        result.Units.Should().Be("ms");
    }

    [Fact]
    public async Task CreateAsync_NumericRangeSetting_SetsNumericValue()
    {
        var setting = CreateNumericRangeSetting("NumericSetting", 0, 100, "ms");
        var state = new SettingStateResult { CurrentValue = 42, Success = true };

        var result = await _sut.CreateAsync(PairFor(setting) ?? SyntheticAlwaysNumericSetting(setting), setting.InputType, state, null, null, null, null, null);

        result.NumericValue.Should().Be(42);
    }

    // ── Selection settings ──

    [Fact]
    public async Task CreateAsync_SelectionSetting_PopulatesComboBoxOptions()
    {
        var setting = CreateSelectionSetting("SelectionSetting");
        var state = new SettingStateResult { CurrentValue = 1, Success = true };

        var result = await _sut.CreateAsync(PairFor(setting) ?? SyntheticAlwaysNumericSetting(setting), setting.InputType, state, null, null, null, null, null);

        result.ComboBoxOptions.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_SelectionSetting_SetsSelectedValue()
    {
        var setting = CreateSelectionSetting("SelectionSetting");
        var state = new SettingStateResult { CurrentValue = 1, Success = true };

        var result = await _sut.CreateAsync(PairFor(setting) ?? SyntheticAlwaysNumericSetting(setting), setting.InputType, state, null, null, null, null, null);

        result.SelectedValue.Should().Be(1);
    }

    // ── Review diff ──

    [Fact]
    public async Task CreateAsync_CallsApplyReviewDiff()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { Success = true };

        await _sut.CreateAsync(PairFor(setting) ?? SyntheticAlwaysNumericSetting(setting), setting.InputType, state, null, null, null, null, null);

        _mockEnricher.Verify(e => e.ApplyReviewDiff(It.IsAny<SettingItemViewModel>(), state), Times.Once);
    }

    // ── Non-selection types call InitializeCompatibilityBanner ──

    [Fact]
    public async Task CreateAsync_NonSelectionType_SetsSelectedValueFromCurrentState()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { CurrentValue = "SomeValue", Success = true };

        var result = await _sut.CreateAsync(PairFor(setting) ?? SyntheticAlwaysNumericSetting(setting), setting.InputType, state, null, null, null, null, null);

        result.SelectedValue.Should().Be("SomeValue");
    }

    // ── Parent VM ──

    [Fact]
    public async Task CreateAsync_PassesParentViewModelToConfig()
    {
        var setting = CreateToggleSetting("TestSetting");
        var state = new SettingStateResult { Success = true };
        var parentVm = new Mock<ISettingsFeatureViewModel>().Object;

        var result = await _sut.CreateAsync(PairFor(setting) ?? SyntheticAlwaysNumericSetting(setting), setting.InputType, state, parentVm, null, null, null, null);

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

        var result = await _sut.CreateAsync(PairFor(setting) ?? SyntheticAlwaysNumericSetting(setting), setting.InputType, state, null, null, null, null, null);

        result.OnText.Should().Be("Enabled");
        result.OffText.Should().Be("Disabled");
    }

    // ── Helper methods ──

    // Pair the new-model Setting exactly as production does: a catalog-authored id resolves to its catalog
    // Setting; a synthetic test def simulates the converter peer the factory would pair. A registry-only
    // NumericRange has no production peer (zero exist) -> null (caller substitutes a synthetic Numeric Setting).
    private static Setting? PairFor(SettingDefinition def)
    {
        var catalogPeer = SettingCatalog.All.FirstOrDefault(s => s.Id == def.Id);
        if (catalogPeer is not null) return catalogPeer;
        if (def.InputType == InputType.NumericRange && (def.PowerCfgSettings?.Count ?? 0) == 0) return null;
        if (def.PowerCfgSettings is { Count: > 0 }) return SettingDefinitionConverter.ConvertPowerCfg(def);
        if (def.InputType == InputType.Selection) return SettingDefinitionConverter.ConvertSelection(def);
        return SettingDefinitionConverter.ConvertToggle(def);
    }

    // Registry single-spinner NumericRange has no production peer (PairFor -> null). Synthesize a Setting whose
    // Always-context Numeric carries the def's range (+ primary registry Recommended/Default when present).
    private static Setting SyntheticAlwaysNumericSetting(SettingDefinition def)
    {
        var reg = def.RegistrySettings is { Count: > 0 } rs ? rs[0] : null;
        var recommended = reg?.RecommendedValue is { } rv
            ? new[] { new ContextValue(PowerContext.Always, Convert.ToInt32(rv)) }
            : System.Array.Empty<ContextValue>();
        var windowsDefault = reg?.DefaultValue is { } dv
            ? new[] { new ContextValue(PowerContext.Always, Convert.ToInt32(dv)) }
            : System.Array.Empty<ContextValue>();
        return new Setting
        {
            Id = def.Id,
            Display = new() { Name = def.Name, Description = def.Description },
            Numeric = new()
            {
                Min = def.NumericRange?.MinValue ?? 0,
                Max = def.NumericRange?.MaxValue ?? 100,
                Units = def.NumericRange?.Units,
                Recommended = recommended,
                WindowsDefault = windowsDefault,
            },
        };
    }

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
            NumericRange = new NumericRangeMetadata
            {
                MinValue = min,
                MaxValue = max,
                Units = units
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
            GroupName = string.Empty,
            ComboBox = new ComboBoxMetadata
            {
                Options = new[]
                {
                    new ComboBoxOption { DisplayName = "Option A" },
                    new ComboBoxOption { DisplayName = "Option B" }
                }
            }
        };
    }
}
