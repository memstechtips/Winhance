using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class PowerOptimizationsViewModelTests
{
    private readonly Mock<IDomainServiceRouter> _mockDomainServiceRouter;
    private readonly Mock<ISettingsLoadingService> _mockSettingsLoadingService;
    private readonly Mock<ILogService> _mockLogService;
    private readonly Mock<ILocalizationService> _mockLocalizationService;
    private readonly Mock<IDispatcherService> _mockDispatcherService;
    private readonly Mock<IDialogService> _mockDialogService;
    private readonly Mock<IEventBus> _mockEventBus;
    private readonly Mock<IPowerPlanComboBoxService> _mockPowerPlanComboBoxService;

    public PowerOptimizationsViewModelTests()
    {
        _mockDomainServiceRouter = new Mock<IDomainServiceRouter>();
        _mockSettingsLoadingService = new Mock<ISettingsLoadingService>();
        _mockLogService = new Mock<ILogService>();
        _mockLocalizationService = new Mock<ILocalizationService>();
        _mockDispatcherService = new Mock<IDispatcherService>();
        _mockDialogService = new Mock<IDialogService>();
        _mockEventBus = new Mock<IEventBus>();
        _mockPowerPlanComboBoxService = new Mock<IPowerPlanComboBoxService>();

        // Set up localization to return the key itself by default
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        // Set up dispatcher to execute actions synchronously for testing
        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(action => action());

        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Returns<Func<Task>>(asyncAction => asyncAction());
    }

    private PowerOptimizationsViewModel CreateViewModel()
    {
        return new PowerOptimizationsViewModel(
            _mockDomainServiceRouter.Object,
            _mockSettingsLoadingService.Object,
            _mockLogService.Object,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockEventBus.Object,
            _mockPowerPlanComboBoxService.Object);
    }

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
    public void ModuleId_ReturnsPower()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.ModuleId.Should().Be(FeatureIds.Power);
    }

    [Fact]
    public void DisplayName_ReturnsLocalizedPowerName()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString("Feature_Power_Name"))
            .Returns("Power");

        var vm = CreateViewModel();

        // Act & Assert
        vm.DisplayName.Should().Be("Power");
    }

    [Fact]
    public void DeletePowerPlanCommand_IsNotNull()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.DeletePowerPlanCommand.Should().NotBeNull();
    }

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
    public void SettingsCount_WhenNoSettings_ReturnsZero()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.SettingsCount.Should().Be(0);
    }

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
    public void ApplySearchFilter_SetsSearchText()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.ApplySearchFilter("power");

        // Assert
        vm.SearchText.Should().Be("power");
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
    public async Task DeletePowerPlanAsync_WithNullPlan_ReturnsWithoutAction()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        await vm.DeletePowerPlanAsync(null);

        // Assert - no dialog or service calls expected
        _mockDialogService.Verify(
            d => d.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task DeletePowerPlanAsync_WithActivePlan_ShowsInformationDialog()
    {
        // Arrange
        var vm = CreateViewModel();
        var activePlan = new PowerPlanComboBoxOption
        {
            DisplayName = "Active Plan",
            IsActive = true,
            ExistsOnSystem = true,
        };

        // Act
        await vm.DeletePowerPlanAsync(activePlan);

        // Assert
        _mockDialogService.Verify(
            d => d.ShowInformationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task DeletePowerPlanAsync_WithPlanNotOnSystem_ShowsInformationDialog()
    {
        // Arrange
        var vm = CreateViewModel();
        var offlinePlan = new PowerPlanComboBoxOption
        {
            DisplayName = "Offline Plan",
            IsActive = false,
            ExistsOnSystem = false,
            SystemPlan = null,
        };

        // Act
        await vm.DeletePowerPlanAsync(offlinePlan);

        // Assert
        _mockDialogService.Verify(
            d => d.ShowInformationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

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
    public void Constructor_WithNullDomainServiceRouter_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new PowerOptimizationsViewModel(
            null!,
            _mockSettingsLoadingService.Object,
            _mockLogService.Object,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockEventBus.Object,
            _mockPowerPlanComboBoxService.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("domainServiceRouter");
    }

    [Fact]
    public void Constructor_WithNullSettingsLoadingService_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new PowerOptimizationsViewModel(
            _mockDomainServiceRouter.Object,
            null!,
            _mockLogService.Object,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockEventBus.Object,
            _mockPowerPlanComboBoxService.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("settingsLoadingService");
    }

    [Fact]
    public void Constructor_WithNullLogService_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new PowerOptimizationsViewModel(
            _mockDomainServiceRouter.Object,
            _mockSettingsLoadingService.Object,
            null!,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockEventBus.Object,
            _mockPowerPlanComboBoxService.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logService");
    }

    [Fact]
    public void Constructor_WithNullLocalizationService_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new PowerOptimizationsViewModel(
            _mockDomainServiceRouter.Object,
            _mockSettingsLoadingService.Object,
            _mockLogService.Object,
            null!,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            _mockEventBus.Object,
            _mockPowerPlanComboBoxService.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("localizationService");
    }

    [Fact]
    public void Constructor_WithNullDispatcherService_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new PowerOptimizationsViewModel(
            _mockDomainServiceRouter.Object,
            _mockSettingsLoadingService.Object,
            _mockLogService.Object,
            _mockLocalizationService.Object,
            null!,
            _mockDialogService.Object,
            _mockEventBus.Object,
            _mockPowerPlanComboBoxService.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("dispatcherService");
    }

    [Fact]
    public void Constructor_WithNullEventBus_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new PowerOptimizationsViewModel(
            _mockDomainServiceRouter.Object,
            _mockSettingsLoadingService.Object,
            _mockLogService.Object,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object,
            _mockDialogService.Object,
            null!,
            _mockPowerPlanComboBoxService.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("eventBus");
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
    public void GroupDescriptionText_WhenNoSettings_ReturnsEmptyString()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.GroupDescriptionText.Should().BeEmpty();
    }
}
