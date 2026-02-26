using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class SoundOptimizationsViewModelTests
{
    private readonly Mock<IDomainServiceRouter> _mockDomainServiceRouter;
    private readonly Mock<ISettingsLoadingService> _mockSettingsLoadingService;
    private readonly Mock<ILogService> _mockLogService;
    private readonly Mock<ILocalizationService> _mockLocalizationService;
    private readonly Mock<IDispatcherService> _mockDispatcherService;
    private readonly Mock<IEventBus> _mockEventBus;

    public SoundOptimizationsViewModelTests()
    {
        _mockDomainServiceRouter = new Mock<IDomainServiceRouter>();
        _mockSettingsLoadingService = new Mock<ISettingsLoadingService>();
        _mockLogService = new Mock<ILogService>();
        _mockLocalizationService = new Mock<ILocalizationService>();
        _mockDispatcherService = new Mock<IDispatcherService>();
        _mockEventBus = new Mock<IEventBus>();

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

    private SoundOptimizationsViewModel CreateViewModel()
    {
        return new SoundOptimizationsViewModel(
            _mockDomainServiceRouter.Object,
            _mockSettingsLoadingService.Object,
            _mockLogService.Object,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object,
            _mockEventBus.Object);
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
    public void ModuleId_ReturnsSound()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.ModuleId.Should().Be(FeatureIds.Sound);
    }

    [Fact]
    public void DisplayName_ReturnsLocalizedSoundName()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString("Feature_Sound_Name"))
            .Returns("Sound");

        var vm = CreateViewModel();

        // Act & Assert
        vm.DisplayName.Should().Be("Sound");
    }

    [Fact]
    public void ImplementsIOptimizationFeatureViewModel()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act & Assert
        vm.Should().BeAssignableTo<IOptimizationFeatureViewModel>();
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

    [Fact]
    public void ApplySearchFilter_SetsSearchText()
    {
        // Arrange
        var vm = CreateViewModel();

        // Act
        vm.ApplySearchFilter("sound");

        // Assert
        vm.SearchText.Should().Be("sound");
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
        var action = () => new SoundOptimizationsViewModel(
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
        var action = () => new SoundOptimizationsViewModel(
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
        var action = () => new SoundOptimizationsViewModel(
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
        var action = () => new SoundOptimizationsViewModel(
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
        var action = () => new SoundOptimizationsViewModel(
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
        var action = () => new SoundOptimizationsViewModel(
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
}
