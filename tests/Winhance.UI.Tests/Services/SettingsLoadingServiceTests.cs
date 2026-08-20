using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.Services;

public class SettingsLoadingServiceTests
{
    private readonly Mock<ICatalogSettingStateProvider> _mockSettingStateProvider = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IInitializationService> _mockInitializationService = new();
    private readonly Mock<ICatalogSettingsRegistry> _mockCatalogSettingsRegistry = new();
    private readonly Mock<IWindowsVersionFilterService> _mockWindowsVersionFilterService = new();
    private readonly Mock<IWindowsVersionService> _mockWindowsVersionService = new();
    private readonly Mock<IUserPreferencesService> _mockUserPreferencesService = new();
    private readonly Mock<ISettingViewModelFactory> _mockViewModelFactory = new();
    private readonly Mock<ISettingLocalizationService> _mockSettingLocalizationService = new();
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IApplicationModeService> _mockApplicationModeService = new();

    private readonly SettingsLoadingService _sut;

    public SettingsLoadingServiceTests()
    {
        _mockSettingStateProvider
            .Setup(p => p.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());

        // Filter ON is the default: the service must request the current-OS scope
        // (GetByFeature(feature, includeOtherOsVersions: false)). Filter-OFF facts override per-test.
        _mockWindowsVersionFilterService.Setup(f => f.IsFilterEnabled).Returns(true);

        // A fixed live build for compatibility-message derivation (Windows 11 24H2).
        _mockWindowsVersionService.Setup(v => v.GetWindowsBuildNumber()).Returns(26100);
        _mockWindowsVersionService.Setup(v => v.GetWindowsBuildRevision()).Returns(0);

        _sut = new SettingsLoadingService(
            _mockSettingStateProvider.Object,
            _mockLogService.Object,
            _mockInitializationService.Object,
            _mockCatalogSettingsRegistry.Object,
            _mockWindowsVersionFilterService.Object,
            _mockWindowsVersionService.Object,
            _mockUserPreferencesService.Object,
            _mockViewModelFactory.Object,
            _mockSettingLocalizationService.Object,
            _mockLocalization.Object,
            _mockApplicationModeService.Object);
    }

