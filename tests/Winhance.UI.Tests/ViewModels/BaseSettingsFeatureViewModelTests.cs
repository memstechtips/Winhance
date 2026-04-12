using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;
using ISettingsLoadingService = Winhance.UI.Features.Common.Interfaces.ISettingsLoadingService;

namespace Winhance.UI.Tests.ViewModels;

/// <summary>
/// Concrete subclass of BaseSettingsFeatureViewModel used for testing.
/// </summary>
public class TestableSettingsFeatureViewModel : BaseSettingsFeatureViewModel
{
    public const string TestModuleId = "TestModule";
    public const string TestDisplayNameKey = "Feature_Test_Name";

    public override string ModuleId => TestModuleId;

    protected override string GetDisplayNameKey() => TestDisplayNameKey;

    public TestableSettingsFeatureViewModel(
        IDomainServiceRouter domainServiceRouter,
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IEventBus eventBus)
        : base(domainServiceRouter, settingsLoadingService, logService, localizationService, dispatcherService, eventBus)
    {
    }
}

public class BaseSettingsFeatureViewModelTests : IDisposable
{
    private readonly Mock<IDomainServiceRouter> _mockDomainServiceRouter;
    private readonly Mock<ISettingsLoadingService> _mockSettingsLoadingService;
    private readonly Mock<ILogService> _mockLogService;
    private readonly Mock<ILocalizationService> _mockLocalizationService;
    private readonly Mock<IDispatcherService> _mockDispatcherService;
    private readonly Mock<IEventBus> _mockEventBus;
    private readonly Mock<IDomainService> _mockDomainService;

    public BaseSettingsFeatureViewModelTests()
    {
        _mockDomainServiceRouter = new Mock<IDomainServiceRouter>();
        _mockSettingsLoadingService = new Mock<ISettingsLoadingService>();
        _mockLogService = new Mock<ILogService>();
        _mockLocalizationService = new Mock<ILocalizationService>();
        _mockDispatcherService = new Mock<IDispatcherService>();
        _mockEventBus = new Mock<IEventBus>();
        _mockDomainService = new Mock<IDomainService>();

        // Default localization: return the key itself
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        // Dispatcher executes actions synchronously for testing
        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(action => action());

        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(asyncAction => asyncAction());

        // Domain service router returns a mock domain service
        _mockDomainServiceRouter
            .Setup(r => r.GetDomainService(It.IsAny<string>()))
            .Returns(_mockDomainService.Object);

        // Event bus subscribe returns a mock subscription token
        _mockEventBus
            .Setup(e => e.Subscribe(It.IsAny<Action<SettingAppliedEvent>>()))
            .Returns(new Mock<ISubscriptionToken>().Object);
        _mockEventBus
            .Setup(e => e.SubscribeAsync(It.IsAny<Func<FilterStateChangedEvent, Task>>()))
            .Returns(new Mock<ISubscriptionToken>().Object);
        _mockEventBus
            .Setup(e => e.Subscribe(It.IsAny<Action<ReviewModeExitedEvent>>()))
            .Returns(new Mock<ISubscriptionToken>().Object);
    }

    public void Dispose()
    {
        // Intentionally empty; individual tests dispose their SUT as needed.
    }

    private TestableSettingsFeatureViewModel CreateViewModel()
    {
        return new TestableSettingsFeatureViewModel(
            _mockDomainServiceRouter.Object,
            _mockSettingsLoadingService.Object,
            _mockLogService.Object,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object,
            _mockEventBus.Object);
    }

    /// <summary>
    /// Creates a mock SettingItemViewModel with the given properties.
    /// Uses the real SettingItemViewModel constructor.
    /// </summary>
    private SettingItemViewModel CreateSettingItem(
        string settingId,
        string name,
        string description = "Description",
        string groupName = "Group1",
        InputType inputType = InputType.Toggle,
        bool isSelected = false,
        int numericValue = 0,
        object? selectedValue = null,
        NumericRangeMetadata? numericRange = null,
        ComboBoxMetadata? comboBox = null,
        IReadOnlyList<PowerCfgSetting>? powerCfgSettings = null)
    {
        var config = new SettingItemViewModelConfig
        {
            SettingDefinition = new SettingDefinition
            {
                Id = settingId,
                Name = name,
                Description = description,
                InputType = inputType,
                NumericRange = numericRange,
                ComboBox = comboBox,
                PowerCfgSettings = powerCfgSettings,
            },
            SettingId = settingId,
            Name = name,
            Description = description,
            GroupName = groupName,
            InputType = inputType,
            IsSelected = isSelected,
            Icon = "TestIcon",
            IconPack = "Material",
        };

        var mockSettingAppService = new Mock<ISettingApplicationService>();
        var mockDialogService = new Mock<IDialogService>();

        var vm = new SettingItemViewModel(
            config,
            mockSettingAppService.Object,
            _mockLogService.Object,
            _mockDispatcherService.Object,
            mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockEventBus.Object);

        // Set post-construction values for non-Toggle types
        if (inputType == InputType.NumericRange)
            vm.NumericValue = numericValue;
        if (inputType == InputType.Selection && selectedValue != null)
            vm.SelectedValue = selectedValue;

        return vm;
    }

