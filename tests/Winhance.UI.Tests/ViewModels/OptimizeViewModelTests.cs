using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class OptimizeViewModelTests
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly Mock<ILocalizationService> _mockLocalizationService;

    public OptimizeViewModelTests()
    {
        _mockLogService = new Mock<ILogService>();
        _mockLocalizationService = new Mock<ILocalizationService>();

        // Default localization setup: return the key itself as the localized string
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
    }

    private IEnumerable<IOptimizationFeatureViewModel> CreateFeatureViewModels()
    {
        // Create one mock per required FeatureId so InitializeSectionMappings
        // and GetFeatureByModuleId succeed.
        var moduleIds = new[]
        {
            FeatureIds.Privacy,
            FeatureIds.Power,
            FeatureIds.GamingPerformance,
            FeatureIds.Update,
            FeatureIds.Notifications,
            FeatureIds.Sound,
        };

        var viewModels = new List<IOptimizationFeatureViewModel>();
        foreach (var moduleId in moduleIds)
        {
            var mock = new Mock<IOptimizationFeatureViewModel>();
            mock.Setup(vm => vm.ModuleId).Returns(moduleId);
            mock.Setup(vm => vm.DisplayName).Returns($"{moduleId} Display");
            mock.Setup(vm => vm.SettingsCount).Returns(0);
            mock.Setup(vm => vm.Settings).Returns(new ObservableCollection<SettingItemViewModel>());
            viewModels.Add(mock.Object);
        }

        return viewModels;
    }

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange & Act
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Assert
        vm.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        // Arrange
        var featureViewModels = CreateFeatureViewModels();

        // Act
        var action = () => new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            featureViewModels);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Sections_ContainsSixEntries()
    {
        // Assert
        OptimizeViewModel.Sections.Should().HaveCount(6);
    }

    [Fact]
    public void Sections_ContainsPrivacySection()
    {
        // Assert
        OptimizeViewModel.Sections
            .Should().Contain(s => s.Key == "Privacy" && s.ModuleId == FeatureIds.Privacy);
    }

    [Fact]
    public void Sections_ContainsPowerSection()
    {
        // Assert
        OptimizeViewModel.Sections
            .Should().Contain(s => s.Key == "Power" && s.ModuleId == FeatureIds.Power);
    }

    [Fact]
    public void Sections_ContainsGamingSection()
    {
        // Assert
        OptimizeViewModel.Sections
            .Should().Contain(s => s.Key == "Gaming" && s.ModuleId == FeatureIds.GamingPerformance);
    }

    [Fact]
    public void Sections_ContainsUpdateSection()
    {
        // Assert
        OptimizeViewModel.Sections
            .Should().Contain(s => s.Key == "Update" && s.ModuleId == FeatureIds.Update);
    }

    [Fact]
    public void Sections_ContainsNotificationSection()
    {
        // Assert
        OptimizeViewModel.Sections
            .Should().Contain(s => s.Key == "Notification" && s.ModuleId == FeatureIds.Notifications);
    }

    [Fact]
    public void Sections_ContainsSoundSection()
    {
        // Assert
        OptimizeViewModel.Sections
            .Should().Contain(s => s.Key == "Sound" && s.ModuleId == FeatureIds.Sound);
    }

    [Fact]
    public void SoundViewModel_IsAssignedFromFeatureViewModels()
    {
        // Arrange & Act
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Assert
        vm.SoundViewModel.Should().NotBeNull();
        vm.SoundViewModel.ModuleId.Should().Be(FeatureIds.Sound);
    }

    [Fact]
    public void UpdateViewModel_IsAssignedFromFeatureViewModels()
    {
        // Arrange & Act
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Assert
        vm.UpdateViewModel.Should().NotBeNull();
        vm.UpdateViewModel.ModuleId.Should().Be(FeatureIds.Update);
    }

    [Fact]
    public void NotificationViewModel_IsAssignedFromFeatureViewModels()
    {
        // Arrange & Act
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Assert
        vm.NotificationViewModel.Should().NotBeNull();
        vm.NotificationViewModel.ModuleId.Should().Be(FeatureIds.Notifications);
    }

    [Fact]
    public void PrivacyViewModel_IsAssignedFromFeatureViewModels()
    {
        // Arrange & Act
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Assert
        vm.PrivacyViewModel.Should().NotBeNull();
        vm.PrivacyViewModel.ModuleId.Should().Be(FeatureIds.Privacy);
    }

    [Fact]
    public void PowerViewModel_IsAssignedFromFeatureViewModels()
    {
        // Arrange & Act
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Assert
        vm.PowerViewModel.Should().NotBeNull();
        vm.PowerViewModel.ModuleId.Should().Be(FeatureIds.Power);
    }

    [Fact]
    public void GamingViewModel_IsAssignedFromFeatureViewModels()
    {
        // Arrange & Act
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Assert
        vm.GamingViewModel.Should().NotBeNull();
        vm.GamingViewModel.ModuleId.Should().Be(FeatureIds.GamingPerformance);
    }

    [Fact]
    public void PageTitle_ReturnsLocalizedString()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString("Category_Optimize_Title"))
            .Returns("Optimize");

        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Act & Assert
        vm.PageTitle.Should().Be("Optimize");
    }

    [Fact]
    public void PageDescription_ReturnsLocalizedString()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString("Category_Optimize_StatusText"))
            .Returns("Optimize your system");

        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Act & Assert
        vm.PageDescription.Should().Be("Optimize your system");
    }

    [Fact]
    public void BreadcrumbRootText_ReturnsLocalizedTitleOrFallback()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString("Category_Optimize_Title"))
            .Returns("Optimize");

        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Act & Assert
        vm.BreadcrumbRootText.Should().Be("Optimize");
    }

    [Fact]
    public void CurrentSectionKey_DefaultsToOverview()
    {
        // Arrange & Act
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Assert
        vm.CurrentSectionKey.Should().Be("Overview");
    }

    [Fact]
    public void IsLoading_DefaultsToTrue()
    {
        // Arrange & Act
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Assert
        vm.IsLoading.Should().BeTrue();
    }

    [Fact]
    public void SearchText_DefaultsToEmptyString()
    {
        // Arrange & Act
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Assert
        vm.SearchText.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        // Arrange
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Act
        var action = () => vm.Dispose();

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void OnNavigatedFrom_ClearsSearchText()
    {
        // Arrange
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        vm.SearchText = "test";

        // Act
        vm.OnNavigatedFrom();

        // Assert
        vm.SearchText.Should().BeEmpty();
    }
}
