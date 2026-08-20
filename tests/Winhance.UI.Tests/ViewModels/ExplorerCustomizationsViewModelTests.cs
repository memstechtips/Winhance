using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Customize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class ExplorerCustomizationsViewModelTests
{
    private readonly Mock<ISettingsLoadingService> _mockSettingsLoadingService;
    private readonly Mock<ILogService> _mockLogService;
    private readonly Mock<ILocalizationService> _mockLocalizationService;
    private readonly Mock<IDispatcherService> _mockDispatcherService;
    private readonly Mock<IEventBus> _mockEventBus;
    private readonly Mock<IApplicationModeService> _mockApplicationModeService;

    public ExplorerCustomizationsViewModelTests()
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

        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(action => action());

        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(asyncAction => asyncAction());
    }

    private ExplorerCustomizationsViewModel CreateViewModel()
    {
        return new ExplorerCustomizationsViewModel(
            _mockSettingsLoadingService.Object,
            _mockLogService.Object,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object,
            _mockEventBus.Object,
            _mockApplicationModeService.Object);
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
    public void ModuleId_ReturnsExplorerCustomization()
    {
        var vm = CreateViewModel();

        vm.ModuleId.Should().Be(FeatureIds.ExplorerCustomization);
    }

    [Fact]
    public void DisplayName_ReturnsLocalizedExplorerName()
    {
        _mockLocalizationService
            .Setup(l => l.GetString("Feature_Explorer_Name"))
            .Returns("Explorer");

        var vm = CreateViewModel();

        vm.DisplayName.Should().Be("Explorer");
    }

    [Fact]
    public void Settings_DefaultsToEmptyCollection()
    {
        var vm = CreateViewModel();

        vm.Settings.Should().NotBeNull();
        vm.Settings.Should().BeEmpty();
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
    public void SettingsCount_WhenNoSettings_ReturnsZero()
    {
        var vm = CreateViewModel();

        vm.SettingsCount.Should().Be(0);
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
    public void ApplySearchFilter_SetsSearchText()
    {
        var vm = CreateViewModel();

        vm.ApplySearchFilter("explorer");

        vm.SearchText.Should().Be("explorer");
    }

    [Fact]
    public void ApplySearchFilter_WithNull_SetsEmptyString()
    {
        var vm = CreateViewModel();

        vm.ApplySearchFilter(null!);

        vm.SearchText.Should().BeEmpty();
    }

    [Fact]
    public void GroupedSettings_DefaultsToEmptyCollection()
    {
        var vm = CreateViewModel();

        vm.GroupedSettings.Should().NotBeNull();
        vm.GroupedSettings.Should().BeEmpty();
    }

    [Fact]
    public void GroupDescriptionText_WhenNoSettings_ReturnsEmptyString()
    {
        var vm = CreateViewModel();

        vm.GroupDescriptionText.Should().BeEmpty();
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
}