    private ObservableCollection<SettingItemViewModel> CreateSettingsCollection(params (string id, string name, string group)[] items)
    {
        var collection = new ObservableCollection<SettingItemViewModel>();
        foreach (var (id, name, group) in items)
        {
            collection.Add(CreateSettingItem(id, name, groupName: group));
        }
        return collection;
    }

    // ── Constructor Tests ──

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var vm = CreateViewModel();

        // Assert
        vm.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        // Act
        var action = () => CreateViewModel();

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNullDomainServiceRouter_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new TestableSettingsFeatureViewModel(
            null!,
            _mockSettingsLoadingService.Object,
            _mockLogService.Object,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object,
            _mockEventBus.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("domainServiceRouter");
    }

    [Fact]
    public void Constructor_WithNullSettingsLoadingService_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new TestableSettingsFeatureViewModel(
            _mockDomainServiceRouter.Object,
            null!,
            _mockLogService.Object,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object,
            _mockEventBus.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("settingsLoadingService");
    }

    [Fact]
    public void Constructor_WithNullLogService_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new TestableSettingsFeatureViewModel(
            _mockDomainServiceRouter.Object,
            _mockSettingsLoadingService.Object,
            null!,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object,
            _mockEventBus.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logService");
    }

    [Fact]
    public void Constructor_WithNullLocalizationService_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new TestableSettingsFeatureViewModel(
            _mockDomainServiceRouter.Object,
            _mockSettingsLoadingService.Object,
            _mockLogService.Object,
            null!,
            _mockDispatcherService.Object,
            _mockEventBus.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("localizationService");
    }

    [Fact]
    public void Constructor_WithNullDispatcherService_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new TestableSettingsFeatureViewModel(
            _mockDomainServiceRouter.Object,
            _mockSettingsLoadingService.Object,
            _mockLogService.Object,
            _mockLocalizationService.Object,
            null!,
            _mockEventBus.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("dispatcherService");
    }

    [Fact]
    public void Constructor_WithNullEventBus_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new TestableSettingsFeatureViewModel(
            _mockDomainServiceRouter.Object,
            _mockSettingsLoadingService.Object,
            _mockLogService.Object,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object,
            null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("eventBus");
    }

    // ── Default Property Tests ──

