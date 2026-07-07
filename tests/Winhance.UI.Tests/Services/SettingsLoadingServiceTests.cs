using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
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

public class SettingsLoadingServiceTests
{
    private readonly Mock<ICatalogSettingStateProvider> _mockSettingStateProvider = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IInitializationService> _mockInitializationService = new();
    private readonly Mock<ISettingPreparationPipeline> _mockPreparationPipeline = new();
    private readonly Mock<IUserPreferencesService> _mockUserPreferencesService = new();
    private readonly Mock<ISettingViewModelFactory> _mockViewModelFactory = new();
    private readonly Mock<ISettingLocalizationService> _mockSettingLocalizationService = new();
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IApplicationModeService> _mockApplicationModeService = new();

    private readonly SettingsLoadingService _sut;

    public SettingsLoadingServiceTests()
    {
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());

        _sut = new SettingsLoadingService(
            _mockSettingStateProvider.Object,
            _mockLogService.Object,
            _mockInitializationService.Object,
            _mockPreparationPipeline.Object,
            _mockUserPreferencesService.Object,
            _mockViewModelFactory.Object,
            _mockSettingLocalizationService.Object,
            _mockLocalization.Object,
            _mockApplicationModeService.Object);
    }

    // ── LoadConfiguredSettingsAsync ──

    [Fact]
    public async Task LoadConfiguredSettingsAsync_ReturnsViewModelsForAllSettings()
    {
        var settings = new List<SettingDefinition>
        {
            new() { Id = "security-workplace-join-messages", Name = "Setting 1", Description = "Desc 1", InputType = InputType.Toggle },
            new() { Id = "start-disable-bing-search-results", Name = "Setting 2", Description = "Desc 2", InputType = InputType.Toggle }
        };

        _mockPreparationPipeline
            .Setup(p => p.PrepareSettings("TestFeature"))
            .Returns(settings);

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                { "security-workplace-join-messages", new SettingStateResult { Success = true, IsEnabled = true } },
                { "start-disable-bing-search-results", new SettingStateResult { Success = true, IsEnabled = false } }
            });

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync(It.IsAny<string>(), false))
            .ReturnsAsync(false);

        var mockVm1 = CreateMockSettingItemViewModel("security-workplace-join-messages");
        var mockVm2 = CreateMockSettingItemViewModel("start-disable-bing-search-results");

        _mockViewModelFactory
            .SetupSequence(f => f.CreateAsync(
                It.IsAny<Setting>(),
                It.IsAny<InputType>(),
                It.IsAny<SettingStateResult>(),
                It.IsAny<ISettingsFeatureViewModel?>(),
                It.IsAny<string?>(),
                It.IsAny<ComboBoxSetupResult?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(mockVm1)
            .ReturnsAsync(mockVm2);

        var result = await _sut.LoadConfiguredSettingsAsync(
            "TestFeature", "Loading...", null);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadConfiguredSettingsAsync_SkipsSettingsWithFailedState()
    {
        var settings = new List<SettingDefinition>
        {
            new() { Id = "security-workplace-join-messages", Name = "Good", Description = "Good desc", InputType = InputType.Toggle },
            new() { Id = "BadSetting", Name = "Bad", Description = "Bad desc", InputType = InputType.Toggle }
        };

        _mockPreparationPipeline
            .Setup(p => p.PrepareSettings("TestFeature"))
            .Returns(settings);

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                { "security-workplace-join-messages", new SettingStateResult { Success = true, IsEnabled = true } },
                { "BadSetting", new SettingStateResult { Success = false, ErrorMessage = "Not found" } }
            });

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync(It.IsAny<string>(), false))
            .ReturnsAsync(false);

        var mockVm = CreateMockSettingItemViewModel("security-workplace-join-messages");
        _mockViewModelFactory
            .Setup(f => f.CreateAsync(
                It.IsAny<Setting>(),
                It.IsAny<InputType>(),
                It.IsAny<SettingStateResult>(),
                It.IsAny<ISettingsFeatureViewModel?>(),
                It.IsAny<string?>(),
                It.IsAny<ComboBoxSetupResult?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(mockVm);

        var result = await _sut.LoadConfiguredSettingsAsync(
            "TestFeature", "Loading...", null);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadConfiguredSettingsAsync_StartsAndCompletesFeatureInitialization()
    {

        _mockPreparationPipeline
            .Setup(p => p.PrepareSettings("TestFeature"))
            .Returns(new List<SettingDefinition>());

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync(It.IsAny<string>(), false))
            .ReturnsAsync(false);

        await _sut.LoadConfiguredSettingsAsync(
            "TestFeature", "Loading...", null);

        _mockInitializationService.Verify(i => i.StartFeatureInitialization("TestFeature"), Times.Once);
        _mockInitializationService.Verify(i => i.CompleteFeatureInitialization("TestFeature"), Times.Once);
    }

    [Fact]
    public async Task LoadConfiguredSettingsAsync_WhenExceptionThrown_CompletesInitializationAndRethrows()
    {

        _mockPreparationPipeline
            .Setup(p => p.PrepareSettings("TestFeature"))
            .Throws(new Exception("Pipeline error"));

        var act = () => _sut.LoadConfiguredSettingsAsync(
            "TestFeature", "Loading...", null);

        await act.Should().ThrowAsync<Exception>().WithMessage("Pipeline error");
        _mockInitializationService.Verify(i => i.CompleteFeatureInitialization("TestFeature"), Times.Once);
    }

    [Fact]
    public async Task LoadConfiguredSettingsAsync_WithEmptySettings_ReturnsEmptyCollection()
    {

        _mockPreparationPipeline
            .Setup(p => p.PrepareSettings("EmptyFeature"))
            .Returns(new List<SettingDefinition>());

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync(It.IsAny<string>(), false))
            .ReturnsAsync(false);

        var result = await _sut.LoadConfiguredSettingsAsync(
            "EmptyFeature", "Loading...", null);

        result.Should().BeEmpty();
    }

    // ── RefreshSettingStatesAsync ──

    [Fact]
    public async Task RefreshSettingStatesAsync_WithNoSettings_ReturnsEmptyDictionary()
    {
        var settings = Enumerable.Empty<SettingItemViewModel>();

        var result = await _sut.RefreshSettingStatesAsync(settings);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_WithSettingsHavingNullDefinitions_ReturnsEmptyDictionary()
    {
        var mockVm = CreateMockSettingItemViewModel("Setting1");
        // SettingDefinition is null by default in the mock

        var result = await _sut.RefreshSettingStatesAsync(new[] { mockVm });

        result.Should().BeNullOrEmpty();
    }

    // ── Selection-type combo resolution ──

    [Fact]
    public async Task LoadConfiguredSettingsAsync_ResolvesComboBoxForSelectionTypeSettings()
    {
        var selectionSetting = new SettingDefinition
        {
            Id = "explorer-customization-measurement-system",
            Name = "Select",
            Description = "Select desc",
            InputType = InputType.Selection
        };

        _mockPreparationPipeline
            .Setup(p => p.PrepareSettings("TestFeature"))
            .Returns(new List<SettingDefinition> { selectionSetting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                { "explorer-customization-measurement-system", new SettingStateResult
                    { Success = true, CurrentValue = 1 } }
            });

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync(It.IsAny<string>(), false))
            .ReturnsAsync(false);

        var mockVm = CreateMockSettingItemViewModel("explorer-customization-measurement-system");
        _mockViewModelFactory
            .Setup(f => f.CreateAsync(
                It.IsAny<Setting>(),
                It.IsAny<InputType>(),
                It.IsAny<SettingStateResult>(),
                It.IsAny<ISettingsFeatureViewModel?>(),
                It.IsAny<string?>(),
                It.IsAny<ComboBoxSetupResult?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(mockVm);

        await _sut.LoadConfiguredSettingsAsync(
            "TestFeature", "Loading...", null);

        // G1a: combo-box resolution is no longer a separate IComboBoxResolver pass - a Selection's CurrentValue comes
        // from GetStatesAsync (its ResolveRawValuesToIndex) plus the catalog detection overlay. Assert the
        // detection path ran for the selection (pairing-independent; the VM list itself depends on catalog pairing).
        _mockSettingStateProvider.Verify(
            d => d.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()), Times.Once);
    }

    // ── RefreshSettingStatesAsync: combo box resolution + batch verification ──

    [Fact]
    public async Task RefreshSettingStatesAsync_SelectionSettings_ResolvesComboBoxValues()
    {
        var selectionDef = new SettingDefinition
        {
            Id = "SelectSetting",
            Name = "Select Setting",
            Description = "Selection test",
            InputType = InputType.Selection
        };

        var parent = new Mock<ISettingsFeatureViewModel>();
        parent.Setup(p => p.ModuleId).Returns("TestModule");
        _mockPreparationPipeline
            .Setup(p => p.PrepareSettings("TestModule"))
            .Returns(new List<SettingDefinition> { selectionDef });

        var vm = CreateMockSettingItemViewModel("SelectSetting", selectionDef, parent.Object);
        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                { "SelectSetting", new SettingStateResult { Success = true, CurrentValue = 1 } }
            });

        var result = await _sut.RefreshSettingStatesAsync(new[] { vm });

        // G1a: the IComboBoxResolver re-resolution was retired; CurrentValue now comes straight from
        // GetStatesAsync (1 here), with the catalog overlay (a no-op in this test) refining paired settings.
        // There is no separate resolver pass to overwrite it to 2.
        result["SelectSetting"].CurrentValue.Should().Be(1);
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_WithMixedInputTypes_ReturnsStatesForAll()
    {
        var toggleDef = new SettingDefinition
        {
            Id = "Toggle1", Name = "Toggle", Description = "Toggle desc", InputType = InputType.Toggle
        };
        var selectionDef = new SettingDefinition
        {
            Id = "Select1", Name = "Select", Description = "Select desc", InputType = InputType.Selection
        };
        var numericDef = new SettingDefinition
        {
            Id = "Numeric1", Name = "Numeric", Description = "Numeric desc", InputType = InputType.NumericRange
        };

        var parent = new Mock<ISettingsFeatureViewModel>();
        parent.Setup(p => p.ModuleId).Returns("TestModule");
        _mockPreparationPipeline
            .Setup(p => p.PrepareSettings("TestModule"))
            .Returns(new List<SettingDefinition> { toggleDef, selectionDef, numericDef });

        var vms = new[]
        {
            CreateMockSettingItemViewModel("Toggle1", toggleDef, parent.Object),
            CreateMockSettingItemViewModel("Select1", selectionDef, parent.Object),
            CreateMockSettingItemViewModel("Numeric1", numericDef, parent.Object)
        };

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                { "Toggle1", new SettingStateResult { Success = true, IsEnabled = true } },
                { "Select1", new SettingStateResult { Success = true, CurrentValue = 1 } },
                { "Numeric1", new SettingStateResult { Success = true, CurrentValue = 300 } }
            });

        var result = await _sut.RefreshSettingStatesAsync(vms);

        result.Should().HaveCount(3);
        result["Toggle1"].IsEnabled.Should().BeTrue();
        result["Select1"].CurrentValue.Should().Be(1);
        result["Numeric1"].CurrentValue.Should().Be(300);
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_CallsStateProviderExactlyOnce()
    {
        var def1 = new SettingDefinition
        {
            Id = "S1", Name = "S1", Description = "Desc", InputType = InputType.Toggle
        };
        var def2 = new SettingDefinition
        {
            Id = "S2", Name = "S2", Description = "Desc", InputType = InputType.Toggle
        };
        var def3 = new SettingDefinition
        {
            Id = "S3", Name = "S3", Description = "Desc", InputType = InputType.NumericRange
        };

        var parent = new Mock<ISettingsFeatureViewModel>();
        parent.Setup(p => p.ModuleId).Returns("TestModule");
        _mockPreparationPipeline
            .Setup(p => p.PrepareSettings("TestModule"))
            .Returns(new List<SettingDefinition> { def1, def2, def3 });

        var vms = new[]
        {
            CreateMockSettingItemViewModel("S1", def1, parent.Object),
            CreateMockSettingItemViewModel("S2", def2, parent.Object),
            CreateMockSettingItemViewModel("S3", def3, parent.Object)
        };

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                { "S1", new SettingStateResult { Success = true } },
                { "S2", new SettingStateResult { Success = true } },
                { "S3", new SettingStateResult { Success = true } }
            });

        await _sut.RefreshSettingStatesAsync(vms);

        // Batch call: exactly one call with all 3 definitions, not 3 individual calls
        _mockSettingStateProvider.Verify(
            d => d.GetStatesAsync(It.Is<IReadOnlyList<SettingDefinition>>(l => l.Count == 3)),
            Times.Once);
    }

    // ── Helper ──

    private static SettingItemViewModel CreateMockSettingItemViewModel(string settingId, ISettingsFeatureViewModel? parent = null)
    {
        return CreateMockSettingItemViewModel(settingId, new SettingDefinition
        {
            Id = settingId,
            Name = settingId,
            Description = "Test",
            InputType = InputType.Toggle
        }, parent);
    }

    private static SettingItemViewModel CreateMockSettingItemViewModel(string settingId, SettingDefinition settingDefinition, ISettingsFeatureViewModel? parent = null)
    {
        var config = new SettingItemViewModelConfig
        {
            Setting = new Setting { Id = settingId, Display = new() { Name = settingDefinition.Name, Description = settingDefinition.Description } },
            ParentFeatureViewModel = parent,
            SettingId = settingId,
            Name = settingDefinition.Name,
            Description = settingDefinition.Description,
            GroupName = string.Empty,
            Icon = string.Empty,
            IconPack = "Material",
            InputType = settingDefinition.InputType,
            IsSelected = false,
            OnText = "On",
            OffText = "Off",
            ActionButtonText = "Apply"
        };

        var mockSettingApp = new Mock<ISettingApplicationService>();
        var mockLog = new Mock<ILogService>();
        var mockDispatcher = new Mock<IDispatcherService>();
        mockDispatcher.Setup(d => d.RunOnUIThread(It.IsAny<Action>())).Callback<Action>(a => a());
        var mockDialog = new Mock<IDialogService>();
        var mockLocalization = new Mock<ILocalizationService>();
        mockLocalization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string k) => k);
        var mockEventBus = new Mock<IEventBus>();
        var mockUserPrefs = new Mock<IUserPreferencesService>();
        var mockRegeditLauncher = new Mock<IRegeditLauncher>();

        return new SettingItemViewModel(
            config,
            mockSettingApp.Object,
            mockLog.Object,
            mockDispatcher.Object,
            mockDialog.Object,
            mockLocalization.Object,
            mockEventBus.Object,
            mockUserPrefs.Object,
            mockRegeditLauncher.Object);
    }
}
