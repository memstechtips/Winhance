using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Customize.Interfaces;
using Winhance.UI.Features.Customize.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class CustomizeViewModelTests
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly Mock<ILocalizationService> _mockLocalizationService;

    public CustomizeViewModelTests()
    {
        _mockLogService = new Mock<ILogService>();
        _mockLocalizationService = new Mock<ILocalizationService>();

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
    }

    private IEnumerable<ICustomizationFeatureViewModel> CreateFeatureViewModels()
    {
        var moduleIds = new[]
        {
            FeatureIds.ExplorerCustomization,
            FeatureIds.StartMenu,
            FeatureIds.Taskbar,
            FeatureIds.WindowsTheme,
        };

        var viewModels = new List<ICustomizationFeatureViewModel>();
        foreach (var moduleId in moduleIds)
        {
            var mock = new Mock<ICustomizationFeatureViewModel>();
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
        var vm = new CustomizeViewModel(
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
        var action = () => new CustomizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            featureViewModels);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Sections_ContainsFourEntries()
    {
        // Assert
        CustomizeViewModel.Sections.Should().HaveCount(4);
    }

    [Fact]
    public void Sections_ContainsExplorerSection()
    {
        // Assert
        CustomizeViewModel.Sections
            .Should().Contain(s => s.Key == "Explorer" && s.ModuleId == FeatureIds.ExplorerCustomization);
    }

    [Fact]
    public void Sections_ContainsStartMenuSection()
    {
        // Assert
        CustomizeViewModel.Sections
            .Should().Contain(s => s.Key == "StartMenu" && s.ModuleId == FeatureIds.StartMenu);
    }

    [Fact]
    public void Sections_ContainsTaskbarSection()
    {
        // Assert
        CustomizeViewModel.Sections
            .Should().Contain(s => s.Key == "Taskbar" && s.ModuleId == FeatureIds.Taskbar);
    }

    [Fact]
    public void Sections_ContainsWindowsThemeSection()
    {
        // Assert
        CustomizeViewModel.Sections
            .Should().Contain(s => s.Key == "WindowsTheme" && s.ModuleId == FeatureIds.WindowsTheme);
    }

    [Fact]
    public void ExplorerViewModel_IsAssignedFromFeatureViewModels()
    {
        // Arrange & Act
        var vm = new CustomizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Assert
        vm.ExplorerViewModel.Should().NotBeNull();
        vm.ExplorerViewModel.ModuleId.Should().Be(FeatureIds.ExplorerCustomization);
    }

    [Fact]
    public void StartMenuViewModel_IsAssignedFromFeatureViewModels()
    {
        // Arrange & Act
        var vm = new CustomizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Assert
        vm.StartMenuViewModel.Should().NotBeNull();
        vm.StartMenuViewModel.ModuleId.Should().Be(FeatureIds.StartMenu);
    }

    [Fact]
    public void TaskbarViewModel_IsAssignedFromFeatureViewModels()
    {
        // Arrange & Act
        var vm = new CustomizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Assert
        vm.TaskbarViewModel.Should().NotBeNull();
        vm.TaskbarViewModel.ModuleId.Should().Be(FeatureIds.Taskbar);
    }

    [Fact]
    public void WindowsThemeViewModel_IsAssignedFromFeatureViewModels()
    {
        // Arrange & Act
        var vm = new CustomizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Assert
        vm.WindowsThemeViewModel.Should().NotBeNull();
        vm.WindowsThemeViewModel.ModuleId.Should().Be(FeatureIds.WindowsTheme);
    }

    [Fact]
    public void PageTitle_ReturnsLocalizedString()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString("Category_Customize_Title"))
            .Returns("Customize");

        var vm = new CustomizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Act & Assert
        vm.PageTitle.Should().Be("Customize");
    }

    [Fact]
    public void PageDescription_ReturnsLocalizedString()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString("Category_Customize_StatusText"))
            .Returns("Customize your system");

        var vm = new CustomizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Act & Assert
        vm.PageDescription.Should().Be("Customize your system");
    }

    [Fact]
    public void BreadcrumbRootText_ReturnsLocalizedTitleOrFallback()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString("Category_Customize_Title"))
            .Returns("Customize");

        var vm = new CustomizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels());

        // Act & Assert
        vm.BreadcrumbRootText.Should().Be("Customize");
    }

    [Fact]
    public void CurrentSectionKey_DefaultsToOverview()
    {
        // Arrange & Act
        var vm = new CustomizeViewModel(
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
        var vm = new CustomizeViewModel(
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
        var vm = new CustomizeViewModel(
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
        var vm = new CustomizeViewModel(
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
        var vm = new CustomizeViewModel(
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