    [Fact]
    public void Settings_DefaultsToEmptyCollection()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.Settings.Should().NotBeNull();
        vm.Settings.Should().BeEmpty();
    }

    [Fact]
    public void GroupedSettings_DefaultsToEmptyCollection()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.GroupedSettings.Should().NotBeNull();
        vm.GroupedSettings.Should().BeEmpty();
    }

    [Fact]
    public void IsLoading_DefaultsToFalse()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void IsExpanded_DefaultsToTrue()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void SearchText_DefaultsToEmptyString()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.SearchText.Should().BeEmpty();
    }

    [Fact]
    public void ModuleId_ReturnsTestModuleId()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.ModuleId.Should().Be(TestableSettingsFeatureViewModel.TestModuleId);
    }

    [Fact]
    public void DisplayName_ReturnsLocalizedString()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString(TestableSettingsFeatureViewModel.TestDisplayNameKey))
            .Returns("Test Feature");

        var vm = CreateViewModel();

        // Act & Assert
        vm.DisplayName.Should().Be("Test Feature");
    }

    [Fact]
    public void SettingsCount_WhenNoSettings_ReturnsZero()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.SettingsCount.Should().Be(0);
    }

    [Fact]
    public void HasVisibleSettings_WhenNoSettings_ReturnsFalse()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.HasVisibleSettings.Should().BeFalse();
    }

    [Fact]
    public void IsVisibleInSearch_WhenNoSettings_ReturnsFalse()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.IsVisibleInSearch.Should().BeFalse();
    }

    // ── LoadSettingsCommand / ToggleExpandCommand ──

    [Fact]
    public void LoadSettingsCommand_IsNotNull()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.LoadSettingsCommand.Should().NotBeNull();
    }

    [Fact]
    public void ToggleExpandCommand_IsNotNull()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.ToggleExpandCommand.Should().NotBeNull();
    }

    [Fact]
    public void ToggleExpandCommand_TogglesIsExpanded()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.IsExpanded.Should().BeTrue();

        // Act
        vm.ToggleExpandCommand.Execute(null);

        // Assert
        vm.IsExpanded.Should().BeFalse();
    }

    [Fact]
    public void ToggleExpandCommand_TogglesBackToTrue()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.IsExpanded = false;

        // Act
        vm.ToggleExpandCommand.Execute(null);

        // Assert
        vm.IsExpanded.Should().BeTrue();
    }

    // ── IsExpanded Toggle ──

    [Fact]
    public void IsExpanded_WhenSetToFalse_RaisesPropertyChanged()
    {
        // Arrange
        var vm = CreateViewModel();
        var propertyChangedRaised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsExpanded))
                propertyChangedRaised = true;
        };

        // Act
        vm.IsExpanded = false;

        // Assert
        propertyChangedRaised.Should().BeTrue();
    }

    // ── LoadSettingsAsync ──

    [Fact]
    public async Task LoadSettingsAsync_SetsIsLoadingTrueDuringLoad()
    {
        // Arrange
        var vm = CreateViewModel();
        bool wasLoadingDuringLoad = false;

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(() =>
            {
                wasLoadingDuringLoad = vm.IsLoading;
                return new ObservableCollection<SettingItemViewModel>();
            });

        // Act
        await vm.LoadSettingsAsync();

        // Assert
        wasLoadingDuringLoad.Should().BeTrue();
        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadSettingsAsync_SetsIsLoadingFalseAfterLoad()
    {
        // Arrange
        var vm = CreateViewModel();

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(new ObservableCollection<SettingItemViewModel>());

        // Act
        await vm.LoadSettingsAsync();

        // Assert
        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadSettingsAsync_PopulatesSettingsCollection()
    {
        // Arrange
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("setting1", "Setting 1", "Group A"),
            ("setting2", "Setting 2", "Group A"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        // Act
        await vm.LoadSettingsAsync();

        // Assert
        vm.Settings.Should().HaveCount(2);
        vm.SettingsCount.Should().Be(2);
    }

    [Fact]
    public async Task LoadSettingsAsync_RebuildGroupedSettings()
    {
        // Arrange
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("s1", "Setting 1", "Group A"),
            ("s2", "Setting 2", "Group B"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        // Act
        await vm.LoadSettingsAsync();

        // Assert
        vm.GroupedSettings.Should().HaveCount(2);
        vm.GroupedSettings[0].Key.Should().Be("Group A");
        vm.GroupedSettings[1].Key.Should().Be("Group B");
    }

    [Fact]
    public async Task LoadSettingsAsync_ConcurrentLoadGuard_DoesNotLoadTwice()
    {
        // Arrange
        var vm = CreateViewModel();
        var loadCount = 0;

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(() =>
            {
                loadCount++;
                return new ObservableCollection<SettingItemViewModel>();
            });

        // Act - load twice
        await vm.LoadSettingsAsync();
        await vm.LoadSettingsAsync();

        // Assert - should only load once due to _settingsLoaded guard
        loadCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadSettingsAsync_SubscribesToEvents()
    {
        // Arrange
        var vm = CreateViewModel();

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(new ObservableCollection<SettingItemViewModel>());

        // Act
        await vm.LoadSettingsAsync();

        // Assert - verify event subscriptions were made
        _mockEventBus.Verify(
            e => e.Subscribe(It.IsAny<Action<SettingAppliedEvent>>()),
            Times.Once);
        _mockEventBus.Verify(
            e => e.SubscribeAsync(It.IsAny<Func<FilterStateChangedEvent, Task>>()),
            Times.Once);
        _mockEventBus.Verify(
            e => e.Subscribe(It.IsAny<Action<ReviewModeExitedEvent>>()),
            Times.Once);
    }

    [Fact]
    public async Task LoadSettingsAsync_OnError_SetsIsLoadingFalse()
    {
        // Arrange
        var vm = CreateViewModel();

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        // Act
        Func<Task> action = () => vm.LoadSettingsAsync();

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>();
        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadSettingsAsync_OnError_ResetsSettingsLoadedFlag_AllowsRetry()
    {
        // Arrange
        var vm = CreateViewModel();
        var callCount = 0;

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("First call fails");
                return new ObservableCollection<SettingItemViewModel>();
            });

        // Act - first call should fail
        Func<Task> firstCall = () => vm.LoadSettingsAsync();
        await firstCall.Should().ThrowAsync<InvalidOperationException>();

        // Second call should succeed because the flag was reset
        await vm.LoadSettingsAsync();

        // Assert
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task LoadSettingsAsync_UsesCorrectModuleId()
    {
        // Arrange
        var vm = CreateViewModel();

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(new ObservableCollection<SettingItemViewModel>());

        // Act
        await vm.LoadSettingsAsync();

        // Assert
        _mockDomainServiceRouter.Verify(
            r => r.GetDomainService(TestableSettingsFeatureViewModel.TestModuleId),
            Times.Once);

        _mockSettingsLoadingService.Verify(
            s => s.LoadConfiguredSettingsAsync(
                _mockDomainService.Object,
                TestableSettingsFeatureViewModel.TestModuleId,
                It.IsAny<string>(),
                vm),
            Times.Once);
    }

    // ── RefreshSettingsAsync ──

    [Fact]
    public async Task RefreshSettingsAsync_ClearsAndReloadsSettings()
    {
        // Arrange
        var vm = CreateViewModel();
        var firstSettings = CreateSettingsCollection(("s1", "First", "G1"));
        var secondSettings = CreateSettingsCollection(("s2", "Second", "G2"));
        var callCount = 0;

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? firstSettings : secondSettings;
            });

        await vm.LoadSettingsAsync();
        vm.Settings.Should().HaveCount(1);

        // Act
        await vm.RefreshSettingsAsync();

        // Assert
        vm.Settings.Should().HaveCount(1);
        vm.Settings[0].Name.Should().Be("Second");
    }

    // ── RefreshSettingStatesAsync ──

    [Fact]
    public async Task RefreshSettingStatesAsync_WhenSettingsNotLoaded_DoesNothing()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act - settings have never been loaded, so this should be a no-op
        await vm.RefreshSettingStatesAsync();

        // Assert
        _mockSettingsLoadingService.Verify(
            s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()),
            Times.Never);
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_WhenSettingsLoaded_RefreshesStates()
    {
        // Arrange
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(("s1", "Setting 1", "Group"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        _mockSettingsLoadingService
            .Setup(s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());

        // Load first
        await vm.LoadSettingsAsync();

        // Act
        await vm.RefreshSettingStatesAsync();

        // Assert
        _mockSettingsLoadingService.Verify(
            s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_UpdatesSettingStatesFromResults()
    {
        // Arrange
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(("s1", "Setting 1", "Group"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        var stateResults = new Dictionary<string, SettingStateResult>
        {
            ["s1"] = new SettingStateResult { Success = true, IsEnabled = true }
        };

        _mockSettingsLoadingService
            .Setup(s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()))
            .ReturnsAsync(stateResults);

        await vm.LoadSettingsAsync();

        // Act
        await vm.RefreshSettingStatesAsync();

        // Assert - the setting should have been updated via UpdateStateFromSystemState
        // The actual state update is done via the dispatcher mock, which runs synchronously
        _mockDispatcherService.Verify(
            d => d.RunOnUIThread(It.IsAny<Action>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_PublishesTooltipUpdatedEvents()
    {
        // Arrange
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(("s1", "Setting 1", "Group"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        var tooltipData = new SettingTooltipData { SettingId = "s1", DisplayValue = "test" };
        var stateResults = new Dictionary<string, SettingStateResult>
        {
            ["s1"] = new SettingStateResult
            {
                Success = true,
                IsEnabled = true,
                TooltipData = tooltipData
            }
        };

        _mockSettingsLoadingService
            .Setup(s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()))
            .ReturnsAsync(stateResults);

        await vm.LoadSettingsAsync();
        _mockEventBus.Invocations.Clear();

        // Act
        await vm.RefreshSettingStatesAsync();

        // Assert - TooltipUpdatedEvent should be published directly from state results
        _mockEventBus.Verify(
            e => e.Publish(It.Is<TooltipUpdatedEvent>(evt => evt.SettingId == "s1")),
            Times.Once);
    }

    // ── ApplySearchFilter ──

    [Fact]
    public void ApplySearchFilter_SetsSearchText()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.ApplySearchFilter("test");

        // Assert
        vm.SearchText.Should().Be("test");
    }

    [Fact]
    public void ApplySearchFilter_WithNull_SetsEmptyString()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.ApplySearchFilter(null!);

        // Assert
        vm.SearchText.Should().BeEmpty();
    }

    [Fact]
    public void ApplySearchFilter_WithEmptyString_SetsEmptyString()
    {
        // Arrange
        var vm = CreateViewModel();
        vm.ApplySearchFilter("initial");

        // Act
        vm.ApplySearchFilter(string.Empty);

        // Assert
        vm.SearchText.Should().BeEmpty();
    }

    // ── HasVisibleSettings / IsVisibleInSearch ──

    [Fact]
    public async Task HasVisibleSettings_WhenSettingsExist_ReturnsTrue()
    {
        // Arrange
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(("s1", "Setting 1", "Group"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        // Act
        await vm.LoadSettingsAsync();

        // Assert - newly created settings have IsVisible = true by default
        vm.HasVisibleSettings.Should().BeTrue();
        vm.IsVisibleInSearch.Should().BeTrue();
    }

    // ── SettingsCount ──

    [Fact]
    public async Task SettingsCount_AfterLoading_ReturnsCorrectCount()
    {
        // Arrange
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("s1", "Setting 1", "G1"),
            ("s2", "Setting 2", "G1"),
            ("s3", "Setting 3", "G2"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        // Act
        await vm.LoadSettingsAsync();

        // Assert
        vm.SettingsCount.Should().Be(3);
    }

    // ── GroupDescriptionText ──

    [Fact]
    public void GroupDescriptionText_WhenNoSettings_ReturnsEmptyString()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.GroupDescriptionText.Should().BeEmpty();
    }

    [Fact]
    public async Task GroupDescriptionText_WithGroupedSettings_ReturnsGroupNames()
    {
        // Arrange
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("s1", "Setting 1", "Alpha"),
            ("s2", "Setting 2", "Beta"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        // Act
        await vm.LoadSettingsAsync();

        // Assert
        vm.GroupDescriptionText.Should().Contain("Alpha");
        vm.GroupDescriptionText.Should().Contain("Beta");
    }

    [Fact]
    public async Task GroupDescriptionText_WithMoreThan4Groups_AppendEllipsis()
    {
        // Arrange
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("s1", "S1", "Group1"),
            ("s2", "S2", "Group2"),
            ("s3", "S3", "Group3"),
            ("s4", "S4", "Group4"),
            ("s5", "S5", "Group5"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        // Act
        await vm.LoadSettingsAsync();

        // Assert
        vm.GroupDescriptionText.Should().EndWith(", ...");
    }

    [Fact]
    public async Task GroupDescriptionText_WithExactly4Groups_DoesNotAppendEllipsis()
    {
        // Arrange
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("s1", "S1", "Group1"),
            ("s2", "S2", "Group2"),
            ("s3", "S3", "Group3"),
            ("s4", "S4", "Group4"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        // Act
        await vm.LoadSettingsAsync();

        // Assert
        vm.GroupDescriptionText.Should().NotEndWith(", ...");
    }

    [Fact]
    public async Task GroupDescriptionText_WithEmptyGroupNames_ReturnsEmptyString()
    {
        // Arrange
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("s1", "S1", ""),
            ("s2", "S2", ""));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        // Act
        await vm.LoadSettingsAsync();

        // Assert - settings with empty group names do not contribute to GroupDescriptionText
        vm.GroupDescriptionText.Should().BeEmpty();
    }

    // ── GroupedSettings Rebuild ──

    [Fact]
    public async Task LoadSettingsAsync_GroupsSettingsByGroupName()
    {
        // Arrange
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("s1", "Setting 1", "Alpha"),
            ("s2", "Setting 2", "Alpha"),
            ("s3", "Setting 3", "Beta"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        // Act
        await vm.LoadSettingsAsync();

        // Assert
        vm.GroupedSettings.Should().HaveCount(2);
        vm.GroupedSettings[0].Key.Should().Be("Alpha");
        vm.GroupedSettings[0].Should().HaveCount(2);
        vm.GroupedSettings[1].Key.Should().Be("Beta");
        vm.GroupedSettings[1].Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadSettingsAsync_SettingsWithEmptyGroupName_FallsBackToOtherGroup()
    {
        // Arrange
        var vm = CreateViewModel();

        // Mock the "Other" localization to return a bracketed value, triggering the fallback
        _mockLocalizationService
            .Setup(l => l.GetString("SettingGroup_Other"))
            .Returns("[SettingGroup_Other]");

        var settings = CreateSettingsCollection(
            ("s1", "Setting 1", ""));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        // Act
        await vm.LoadSettingsAsync();

        // Assert - should fall back to "Other" when localization returns bracketed key
        vm.GroupedSettings.Should().HaveCount(1);
        vm.GroupedSettings[0].Key.Should().Be("Other");
    }

    // ── Dispose ──

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        var action = () => vm.Dispose();

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        var action = () =>
        {
            vm.Dispose();
            vm.Dispose();
        };

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_UnsubscribesFromEvents()
    {
        // Arrange
        var mockSettingToken = new Mock<ISubscriptionToken>();
        var mockFilterToken = new Mock<ISubscriptionToken>();
        var mockReviewToken = new Mock<ISubscriptionToken>();

        _mockEventBus
            .Setup(e => e.Subscribe(It.IsAny<Action<SettingAppliedEvent>>()))
            .Returns(mockSettingToken.Object);
        _mockEventBus
            .Setup(e => e.SubscribeAsync(It.IsAny<Func<FilterStateChangedEvent, Task>>()))
            .Returns(mockFilterToken.Object);
        _mockEventBus
            .Setup(e => e.Subscribe(It.IsAny<Action<ReviewModeExitedEvent>>()))
            .Returns(mockReviewToken.Object);

        var vm = CreateViewModel();

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(new ObservableCollection<SettingItemViewModel>());

        // Trigger event subscriptions by loading
        await vm.LoadSettingsAsync();

        // Act
        vm.Dispose();

        // Assert - subscription tokens should be disposed
        mockSettingToken.Verify(t => t.Dispose(), Times.Once);
        mockFilterToken.Verify(t => t.Dispose(), Times.Once);
        mockReviewToken.Verify(t => t.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Dispose_ClearsSettingsCollection()
    {
        // Arrange
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(("s1", "Setting 1", "Group"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();
        vm.Settings.Should().NotBeEmpty();

        // Act
        vm.Dispose();

        // Assert
        vm.Settings.Should().BeEmpty();
    }

    // ── Property Changed Notifications ──

    [Fact]
    public async Task LoadSettingsAsync_RaisesPropertyChangedForHasVisibleSettings()
    {
        // Arrange
        var vm = CreateViewModel();
        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(new ObservableCollection<SettingItemViewModel>());

        // Act
        await vm.LoadSettingsAsync();

        // Assert
        raisedProperties.Should().Contain(nameof(vm.HasVisibleSettings));
        raisedProperties.Should().Contain(nameof(vm.IsVisibleInSearch));
        raisedProperties.Should().Contain(nameof(vm.SettingsCount));
        raisedProperties.Should().Contain(nameof(vm.GroupDescriptionText));
    }

    [Fact]
    public async Task LoadSettingsAsync_RaisesPropertyChangedForIsLoading()
    {
        // Arrange
        var vm = CreateViewModel();
        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(new ObservableCollection<SettingItemViewModel>());

        // Act
        await vm.LoadSettingsAsync();

        // Assert
        raisedProperties.Should().Contain(nameof(vm.IsLoading));
    }

    // ── LoadSettingsAsync: InputType population (guards #482 blank page) ──

    [Fact]
    public async Task LoadSettingsAsync_WithToggleSettings_PopulatesIsSelectedFromState()
    {
        var vm = CreateViewModel();
        var settings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("toggle1", "Toggle Setting", isSelected: true)
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        vm.Settings.Should().HaveCount(1);
        vm.Settings[0].IsSelected.Should().BeTrue();
        vm.HasVisibleSettings.Should().BeTrue();
    }

    [Fact]
    public async Task LoadSettingsAsync_WithSelectionSettings_PopulatesSelectedValueAndComboBoxOptions()
    {
        var vm = CreateViewModel();
        var comboBox = new ComboBoxMetadata
        {
            Options = new[]
            {
                new Winhance.Core.Features.Common.Models.ComboBoxOption { DisplayName = "Low" },
                new Winhance.Core.Features.Common.Models.ComboBoxOption { DisplayName = "Medium" },
                new Winhance.Core.Features.Common.Models.ComboBoxOption { DisplayName = "High" }
            }
        };
        var settings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("sel1", "Selection Setting", inputType: InputType.Selection,
                selectedValue: 1, comboBox: comboBox)
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        vm.Settings.Should().HaveCount(1);
        vm.Settings[0].SelectedValue.Should().Be(1);
        vm.HasVisibleSettings.Should().BeTrue();
    }

    [Fact]
    public async Task LoadSettingsAsync_WithNumericRangeSettings_PopulatesNumericValueAndBounds()
    {
        var vm = CreateViewModel();
        var numericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 120, Units = "Minutes" };
        var settings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("num1", "Numeric Setting", inputType: InputType.NumericRange,
                numericValue: 30, numericRange: numericRange)
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        vm.Settings.Should().HaveCount(1);
        vm.Settings[0].NumericValue.Should().Be(30);
        vm.HasVisibleSettings.Should().BeTrue();
    }

    [Fact]
    public async Task LoadSettingsAsync_WithMixedInputTypes_AllSettingsPopulatedCorrectly()
    {
        var vm = CreateViewModel();
        var settings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("toggle1", "Toggle", inputType: InputType.Toggle, isSelected: true),
            CreateSettingItem("sel1", "Selection", inputType: InputType.Selection, selectedValue: 2),
            CreateSettingItem("num1", "Numeric", inputType: InputType.NumericRange, numericValue: 45,
                numericRange: new NumericRangeMetadata { MinValue = 0, MaxValue = 100 }),
            CreateSettingItem("action1", "Action", inputType: InputType.Action)
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        vm.Settings.Should().HaveCount(4);
        vm.Settings[0].IsSelected.Should().BeTrue();
        vm.Settings[1].SelectedValue.Should().Be(2);
        vm.Settings[2].NumericValue.Should().Be(45);
        vm.Settings[3].InputType.Should().Be(InputType.Action);
        vm.HasVisibleSettings.Should().BeTrue();
    }

    // ── RefreshSettingStatesAsync: value updates (guards #483 value corruption) ──

    [Fact]
    public async Task RefreshSettingStatesAsync_ToggleSetting_UpdatesIsSelectedFromNewState()
    {
        var vm = CreateViewModel();
        var settings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("t1", "Toggle", isSelected: false)
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        var refreshStates = new Dictionary<string, SettingStateResult>
        {
            ["t1"] = new SettingStateResult { Success = true, IsEnabled = true }
        };
        _mockSettingsLoadingService
            .Setup(s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()))
            .ReturnsAsync(refreshStates);

        await vm.LoadSettingsAsync();
        vm.Settings[0].IsSelected.Should().BeFalse();

        await vm.RefreshSettingStatesAsync();

        vm.Settings[0].IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_SelectionSetting_UpdatesSelectedValueFromNewState()
    {
        var vm = CreateViewModel();
        var settings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("sel1", "Selection", inputType: InputType.Selection, selectedValue: 0)
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        var refreshStates = new Dictionary<string, SettingStateResult>
        {
            ["sel1"] = new SettingStateResult { Success = true, CurrentValue = 2 }
        };
        _mockSettingsLoadingService
            .Setup(s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()))
            .ReturnsAsync(refreshStates);

        await vm.LoadSettingsAsync();

        await vm.RefreshSettingStatesAsync();

        vm.Settings[0].SelectedValue.Should().Be(2);
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_NumericRangeSetting_UpdatesNumericValueFromNewState()
    {
        var vm = CreateViewModel();
        var numericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 120, Units = "Minutes" };
        var settings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("num1", "Numeric", inputType: InputType.NumericRange,
                numericValue: 10, numericRange: numericRange)
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        // RefreshSettingStatesAsync returns raw system values (seconds), unit conversion happens in UpdateStateFromSystemState
        var refreshStates = new Dictionary<string, SettingStateResult>
        {
            ["num1"] = new SettingStateResult { Success = true, CurrentValue = 1800 }
        };
        _mockSettingsLoadingService
            .Setup(s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()))
            .ReturnsAsync(refreshStates);

        await vm.LoadSettingsAsync();
        vm.Settings[0].NumericValue.Should().Be(10);

        await vm.RefreshSettingStatesAsync();

        vm.Settings[0].NumericValue.Should().Be(30); // 1800 seconds / 60 = 30 minutes
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_FailedResult_DoesNotChangeExistingValues()
    {
        var vm = CreateViewModel();
        var settings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("t1", "Toggle", isSelected: true)
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        var refreshStates = new Dictionary<string, SettingStateResult>
        {
            ["t1"] = new SettingStateResult { Success = false, IsEnabled = false }
        };
        _mockSettingsLoadingService
            .Setup(s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()))
            .ReturnsAsync(refreshStates);

        await vm.LoadSettingsAsync();

        await vm.RefreshSettingStatesAsync();

        vm.Settings[0].IsSelected.Should().BeTrue(); // unchanged due to failed result
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_MissingSettingInResults_DoesNotThrow()
    {
        var vm = CreateViewModel();
        var settings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("t1", "Toggle", isSelected: true),
            CreateSettingItem("t2", "Toggle 2", isSelected: false)
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        // Only return state for t1, not t2
        var refreshStates = new Dictionary<string, SettingStateResult>
        {
            ["t1"] = new SettingStateResult { Success = true, IsEnabled = false }
        };
        _mockSettingsLoadingService
            .Setup(s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()))
            .ReturnsAsync(refreshStates);

        await vm.LoadSettingsAsync();

        var action = () => vm.RefreshSettingStatesAsync();

        await action.Should().NotThrowAsync();
        vm.Settings[0].IsSelected.Should().BeFalse(); // updated
        vm.Settings[1].IsSelected.Should().BeFalse(); // unchanged (no state returned)
    }

    // ── Full lifecycle: load → refresh → values preserved ──

    [Fact]
    public async Task FullLifecycle_LoadThenRefreshWithSameValues_PreservesAllTypes()
    {
        var vm = CreateViewModel();
        var numericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 60 };
        var settings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("t1", "Toggle", isSelected: true),
            CreateSettingItem("sel1", "Selection", inputType: InputType.Selection, selectedValue: 1),
            CreateSettingItem("num1", "Numeric", inputType: InputType.NumericRange,
                numericValue: 42, numericRange: numericRange)
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        // Refresh returns the same values
        var refreshStates = new Dictionary<string, SettingStateResult>
        {
            ["t1"] = new SettingStateResult { Success = true, IsEnabled = true },
            ["sel1"] = new SettingStateResult { Success = true, CurrentValue = 1 },
            ["num1"] = new SettingStateResult { Success = true, CurrentValue = 42 }
        };
        _mockSettingsLoadingService
            .Setup(s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()))
            .ReturnsAsync(refreshStates);

        await vm.LoadSettingsAsync();
        await vm.RefreshSettingStatesAsync();

        vm.Settings[0].IsSelected.Should().BeTrue();
        vm.Settings[1].SelectedValue.Should().Be(1);
        vm.Settings[2].NumericValue.Should().Be(42);
    }

    [Fact]
    public async Task FullLifecycle_LoadThenRefreshWithChangedValues_UpdatesAllTypes()
    {
        var vm = CreateViewModel();
        var numericRange = new NumericRangeMetadata { MinValue = 0, MaxValue = 100 };
        var settings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("t1", "Toggle", isSelected: false),
            CreateSettingItem("sel1", "Selection", inputType: InputType.Selection, selectedValue: 0),
            CreateSettingItem("num1", "Numeric", inputType: InputType.NumericRange,
                numericValue: 10, numericRange: numericRange)
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        // Refresh returns different values
        var refreshStates = new Dictionary<string, SettingStateResult>
        {
            ["t1"] = new SettingStateResult { Success = true, IsEnabled = true },
            ["sel1"] = new SettingStateResult { Success = true, CurrentValue = 3 },
            ["num1"] = new SettingStateResult { Success = true, CurrentValue = 75 }
        };
        _mockSettingsLoadingService
            .Setup(s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()))
            .ReturnsAsync(refreshStates);

        await vm.LoadSettingsAsync();
        await vm.RefreshSettingStatesAsync();

        vm.Settings[0].IsSelected.Should().BeTrue();
        vm.Settings[1].SelectedValue.Should().Be(3);
        vm.Settings[2].NumericValue.Should().Be(75);
    }

    // ── Multiple reload cycles ──

    [Fact]
    public async Task RefreshSettingsAsync_DisposesOldSettings_LoadsNewOnes()
    {
        var vm = CreateViewModel();
        var firstSettings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("old1", "Old Setting 1"),
            CreateSettingItem("old2", "Old Setting 2")
        };
        var secondSettings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("new1", "New Setting 1"),
            CreateSettingItem("new2", "New Setting 2"),
            CreateSettingItem("new3", "New Setting 3")
        };
        var callCount = 0;

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? firstSettings : secondSettings;
            });

        await vm.LoadSettingsAsync();
        vm.Settings.Should().HaveCount(2);
        vm.Settings[0].Name.Should().Be("Old Setting 1");

        await vm.RefreshSettingsAsync();

        vm.Settings.Should().HaveCount(3);
        vm.Settings[0].Name.Should().Be("New Setting 1");
    }

    [Fact]
    public async Task LoadSettingsAsync_CalledAfterRefreshSettings_RebuildsGroupedSettings()
    {
        var vm = CreateViewModel();
        var firstSettings = CreateSettingsCollection(("s1", "S1", "GroupA"));
        var secondSettings = CreateSettingsCollection(("s2", "S2", "GroupB"), ("s3", "S3", "GroupC"));
        var callCount = 0;

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? firstSettings : secondSettings;
            });

        await vm.LoadSettingsAsync();
        vm.GroupedSettings.Should().HaveCount(1);
        vm.GroupedSettings[0].Key.Should().Be("GroupA");

        await vm.RefreshSettingsAsync();

        vm.GroupedSettings.Should().HaveCount(2);
        vm.GroupedSettings[0].Key.Should().Be("GroupB");
        vm.GroupedSettings[1].Key.Should().Be("GroupC");
    }

    // ── GroupedSettings Ordering ──

    [Fact]
    public async Task LoadSettingsAsync_GroupedSettingsPreservesInsertionOrder()
    {
        // Arrange
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("s1", "S1", "Zebra"),
            ("s2", "S2", "Alpha"),
            ("s3", "S3", "Middle"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<IDomainService>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        // Act
        await vm.LoadSettingsAsync();

        // Assert - groups should appear in the order settings were encountered, not alphabetically
        vm.GroupedSettings[0].Key.Should().Be("Zebra");
        vm.GroupedSettings[1].Key.Should().Be("Alpha");
        vm.GroupedSettings[2].Key.Should().Be("Middle");
    }
}
