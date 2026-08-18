using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Events.Settings;
using Winhance.Core.Features.Common.Events.UI;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Catalog;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;
using ISettingsLoadingService = Winhance.UI.Features.Common.Interfaces.ISettingsLoadingService;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.ViewModels;

public class TestableSettingsFeatureViewModel : BaseSettingsFeatureViewModel
{
    public const string TestModuleId = "TestModule";
    public const string TestDisplayNameKey = "Feature_Test_Name";

    public override string ModuleId => TestModuleId;

    protected override string GetDisplayNameKey() => TestDisplayNameKey;

    public TestableSettingsFeatureViewModel(
        ISettingsLoadingService settingsLoadingService,
        ILogService logService,
        ILocalizationService localizationService,
        IDispatcherService dispatcherService,
        IEventBus eventBus,
        IApplicationModeService applicationModeService)
        : base(settingsLoadingService, logService, localizationService, dispatcherService, eventBus, applicationModeService)
    {
    }
}

public class BaseSettingsFeatureViewModelTests
{
    private readonly Mock<ISettingsLoadingService> _mockSettingsLoadingService;
    private readonly Mock<ILogService> _mockLogService;
    private readonly Mock<ILocalizationService> _mockLocalizationService;
    private readonly Mock<IDispatcherService> _mockDispatcherService;
    private readonly Mock<IEventBus> _mockEventBus;
    private readonly Mock<IApplicationModeService> _mockApplicationModeService;