    [Fact]
    public async Task LoadConfiguredSettingsAsync_ReturnsViewModelsForAllSettings()
    {
        var settings = new List<Setting>
        {
            CreateCatalogSetting("security-workplace-join-messages"),
            CreateCatalogSetting("start-disable-bing-search-results")
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("TestFeature", false))
            .Returns(settings);

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
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
                It.IsAny<SettingStateResult>(),
                It.IsAny<ISettingsFeatureViewModel?>(),
                It.IsAny<string?>(),
                It.IsAny<ComboBoxSetupResult?>(),
                It.IsAny<string?>(),
                It.IsAny<WinBuild>()))
            .ReturnsAsync(mockVm1)
            .ReturnsAsync(mockVm2);

        var result = await _sut.LoadConfiguredSettingsAsync(
            "TestFeature", "Loading...", null);

        result.Should().HaveCount(2);

        // Mode pin: filter ON (the default here) must request the current-OS scope.
        _mockCatalogSettingsRegistry.Verify(r => r.GetByFeature("TestFeature", false), Times.Once);
    }

    [Fact]
    public async Task LoadConfiguredSettingsAsync_SkipsSettingsWithFailedState()
    {
        var settings = new List<Setting>
        {
            CreateCatalogSetting("security-workplace-join-messages"),
            CreateCatalogSetting("BadSetting")
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("TestFeature", false))
            .Returns(settings);

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
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
                It.IsAny<SettingStateResult>(),
                It.IsAny<ISettingsFeatureViewModel?>(),
                It.IsAny<string?>(),
                It.IsAny<ComboBoxSetupResult?>(),
                It.IsAny<string?>(),
                It.IsAny<WinBuild>()))
            .ReturnsAsync(mockVm);

        var result = await _sut.LoadConfiguredSettingsAsync(
            "TestFeature", "Loading...", null);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadConfiguredSettingsAsync_StartsAndCompletesFeatureInitialization()
    {

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("TestFeature", It.IsAny<bool>()))
            .Returns(new List<Setting>());

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
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

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("TestFeature", It.IsAny<bool>()))
            .Throws(new Exception("Registry error"));

        var act = () => _sut.LoadConfiguredSettingsAsync(
            "TestFeature", "Loading...", null);

        await act.Should().ThrowAsync<Exception>().WithMessage("Registry error");
        _mockInitializationService.Verify(i => i.CompleteFeatureInitialization("TestFeature"), Times.Once);
    }

    [Fact]
    public async Task LoadConfiguredSettingsAsync_WithEmptySettings_ReturnsEmptyCollection()
    {

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("EmptyFeature", It.IsAny<bool>()))
            .Returns(new List<Setting>());

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync(It.IsAny<string>(), false))
            .ReturnsAsync(false);

        var result = await _sut.LoadConfiguredSettingsAsync(
            "EmptyFeature", "Loading...", null);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadConfiguredSettingsAsync_WhenFilterDisabled_LoadsTheOtherOsVersionsScope()
    {
        // STRICT filter-OFF pin: ONLY the includeOtherOsVersions: true scope is set up. If the service failed
        // to thread !IsFilterEnabled through to GetByFeature, the false-arg call would return Moq's empty
        // default and the count assert below would fail - the scope threading is load-bearing.
        _mockWindowsVersionFilterService.Setup(f => f.IsFilterEnabled).Returns(false);

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("TestFeature", true))
            .Returns(new List<Setting> { CreateCatalogSetting("security-workplace-join-messages") });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                { "security-workplace-join-messages", new SettingStateResult { Success = true, IsEnabled = true } }
            });

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync(It.IsAny<string>(), false))
            .ReturnsAsync(false);

        var mockVm = CreateMockSettingItemViewModel("security-workplace-join-messages");
        _mockViewModelFactory
            .Setup(f => f.CreateAsync(
                It.IsAny<Setting>(),
                It.IsAny<SettingStateResult>(),
                It.IsAny<ISettingsFeatureViewModel?>(),
                It.IsAny<string?>(),
                It.IsAny<ComboBoxSetupResult?>(),
                It.IsAny<string?>(),
                It.IsAny<WinBuild>()))
            .ReturnsAsync(mockVm);

        var result = await _sut.LoadConfiguredSettingsAsync(
            "TestFeature", "Loading...", null);

        result.Should().HaveCount(1);
        _mockCatalogSettingsRegistry.Verify(r => r.GetByFeature("TestFeature", true), Times.Once);
    }

    [Fact]
    public async Task LoadConfiguredSettingsAsync_DerivesAndLocalizesCompatibilityMessage_ForOsGatedSetting()
    {
        // Filter OFF surfaces other-OS settings; a Windows-10-only setting on the mocked 26100 build derives
        // "Compatibility_Windows10Only" (AvailabilityCompatibility), which the service localizes before the
        // factory call. GetString is mocked key-verbatim (this file's convention), so the factory receives
        // the key itself.
        _mockWindowsVersionFilterService.Setup(f => f.IsFilterEnabled).Returns(false);
        _mockLocalization.Setup(l => l.GetString(It.IsAny<string>())).Returns((string k) => k);
        // Mirrors the stub above onto TryGetString - an unstubbed Moq answers "missing" for every key.
        _mockLocalization.MirrorTryGetString();

        var win10Only = new Setting
        {
            Id = "legacy-win10-setting",
            Display = new() { Name = "Legacy", Description = "Windows 10 only" },
            Availability = new Availability { Builds = new[] { BuildRange.Windows10 } }
        };

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("TestFeature", true))
            .Returns(new List<Setting> { win10Only });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                { "legacy-win10-setting", new SettingStateResult { Success = true, IsEnabled = true } }
            });

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync(It.IsAny<string>(), false))
            .ReturnsAsync(false);

        string? receivedCompatibilityMessage = null;
        var mockVm = CreateMockSettingItemViewModel("legacy-win10-setting");
        _mockViewModelFactory
            .Setup(f => f.CreateAsync(
                It.IsAny<Setting>(),
                It.IsAny<SettingStateResult>(),
                It.IsAny<ISettingsFeatureViewModel?>(),
                It.IsAny<string?>(),
                It.IsAny<ComboBoxSetupResult?>(),
                It.IsAny<string?>(),
                It.IsAny<WinBuild>()))
            .Callback<Setting, SettingStateResult, ISettingsFeatureViewModel?, string?, ComboBoxSetupResult?, string?, WinBuild>(
                (_, _, _, _, _, compatibilityMessage, _) => receivedCompatibilityMessage = compatibilityMessage)
            .ReturnsAsync(mockVm);

        await _sut.LoadConfiguredSettingsAsync(
            "TestFeature", "Loading...", null);

        receivedCompatibilityMessage.Should().Be("Compatibility_Windows10Only");
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_WithNoSettings_ReturnsEmptyDictionary()
    {
        var settings = Enumerable.Empty<SettingItemViewModel>();

        var result = await _sut.RefreshSettingStatesAsync(settings);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_WithSettingsHavingNoParentModule_ReturnsEmptyDictionary()
    {
        // No ParentFeatureViewModel means no owning module to re-source catalog Settings from.
        var mockVm = CreateMockSettingItemViewModel("Setting1");

        var result = await _sut.RefreshSettingStatesAsync(new[] { mockVm });

        result.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task LoadConfiguredSettingsAsync_ResolvesComboBoxForSelectionTypeSettings()
    {
        var selectionSetting = CreateCatalogSetting("explorer-customization-measurement-system");

        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("TestFeature", false))
            .Returns(new List<Setting> { selectionSetting });

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
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
                It.IsAny<SettingStateResult>(),
                It.IsAny<ISettingsFeatureViewModel?>(),
                It.IsAny<string?>(),
                It.IsAny<ComboBoxSetupResult?>(),
                It.IsAny<string?>(),
                It.IsAny<WinBuild>()))
            .ReturnsAsync(mockVm);

        await _sut.LoadConfiguredSettingsAsync(
            "TestFeature", "Loading...", null);

        // A Selection's CurrentValue comes from GetStatesAsync (its ResolveRawValuesToIndex) plus the catalog
        // detection overlay, so asserting the detection path ran is what pins combo-box resolution.
        _mockSettingStateProvider.Verify(
            d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()), Times.Once);
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_SelectionSettings_ResolvesComboBoxValues()
    {
        var selectionSetting = CreateCatalogSetting("SelectSetting");

        var parent = new Mock<ISettingsFeatureViewModel>();
        parent.Setup(p => p.ModuleId).Returns("TestModule");
        // Exact-arg setup doubles as the refresh-path filter-ON pin: a true-arg call would return Moq's
        // empty default and the lookup below would fail.
        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("TestModule", false))
            .Returns(new List<Setting> { selectionSetting });

        var vm = CreateMockSettingItemViewModel("SelectSetting", parent.Object);
        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                { "SelectSetting", new SettingStateResult { Success = true, CurrentValue = 1 } }
            });

        var result = await _sut.RefreshSettingStatesAsync(new[] { vm });

        // CurrentValue comes straight from GetStatesAsync (1 here), with the catalog overlay (a no-op in this
        // test) refining paired settings; no separate resolver pass may overwrite it.
        result["SelectSetting"].CurrentValue.Should().Be(1);
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_WithMultipleSettings_ReturnsStatesForAll()
    {
        var toggleSetting = CreateCatalogSetting("Toggle1");
        var selectionSetting = CreateCatalogSetting("Select1");
        var numericSetting = CreateCatalogSetting("Numeric1");

        var parent = new Mock<ISettingsFeatureViewModel>();
        parent.Setup(p => p.ModuleId).Returns("TestModule");
        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("TestModule", false))
            .Returns(new List<Setting> { toggleSetting, selectionSetting, numericSetting });

        var vms = new[]
        {
            CreateMockSettingItemViewModel("Toggle1", parent.Object),
            CreateMockSettingItemViewModel("Select1", parent.Object),
            CreateMockSettingItemViewModel("Numeric1", parent.Object)
        };

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
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
        var setting1 = CreateCatalogSetting("S1");
        var setting2 = CreateCatalogSetting("S2");
        var setting3 = CreateCatalogSetting("S3");

        var parent = new Mock<ISettingsFeatureViewModel>();
        parent.Setup(p => p.ModuleId).Returns("TestModule");
        _mockCatalogSettingsRegistry
            .Setup(r => r.GetByFeature("TestModule", false))
            .Returns(new List<Setting> { setting1, setting2, setting3 });

        var vms = new[]
        {
            CreateMockSettingItemViewModel("S1", parent.Object),
            CreateMockSettingItemViewModel("S2", parent.Object),
            CreateMockSettingItemViewModel("S3", parent.Object)
        };

        _mockSettingStateProvider
            .Setup(d => d.GetStatesAsync(It.IsAny<IReadOnlyList<Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                { "S1", new SettingStateResult { Success = true } },
                { "S2", new SettingStateResult { Success = true } },
                { "S3", new SettingStateResult { Success = true } }
            });

        await _sut.RefreshSettingStatesAsync(vms);

        _mockSettingStateProvider.Verify(
            d => d.GetStatesAsync(It.Is<IReadOnlyList<Setting>>(l => l.Count == 3)),
            Times.Once);
    }

    private static Setting CreateCatalogSetting(string id)
    {
        return new Setting { Id = id, Display = new() { Name = id, Description = "Test" } };
    }

    private static SettingItemViewModel CreateMockSettingItemViewModel(string settingId, ISettingsFeatureViewModel? parent = null)
    {
        var config = new SettingItemViewModelConfig
        {
            Setting = new Setting { Id = settingId, Display = new() { Name = settingId, Description = "Test" } },
            ParentFeatureViewModel = parent,
            SettingId = settingId,
            Name = settingId,
            Description = "Test",
            GroupName = string.Empty,
            Icon = string.Empty,
            IconPack = "Material",
            InputType = InputType.Toggle,
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
        // Mirrors the stub above onto TryGetString - an unstubbed Moq answers "missing" for every key.
        mockLocalization.MirrorTryGetString();
        var mockUserPrefs = new Mock<IUserPreferencesService>();
        var mockRegeditLauncher = new Mock<IRegeditLauncher>();

        return new SettingItemViewModel(
            config,
            SettingWriteStrategies.Selector(
                mockSettingApp.Object, mockDialog.Object, mockLocalization.Object, mockLog.Object),
            mockLog.Object,
            mockDispatcher.Object,
            mockDialog.Object,
            mockLocalization.Object,
            mockUserPrefs.Object,
            mockRegeditLauncher.Object);
    }
}
