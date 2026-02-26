using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Features;
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
    private readonly Mock<ISystemSettingsDiscoveryService> _mockDiscoveryService = new();
    private readonly Mock<IEventBus> _mockEventBus = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IInitializationService> _mockInitializationService = new();
    private readonly Mock<IComboBoxResolver> _mockComboBoxResolver = new();
    private readonly Mock<ISettingPreparationPipeline> _mockPreparationPipeline = new();
    private readonly Mock<IUserPreferencesService> _mockUserPreferencesService = new();
    private readonly Mock<ISettingViewModelFactory> _mockViewModelFactory = new();

    private readonly SettingsLoadingService _sut;

    public SettingsLoadingServiceTests()
    {
        _sut = new SettingsLoadingService(
            _mockDiscoveryService.Object,
            _mockEventBus.Object,
            _mockLogService.Object,
            _mockInitializationService.Object,
            _mockComboBoxResolver.Object,
            _mockPreparationPipeline.Object,
            _mockUserPreferencesService.Object,
            _mockViewModelFactory.Object);
    }

    // ── LoadConfiguredSettingsAsync ──

    [Fact]
    public async Task LoadConfiguredSettingsAsync_ReturnsViewModelsForAllSettings()
    {
        var domainService = new Mock<IDomainService>().Object;
        var settings = new List<SettingDefinition>
        {
            new() { Id = "Setting1", Name = "Setting 1", Description = "Desc 1", InputType = InputType.Toggle },
            new() { Id = "Setting2", Name = "Setting 2", Description = "Desc 2", InputType = InputType.Toggle }
        };

        _mockPreparationPipeline
            .Setup(p => p.PrepareSettings("TestFeature"))
            .Returns(settings);

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                { "Setting1", new SettingStateResult { Success = true, IsEnabled = true } },
                { "Setting2", new SettingStateResult { Success = true, IsEnabled = false } }
            });

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync(It.IsAny<string>(), false))
            .ReturnsAsync(false);

        var mockVm1 = CreateMockSettingItemViewModel("Setting1");
        var mockVm2 = CreateMockSettingItemViewModel("Setting2");

        _mockViewModelFactory
            .SetupSequence(f => f.CreateAsync(
                It.IsAny<SettingDefinition>(),
                It.IsAny<SettingStateResult>(),
                It.IsAny<ISettingsFeatureViewModel?>()))
            .ReturnsAsync(mockVm1)
            .ReturnsAsync(mockVm2);

        var result = await _sut.LoadConfiguredSettingsAsync(
            domainService, "TestFeature", "Loading...", null);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadConfiguredSettingsAsync_SkipsSettingsWithFailedState()
    {
        var domainService = new Mock<IDomainService>().Object;
        var settings = new List<SettingDefinition>
        {
            new() { Id = "GoodSetting", Name = "Good", Description = "Good desc", InputType = InputType.Toggle },
            new() { Id = "BadSetting", Name = "Bad", Description = "Bad desc", InputType = InputType.Toggle }
        };

        _mockPreparationPipeline
            .Setup(p => p.PrepareSettings("TestFeature"))
            .Returns(settings);

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                { "GoodSetting", new SettingStateResult { Success = true, IsEnabled = true } },
                { "BadSetting", new SettingStateResult { Success = false, ErrorMessage = "Not found" } }
            });

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync(It.IsAny<string>(), false))
            .ReturnsAsync(false);

        var mockVm = CreateMockSettingItemViewModel("GoodSetting");
        _mockViewModelFactory
            .Setup(f => f.CreateAsync(
                It.Is<SettingDefinition>(s => s.Id == "GoodSetting"),
                It.IsAny<SettingStateResult>(),
                It.IsAny<ISettingsFeatureViewModel?>()))
            .ReturnsAsync(mockVm);

        var result = await _sut.LoadConfiguredSettingsAsync(
            domainService, "TestFeature", "Loading...", null);

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadConfiguredSettingsAsync_StartsAndCompletesFeatureInitialization()
    {
        var domainService = new Mock<IDomainService>().Object;

        _mockPreparationPipeline
            .Setup(p => p.PrepareSettings("TestFeature"))
            .Returns(new List<SettingDefinition>());

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync(It.IsAny<string>(), false))
            .ReturnsAsync(false);

        await _sut.LoadConfiguredSettingsAsync(
            domainService, "TestFeature", "Loading...", null);

        _mockInitializationService.Verify(i => i.StartFeatureInitialization("TestFeature"), Times.Once);
        _mockInitializationService.Verify(i => i.CompleteFeatureInitialization("TestFeature"), Times.Once);
    }

    [Fact]
    public async Task LoadConfiguredSettingsAsync_PublishesFeatureComposedEvent()
    {
        var domainService = new Mock<IDomainService>().Object;
        var settings = new List<SettingDefinition>
        {
            new() { Id = "Setting1", Name = "Setting 1", Description = "Desc 1", InputType = InputType.Toggle }
        };

        _mockPreparationPipeline
            .Setup(p => p.PrepareSettings("TestFeature"))
            .Returns(settings);

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                { "Setting1", new SettingStateResult { Success = true } }
            });

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync(It.IsAny<string>(), false))
            .ReturnsAsync(false);

        var mockVm = CreateMockSettingItemViewModel("Setting1");
        _mockViewModelFactory
            .Setup(f => f.CreateAsync(
                It.IsAny<SettingDefinition>(),
                It.IsAny<SettingStateResult>(),
                It.IsAny<ISettingsFeatureViewModel?>()))
            .ReturnsAsync(mockVm);

        await _sut.LoadConfiguredSettingsAsync(
            domainService, "TestFeature", "Loading...", null);

        _mockEventBus.Verify(e => e.Publish(It.IsAny<FeatureComposedEvent>()), Times.Once);
    }

    [Fact]
    public async Task LoadConfiguredSettingsAsync_WhenExceptionThrown_CompletesInitializationAndRethrows()
    {
        var domainService = new Mock<IDomainService>().Object;

        _mockPreparationPipeline
            .Setup(p => p.PrepareSettings("TestFeature"))
            .Throws(new Exception("Pipeline error"));

        var act = () => _sut.LoadConfiguredSettingsAsync(
            domainService, "TestFeature", "Loading...", null);

        await act.Should().ThrowAsync<Exception>().WithMessage("Pipeline error");
        _mockInitializationService.Verify(i => i.CompleteFeatureInitialization("TestFeature"), Times.Once);
    }

    [Fact]
    public async Task LoadConfiguredSettingsAsync_WithEmptySettings_ReturnsEmptyCollection()
    {
        var domainService = new Mock<IDomainService>().Object;

        _mockPreparationPipeline
            .Setup(p => p.PrepareSettings("EmptyFeature"))
            .Returns(new List<SettingDefinition>());

        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync(It.IsAny<string>(), false))
            .ReturnsAsync(false);

        var result = await _sut.LoadConfiguredSettingsAsync(
            domainService, "EmptyFeature", "Loading...", null);

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
        var domainService = new Mock<IDomainService>().Object;
        var selectionSetting = new SettingDefinition
        {
            Id = "SelectSetting",
            Name = "Select",
            Description = "Select desc",
            InputType = InputType.Selection
        };

        _mockPreparationPipeline
            .Setup(p => p.PrepareSettings("TestFeature"))
            .Returns(new List<SettingDefinition> { selectionSetting });

        var rawValues = new Dictionary<string, object?> { { "PowerCfgValue", 1 } };
        _mockDiscoveryService
            .Setup(d => d.GetSettingStatesAsync(It.IsAny<IReadOnlyList<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                { "SelectSetting", new SettingStateResult
                    { Success = true, CurrentValue = 1, RawValues = rawValues } }
            });

        _mockComboBoxResolver
            .Setup(r => r.ResolveCurrentValueAsync(
                selectionSetting,
                It.IsAny<Dictionary<string, object?>>()))
            .ReturnsAsync(2);

        _mockUserPreferencesService
            .Setup(u => u.GetPreferenceAsync(It.IsAny<string>(), false))
            .ReturnsAsync(false);

        var mockVm = CreateMockSettingItemViewModel("SelectSetting");
        _mockViewModelFactory
            .Setup(f => f.CreateAsync(
                It.IsAny<SettingDefinition>(),
                It.IsAny<SettingStateResult>(),
                It.IsAny<ISettingsFeatureViewModel?>()))
            .ReturnsAsync(mockVm);

        await _sut.LoadConfiguredSettingsAsync(
            domainService, "TestFeature", "Loading...", null);

        _mockComboBoxResolver.Verify(r => r.ResolveCurrentValueAsync(
            selectionSetting, It.IsAny<Dictionary<string, object?>>()), Times.Once);
    }

    // ── Helper ──

    private static SettingItemViewModel CreateMockSettingItemViewModel(string settingId)
    {
        // Create a minimal config for the SettingItemViewModel
        // Since we can't easily mock ObservableObject, we create a real instance
        // with a minimal set of dependencies
        var settingDefinition = new SettingDefinition
        {
            Id = settingId,
            Name = settingId,
            Description = "Test",
            InputType = InputType.Toggle
        };

        var config = new SettingItemViewModelConfig
        {
            SettingDefinition = settingDefinition,
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