    public BaseSettingsFeatureViewModelTests()
    {
        _mockSettingsLoadingService = new Mock<ISettingsLoadingService>();
        _mockLogService = new Mock<ILogService>();
        _mockLocalizationService = new Mock<ILocalizationService>();
        _mockDispatcherService = new Mock<IDispatcherService>();
        _mockEventBus = new Mock<IEventBus>();
        _mockApplicationModeService = new Mock<IApplicationModeService>();

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        // Mirrors the stub above onto TryGetString - an unstubbed Moq answers "missing" for every key.
        _mockLocalizationService.MirrorTryGetString();

        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(action => action());

        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(asyncAction => asyncAction());

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

    private TestableSettingsFeatureViewModel CreateViewModel()
    {
        return new TestableSettingsFeatureViewModel(
            _mockSettingsLoadingService.Object,
            _mockLogService.Object,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object,
            _mockEventBus.Object,
            _mockApplicationModeService.Object);
    }

    private SettingItemViewModel CreateSettingItem(
        string settingId,
        string name,
        string description = "Description",
        string groupName = "Group1",
        InputType inputType = InputType.Toggle,
        bool isSelected = false,
        int numericValue = 0,
        object? selectedValue = null,
        // Display units for a NumericRange setting ("Minutes"/"Hours"/"Milliseconds"). Null leaves
        // Setting.Numeric unset, so UnitConversionHelper passes raw system values through unchanged -
        // which is what the tests asserting NumericValue == the raw CurrentValue rely on. Supply it
        // only when the test is actually exercising the unit conversion.
        string? numericUnits = null,
        // Catalog authoring the presentation gate reads: where the card nests, whether it declares a
        // gate, and the state labels the gate compares against. All null = an ungated, top-level card.
        string? uiParentId = null,
        EnabledWhen? enabledWhen = null,
        string[]? stateLabels = null)
    {
        var config = new SettingItemViewModelConfig
        {
            Setting = new Setting
            {
                Id = settingId,
                Display = new() { Name = name, Description = description },
                Numeric = numericUnits is null
                    ? null
                    : new Numeric { Min = 0, Max = 100_000, Units = numericUnits },
                UiParentId = uiParentId,
                EnabledWhen = enabledWhen,
                States = (stateLabels ?? Array.Empty<string>())
                    .Select(label => new SettingState { Label = label })
                    .ToArray(),
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
            SettingWriteStrategies.Selector(
                mockSettingAppService.Object, mockDialogService.Object, _mockLocalizationService.Object, _mockLogService.Object),
            _mockLogService.Object,
            _mockDispatcherService.Object,
            mockDialogService.Object,
            _mockLocalizationService.Object);

        if (inputType == InputType.NumericRange)
            vm.NumericValue = numericValue;
        if (inputType == InputType.Selection && selectedValue != null)
            vm.SelectedValue = selectedValue;

        return vm;
    }

    // Enabled/Disabled - the two labels every catalog toggle has, and what CurrentStateLabel maps
    // IsSelected onto.
    private static readonly string[] ToggleStates = { "Enabled", "Disabled" };
    private static readonly string[] EnabledOnly = ["Enabled"];
    private static readonly string[] LightDarkModes = ["Light Mode", "Dark Mode"];
    private static readonly string[] LightModeOnly = ["Light Mode"];
    private static readonly string[] ServiceStartModes = ["Off", "Manual", "Automatic"];
    private static readonly string[] ServiceRunningModes = ["Manual", "Automatic"];

    private void SetupLoad(ObservableCollection<SettingItemViewModel> settings) =>
        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

    private ObservableCollection<SettingItemViewModel> CreateSettingsCollection(params (string id, string name, string group)[] items)
    {
        var collection = new ObservableCollection<SettingItemViewModel>();
        foreach (var (id, name, group) in items)
        {
            collection.Add(CreateSettingItem(id, name, groupName: group));
        }
        return collection;
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        var vm = CreateViewModel();

        vm.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        var action = () => CreateViewModel();

        action.Should().NotThrow();
    }

    [Fact]
    public void Settings_DefaultsToEmptyCollection()
    {
        var vm = CreateViewModel();

        vm.Settings.Should().NotBeNull();
        vm.Settings.Should().BeEmpty();
    }

    [Fact]
    public void GroupedSettings_DefaultsToEmptyCollection()
    {
        var vm = CreateViewModel();

        vm.GroupedSettings.Should().NotBeNull();
        vm.GroupedSettings.Should().BeEmpty();
    }

    [Fact]
    public void IsLoading_DefaultsToFalse()
    {
        var vm = CreateViewModel();

        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public void IsExpanded_DefaultsToTrue()
    {
        var vm = CreateViewModel();

        vm.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void SearchText_DefaultsToEmptyString()
    {
        var vm = CreateViewModel();

        vm.SearchText.Should().BeEmpty();
    }

    [Fact]
    public void ModuleId_ReturnsTestModuleId()
    {
        var vm = CreateViewModel();

        vm.ModuleId.Should().Be(TestableSettingsFeatureViewModel.TestModuleId);
    }

    [Fact]
    public void DisplayName_ReturnsLocalizedString()
    {
        _mockLocalizationService
            .Setup(l => l.GetString(TestableSettingsFeatureViewModel.TestDisplayNameKey))
            .Returns("Test Feature");

        var vm = CreateViewModel();

        vm.DisplayName.Should().Be("Test Feature");
    }

    [Fact]
    public void SettingsCount_WhenNoSettings_ReturnsZero()
    {
        var vm = CreateViewModel();

        vm.SettingsCount.Should().Be(0);
    }

    [Fact]
    public void HasVisibleSettings_WhenNoSettings_ReturnsFalse()
    {
        var vm = CreateViewModel();

        vm.HasVisibleSettings.Should().BeFalse();
    }

    [Fact]
    public void IsVisibleInSearch_WhenNoSettings_ReturnsFalse()
    {
        var vm = CreateViewModel();

        vm.IsVisibleInSearch.Should().BeFalse();
    }

    [Fact]
    public void LoadSettingsCommand_IsNotNull()
    {
        var vm = CreateViewModel();

        vm.LoadSettingsCommand.Should().NotBeNull();
    }

    [Fact]
    public void ToggleExpandCommand_IsNotNull()
    {
        var vm = CreateViewModel();

        vm.ToggleExpandCommand.Should().NotBeNull();
    }

    [Fact]
    public void ToggleExpandCommand_TogglesIsExpanded()
    {
        var vm = CreateViewModel();
        vm.IsExpanded.Should().BeTrue();

        vm.ToggleExpandCommand.Execute(null);

        vm.IsExpanded.Should().BeFalse();
    }

    [Fact]
    public void ToggleExpandCommand_TogglesBackToTrue()
    {
        var vm = CreateViewModel();
        vm.IsExpanded = false;

        vm.ToggleExpandCommand.Execute(null);

        vm.IsExpanded.Should().BeTrue();
    }

    [Fact]
    public void IsExpanded_WhenSetToFalse_RaisesPropertyChanged()
    {
        var vm = CreateViewModel();
        var propertyChangedRaised = false;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsExpanded))
                propertyChangedRaised = true;
        };

        vm.IsExpanded = false;

        propertyChangedRaised.Should().BeTrue();
    }

    [Fact]
    public async Task LoadSettingsAsync_SetsIsLoadingTrueDuringLoad()
    {
        var vm = CreateViewModel();
        bool wasLoadingDuringLoad = false;

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(() =>
            {
                wasLoadingDuringLoad = vm.IsLoading;
                return new ObservableCollection<SettingItemViewModel>();
            });

        await vm.LoadSettingsAsync();

        wasLoadingDuringLoad.Should().BeTrue();
        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadSettingsAsync_SetsIsLoadingFalseAfterLoad()
    {
        var vm = CreateViewModel();

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(new ObservableCollection<SettingItemViewModel>());

        await vm.LoadSettingsAsync();

        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadSettingsAsync_PopulatesSettingsCollection()
    {
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("setting1", "Setting 1", "Group A"),
            ("setting2", "Setting 2", "Group A"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        vm.Settings.Should().HaveCount(2);
        vm.SettingsCount.Should().Be(2);
    }

    [Fact]
    public async Task LoadSettingsAsync_RebuildGroupedSettings()
    {
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("s1", "Setting 1", "Group A"),
            ("s2", "Setting 2", "Group B"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        vm.GroupedSettings.Should().HaveCount(2);
        vm.GroupedSettings[0].Key.Should().Be("Group A");
        vm.GroupedSettings[1].Key.Should().Be("Group B");
    }

    [Fact]
    public async Task LoadSettingsAsync_ConcurrentLoadGuard_DoesNotLoadTwice()
    {
        var vm = CreateViewModel();
        var loadCount = 0;

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(() =>
            {
                loadCount++;
                return new ObservableCollection<SettingItemViewModel>();
            });

        await vm.LoadSettingsAsync();
        await vm.LoadSettingsAsync();

        loadCount.Should().Be(1);
    }

    [Fact]
    public async Task LoadSettingsAsync_SubscribesToEvents()
    {
        var vm = CreateViewModel();

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(new ObservableCollection<SettingItemViewModel>());

        await vm.LoadSettingsAsync();

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
        var vm = CreateViewModel();

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        Func<Task> action = () => vm.LoadSettingsAsync();

        await action.Should().ThrowAsync<InvalidOperationException>();
        vm.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadSettingsAsync_OnError_ResetsSettingsLoadedFlag_AllowsRetry()
    {
        var vm = CreateViewModel();
        var callCount = 0;

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
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

        Func<Task> firstCall = () => vm.LoadSettingsAsync();
        await firstCall.Should().ThrowAsync<InvalidOperationException>();

        await vm.LoadSettingsAsync();

        callCount.Should().Be(2);
    }

    [Fact]
    public async Task LoadSettingsAsync_UsesCorrectModuleId()
    {
        var vm = CreateViewModel();

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(new ObservableCollection<SettingItemViewModel>());

        await vm.LoadSettingsAsync();

        _mockSettingsLoadingService.Verify(
            s => s.LoadConfiguredSettingsAsync(
                TestableSettingsFeatureViewModel.TestModuleId,
                It.IsAny<string>(),
                vm),
            Times.Once);
    }

    [Fact]
    public async Task RefreshSettingsAsync_ClearsAndReloadsSettings()
    {
        var vm = CreateViewModel();
        var firstSettings = CreateSettingsCollection(("s1", "First", "G1"));
        var secondSettings = CreateSettingsCollection(("s2", "Second", "G2"));
        var callCount = 0;

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
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

        await vm.RefreshSettingsAsync();

        vm.Settings.Should().HaveCount(1);
        vm.Settings[0].Name.Should().Be("Second");
    }

    [Fact]
    public async Task RefreshSettingsAsync_PublishesSettingsRefreshedEvent()
    {
        // Regression: rebuilding the list creates fresh cards whose badge/technical-details
        // visibility is at default, so the refresh must re-publish SettingsRefreshedEvent for
        // the page to re-apply the View-menu state. Without it, Info badges silently disappear
        // after an apply-recommended refresh (and only return on a manual View-menu re-toggle).
        var vm = CreateViewModel();
        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(() => CreateSettingsCollection(("s1", "First", "G1")));

        await vm.LoadSettingsAsync();

        await vm.RefreshSettingsAsync();

        // RefreshSettingsAsync publishes exactly once (LoadSettingsAsync itself does not).
        _mockEventBus.Verify(e => e.Publish(It.IsAny<SettingsRefreshedEvent>()), Times.Once);
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_WhenSettingsNotLoaded_DoesNothing()
    {
        var vm = CreateViewModel();

        await vm.RefreshSettingStatesAsync();

        _mockSettingsLoadingService.Verify(
            s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()),
            Times.Never);
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_WhenSettingsLoaded_RefreshesStates()
    {
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(("s1", "Setting 1", "Group"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        _mockSettingsLoadingService
            .Setup(s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());

        await vm.LoadSettingsAsync();

        await vm.RefreshSettingStatesAsync();

        _mockSettingsLoadingService.Verify(
            s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()),
            Times.Once);
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_InBuilderMode_DoesNotReadSystemState()
    {
        // Builder mode authors un-applied state into the VMs; a navigation
        // refresh must not clobber it with live system values.
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(("s1", "Setting 1", "Group"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        _mockApplicationModeService
            .Setup(m => m.CurrentMode)
            .Returns(WinhanceMode.Builder);

        await vm.RefreshSettingStatesAsync();

        _mockSettingsLoadingService.Verify(
            s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()),
            Times.Never);
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_UpdatesSettingStatesFromResults()
    {
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(("s1", "Setting 1", "Group"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
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

        await vm.RefreshSettingStatesAsync();

        // The actual state update is done via the dispatcher mock, which runs synchronously
        _mockDispatcherService.Verify(
            d => d.RunOnUIThread(It.IsAny<Action>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void ApplySearchFilter_SetsSearchText()
    {
        var vm = CreateViewModel();

        vm.ApplySearchFilter("test");

        vm.SearchText.Should().Be("test");
    }

    [Fact]
    public void ApplySearchFilter_WithNull_SetsEmptyString()
    {
        var vm = CreateViewModel();

        vm.ApplySearchFilter(null!);

        vm.SearchText.Should().BeEmpty();
    }

    [Fact]
    public void ApplySearchFilter_WithEmptyString_SetsEmptyString()
    {
        var vm = CreateViewModel();
        vm.ApplySearchFilter("initial");

        vm.ApplySearchFilter(string.Empty);

        vm.SearchText.Should().BeEmpty();
    }

    [Fact]
    public async Task HasVisibleSettings_WhenSettingsExist_ReturnsTrue()
    {
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(("s1", "Setting 1", "Group"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        vm.HasVisibleSettings.Should().BeTrue();
        vm.IsVisibleInSearch.Should().BeTrue();
    }

    [Fact]
    public async Task SettingsCount_AfterLoading_ReturnsCorrectCount()
    {
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("s1", "Setting 1", "G1"),
            ("s2", "Setting 2", "G1"),
            ("s3", "Setting 3", "G2"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        vm.SettingsCount.Should().Be(3);
    }

    [Fact]
    public void GroupDescriptionText_WhenNoSettings_ReturnsEmptyString()
    {
        var vm = CreateViewModel();

        vm.GroupDescriptionText.Should().BeEmpty();
    }

    [Fact]
    public async Task GroupDescriptionText_WithGroupedSettings_ReturnsGroupNames()
    {
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("s1", "Setting 1", "Alpha"),
            ("s2", "Setting 2", "Beta"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        vm.GroupDescriptionText.Should().Contain("Alpha");
        vm.GroupDescriptionText.Should().Contain("Beta");
    }

    [Fact]
    public async Task GroupDescriptionText_WithMoreThan4Groups_AppendEllipsis()
    {
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("s1", "S1", "Group1"),
            ("s2", "S2", "Group2"),
            ("s3", "S3", "Group3"),
            ("s4", "S4", "Group4"),
            ("s5", "S5", "Group5"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        vm.GroupDescriptionText.Should().EndWith(", ...");
    }

    [Fact]
    public async Task GroupDescriptionText_WithExactly4Groups_DoesNotAppendEllipsis()
    {
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("s1", "S1", "Group1"),
            ("s2", "S2", "Group2"),
            ("s3", "S3", "Group3"),
            ("s4", "S4", "Group4"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        vm.GroupDescriptionText.Should().NotEndWith(", ...");
    }

    [Fact]
    public async Task GroupDescriptionText_WithEmptyGroupNames_ReturnsEmptyString()
    {
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("s1", "S1", ""),
            ("s2", "S2", ""));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        vm.GroupDescriptionText.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadSettingsAsync_GroupsSettingsByGroupName()
    {
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("s1", "Setting 1", "Alpha"),
            ("s2", "Setting 2", "Alpha"),
            ("s3", "Setting 3", "Beta"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        vm.GroupedSettings.Should().HaveCount(2);
        vm.GroupedSettings[0].Key.Should().Be("Alpha");
        vm.GroupedSettings[0].Should().HaveCount(2);
        vm.GroupedSettings[1].Key.Should().Be("Beta");
        vm.GroupedSettings[1].Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadSettingsAsync_SettingsWithEmptyGroupName_FallsBackToOtherGroup()
    {
        var vm = CreateViewModel();

        _mockLocalizationService.MissingKey("SettingGroup_Other");

        var settings = CreateSettingsCollection(
            ("s1", "Setting 1", ""));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        vm.GroupedSettings.Should().HaveCount(1);
        vm.GroupedSettings[0].Key.Should().Be("Other");
    }

    [Fact]
    public async Task LoadSettingsAsync_OtherGroupTranslationLooksLikeAMissMarker_IsStillUsed()
    {
        // A translation is allowed to be bracketed. The old code recognised a miss by the shape of
        // the string, so it threw this away and showed English instead.
        var vm = CreateViewModel();
        _mockLocalizationService.PresentKey("SettingGroup_Other", "[Sonstige]");

        var settings = CreateSettingsCollection(
            ("s1", "Setting 1", ""));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        vm.GroupedSettings.Should().HaveCount(1);
        vm.GroupedSettings[0].Key.Should().Be("[Sonstige]");
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var vm = CreateViewModel();

        var action = () => vm.Dispose();

        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var vm = CreateViewModel();

        var action = () =>
        {
            vm.Dispose();
            vm.Dispose();
        };

        action.Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_UnsubscribesFromEvents()
    {
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
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(new ObservableCollection<SettingItemViewModel>());

        // Trigger event subscriptions by loading
        await vm.LoadSettingsAsync();

        vm.Dispose();

        mockSettingToken.Verify(t => t.Dispose(), Times.Once);
        mockFilterToken.Verify(t => t.Dispose(), Times.Once);
        mockReviewToken.Verify(t => t.Dispose(), Times.Once);
    }

    [Fact]
    public async Task Dispose_ClearsSettingsCollection()
    {
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(("s1", "Setting 1", "Group"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();
        vm.Settings.Should().NotBeEmpty();

        vm.Dispose();

        vm.Settings.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadSettingsAsync_RaisesPropertyChangedForHasVisibleSettings()
    {
        var vm = CreateViewModel();
        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(new ObservableCollection<SettingItemViewModel>());

        await vm.LoadSettingsAsync();

        raisedProperties.Should().Contain(nameof(vm.HasVisibleSettings));
        raisedProperties.Should().Contain(nameof(vm.IsVisibleInSearch));
        raisedProperties.Should().Contain(nameof(vm.SettingsCount));
        raisedProperties.Should().Contain(nameof(vm.GroupDescriptionText));
    }

    [Fact]
    public async Task LoadSettingsAsync_RaisesPropertyChangedForIsLoading()
    {
        var vm = CreateViewModel();
        var raisedProperties = new List<string>();
        vm.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName!);

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(new ObservableCollection<SettingItemViewModel>());

        await vm.LoadSettingsAsync();

        raisedProperties.Should().Contain(nameof(vm.IsLoading));
    }

    // The InputType-population tests below guard #482 (blank page).

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
                It.IsAny<string>(),
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
        var settings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("sel1", "Selection Setting", inputType: InputType.Selection,
                selectedValue: 1)
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
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
        var settings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("num1", "Numeric Setting", inputType: InputType.NumericRange,
                numericValue: 30)
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
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
            CreateSettingItem("num1", "Numeric", inputType: InputType.NumericRange, numericValue: 45),
            CreateSettingItem("action1", "Action", inputType: InputType.Action)
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
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

    // The value-update tests below guard #483 (value corruption).

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
                It.IsAny<string>(),
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
                It.IsAny<string>(),
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
        var settings = new ObservableCollection<SettingItemViewModel>
        {
            // Units are what make the 1800 -> 30 conversion below happen. Without them the helper
            // leaves Setting.Numeric null and ConvertFromSystemUnits is an identity.
            CreateSettingItem("num1", "Numeric", inputType: InputType.NumericRange,
                numericValue: 10, numericUnits: "Minutes")
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
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

        vm.Settings[0].NumericValue.Should().Be(30);
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
                It.IsAny<string>(),
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

        vm.Settings[0].IsSelected.Should().BeTrue();
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
                It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

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

    [Fact]
    public async Task FullLifecycle_LoadThenRefreshWithSameValues_PreservesAllTypes()
    {
        var vm = CreateViewModel();
        var settings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("t1", "Toggle", isSelected: true),
            CreateSettingItem("sel1", "Selection", inputType: InputType.Selection, selectedValue: 1),
            CreateSettingItem("num1", "Numeric", inputType: InputType.NumericRange,
                numericValue: 42)
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

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
        var settings = new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("t1", "Toggle", isSelected: false),
            CreateSettingItem("sel1", "Selection", inputType: InputType.Selection, selectedValue: 0),
            CreateSettingItem("num1", "Numeric", inputType: InputType.NumericRange,
                numericValue: 10)
        };

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

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
                It.IsAny<string>(),
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
                It.IsAny<string>(),
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

    [Fact]
    public async Task LoadSettingsAsync_GroupedSettingsPreservesInsertionOrder()
    {
        var vm = CreateViewModel();
        var settings = CreateSettingsCollection(
            ("s1", "S1", "Zebra"),
            ("s2", "S2", "Alpha"),
            ("s3", "S3", "Middle"));

        _mockSettingsLoadingService
            .Setup(s => s.LoadConfiguredSettingsAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<ISettingsFeatureViewModel>()))
            .ReturnsAsync(settings);

        await vm.LoadSettingsAsync();

        vm.GroupedSettings[0].Key.Should().Be("Zebra");
        vm.GroupedSettings[1].Key.Should().Be("Alpha");
        vm.GroupedSettings[2].Key.Should().Be("Middle");
    }

    // A card is greyed only when its catalog says so, in EnabledWhen, and only while the setting that
    // names is outside the listed states. Nesting alone gates nothing. What this replaced compared the
    // parent's selected INDEX against zero, which greyed both Windows-theme sub-toggles on every stock
    // Windows 11 install because "Light Mode" is state 0.

    [Fact]
    public async Task LoadSettingsAsync_NestedChildWithNoDeclaredGate_IsNotDisabledByAnOffParent()
    {
        // THE REPORTED REGRESSION. The parent is off; the child declares no gate; the child stays live.
        var vm = CreateViewModel();
        SetupLoad(new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("parent", "Parent", isSelected: false, stateLabels: ToggleStates),
            CreateSettingItem("child", "Child", isSelected: true, uiParentId: "parent",
                stateLabels: ToggleStates),
        });

        await vm.LoadSettingsAsync();

        vm.Settings[1].ParentIsEnabled.Should().BeTrue();
        vm.Settings[1].EffectiveIsEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, true)]    // parent resolves to "Enabled" - the declared state
    [InlineData(false, false)]  // parent resolves to "Disabled" - outside it
    public async Task LoadSettingsAsync_DeclaredGate_FollowsTheParentsStateLabel(
        bool parentIsOn, bool expectedGate)
    {
        var vm = CreateViewModel();
        SetupLoad(new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("parent", "Parent", isSelected: parentIsOn, stateLabels: ToggleStates),
            CreateSettingItem("child", "Child", uiParentId: "parent", stateLabels: ToggleStates,
                enabledWhen: new EnabledWhen("parent", EnabledOnly)),
        });

        await vm.LoadSettingsAsync();

        vm.Settings[1].ParentIsEnabled.Should().Be(expectedGate);
    }

    [Fact]
    public async Task LoadSettingsAsync_DeclaredGate_ComparesTheLabelNotTheIndex()
    {
        // The theme shape: a Selection whose "usable" state is index 0. The old heuristic was literally
        // `index != 0`, so it disabled the child here. Keying on the label is what makes that
        // unreproducible.
        var vm = CreateViewModel();
        SetupLoad(new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("master", "Master", inputType: InputType.Selection, selectedValue: 0,
                stateLabels: LightDarkModes),
            CreateSettingItem("child", "Child", uiParentId: "master", stateLabels: ToggleStates,
                enabledWhen: new EnabledWhen("master", LightModeOnly)),
        });

        await vm.LoadSettingsAsync();

        vm.Settings[1].ParentIsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task LoadSettingsAsync_DeclaredGate_AcceptsAnyOfSeveralStates()
    {
        // gaming-performance-prefetch is usable in two of SysMain's three states, which a single
        // "the parent is on" bool could never express.
        var vm = CreateViewModel();
        SetupLoad(new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("service", "Service", inputType: InputType.Selection, selectedValue: 1,
                stateLabels: ServiceStartModes),
            CreateSettingItem("child", "Child", uiParentId: "service", stateLabels: ToggleStates,
                enabledWhen: new EnabledWhen("service", ServiceRunningModes)),
        });

        await vm.LoadSettingsAsync();

        vm.Settings[1].ParentIsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshSettingStatesAsync_RecomputesTheGate()
    {
        // THE STALE-GATE BUG. The navigation refresh re-read every card's state and never revisited the
        // gate, so a parent that changed elsewhere left its children holding the previous verdict until
        // the whole page was rebuilt.
        var vm = CreateViewModel();
        SetupLoad(new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("parent", "Parent", isSelected: true, stateLabels: ToggleStates),
            CreateSettingItem("child", "Child", uiParentId: "parent", stateLabels: ToggleStates,
                enabledWhen: new EnabledWhen("parent", EnabledOnly)),
        });
        _mockSettingsLoadingService
            .Setup(s => s.RefreshSettingStatesAsync(It.IsAny<IEnumerable<SettingItemViewModel>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>
            {
                ["parent"] = new SettingStateResult { Success = true, IsEnabled = false },
            });

        await vm.LoadSettingsAsync();
        vm.Settings[1].ParentIsEnabled.Should().BeTrue();

        await vm.RefreshSettingStatesAsync();

        vm.Settings[1].ParentIsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SettingAppliedEvent_RecomputesTheGate()
    {
        Action<SettingAppliedEvent>? handler = null;
        _mockEventBus
            .Setup(e => e.Subscribe(It.IsAny<Action<SettingAppliedEvent>>()))
            .Callback<Action<SettingAppliedEvent>>(h => handler = h)
            .Returns(new Mock<ISubscriptionToken>().Object);

        var vm = CreateViewModel();
        SetupLoad(new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("parent", "Parent", isSelected: true, stateLabels: ToggleStates),
            CreateSettingItem("child", "Child", uiParentId: "parent", stateLabels: ToggleStates,
                enabledWhen: new EnabledWhen("parent", EnabledOnly)),
        });

        await vm.LoadSettingsAsync();
        vm.Settings[1].ParentIsEnabled.Should().BeTrue();

        handler.Should().NotBeNull();
        handler!(new SettingAppliedEvent("parent", isEnabled: false));

        vm.Settings[1].ParentIsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task DeclaredGate_WhoseTargetIsNotLoadedHere_LeavesTheCardUsable()
    {
        // A gate is a positive claim. With nothing to read it, taking the control away would be a guess
        // in the direction that costs the user the setting.
        var vm = CreateViewModel();
        SetupLoad(new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("child", "Child", uiParentId: "elsewhere", stateLabels: ToggleStates,
                enabledWhen: new EnabledWhen("elsewhere", EnabledOnly)),
        });

        await vm.LoadSettingsAsync();

        vm.Settings[0].ParentIsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task DeclaredGate_WhoseTargetStateCannotBeNamed_LeavesTheCardUsable()
    {
        // Same rule for an unresolved reading: the -1 Custom sentinel means detection placed the parent
        // on no state at all, which says nothing about whether the child means anything.
        var vm = CreateViewModel();
        SetupLoad(new ObservableCollection<SettingItemViewModel>
        {
            CreateSettingItem("master", "Master", inputType: InputType.Selection, selectedValue: -1,
                stateLabels: LightDarkModes),
            CreateSettingItem("child", "Child", uiParentId: "master", stateLabels: ToggleStates,
                enabledWhen: new EnabledWhen("master", LightModeOnly)),
        });

        await vm.LoadSettingsAsync();

        vm.Settings[1].ParentIsEnabled.Should().BeTrue();
    }
}
