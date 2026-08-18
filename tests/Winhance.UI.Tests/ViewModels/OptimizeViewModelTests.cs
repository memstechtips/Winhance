using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.ViewModels;

public class OptimizeViewModelTests
{
    private readonly Mock<ILogService> _mockLogService;
    private readonly Mock<ILocalizationService> _mockLocalizationService;
    private readonly Mock<IConfigReviewBadgeService> _mockBadgeService;
    private readonly Mock<IConfigReviewModeService> _mockReviewModeService;

    public OptimizeViewModelTests()
    {
        _mockLogService = new Mock<ILogService>();
        _mockLocalizationService = new Mock<ILocalizationService>();
        _mockBadgeService = new Mock<IConfigReviewBadgeService>();
        _mockReviewModeService = new Mock<IConfigReviewModeService>();

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockLocalizationService.MirrorTryGetString();
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
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels(),
            _mockBadgeService.Object,
            _mockReviewModeService.Object);

        vm.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithValidDependencies_DoesNotThrow()
    {
        var featureViewModels = CreateFeatureViewModels();

        var action = () => new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            featureViewModels,
            _mockBadgeService.Object,
            _mockReviewModeService.Object);

        action.Should().NotThrow();
    }

    [Fact]
    public void Sections_AreInTheOrderTheUserSees()
    {
        // The overview cards and the breadcrumb flyout render Sections as written, so this list is
        // the display order. The Contains tests below pass under any permutation - that is how the
        // Customize page's order was silently reversed by a refactor and only caught by eye.
        OptimizeViewModel.Sections.Select(s => s.Key)
            .Should().Equal("Privacy", "Power", "Gaming", "Update", "Notification", "Sound");
    }

    [Fact]
    public void Sections_ContainsSixEntries()
    {
        OptimizeViewModel.Sections.Should().HaveCount(6);
    }

    [Fact]
    public void Sections_ContainsPrivacySection()
    {
        OptimizeViewModel.Sections
            .Should().Contain(s => s.Key == "Privacy" && s.ModuleId == FeatureIds.Privacy);
    }

    [Fact]
    public void Sections_ContainsPowerSection()
    {
        OptimizeViewModel.Sections
            .Should().Contain(s => s.Key == "Power" && s.ModuleId == FeatureIds.Power);
    }

    [Fact]
    public void Sections_ContainsGamingSection()
    {
        OptimizeViewModel.Sections
            .Should().Contain(s => s.Key == "Gaming" && s.ModuleId == FeatureIds.GamingPerformance);
    }

    [Fact]
    public void Sections_ContainsUpdateSection()
    {
        OptimizeViewModel.Sections
            .Should().Contain(s => s.Key == "Update" && s.ModuleId == FeatureIds.Update);
    }

    [Fact]
    public void Sections_ContainsNotificationSection()
    {
        OptimizeViewModel.Sections
            .Should().Contain(s => s.Key == "Notification" && s.ModuleId == FeatureIds.Notifications);
    }

    [Fact]
    public void Sections_ContainsSoundSection()
    {
        OptimizeViewModel.Sections
            .Should().Contain(s => s.Key == "Sound" && s.ModuleId == FeatureIds.Sound);
    }

    [Fact]
    public void SoundViewModel_IsAssignedFromFeatureViewModels()
    {
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels(),
            _mockBadgeService.Object,
            _mockReviewModeService.Object);

        vm.SoundViewModel.Should().NotBeNull();
        vm.SoundViewModel.ModuleId.Should().Be(FeatureIds.Sound);
    }

    [Fact]
    public void UpdateViewModel_IsAssignedFromFeatureViewModels()
    {
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels(),
            _mockBadgeService.Object,
            _mockReviewModeService.Object);

        vm.UpdateViewModel.Should().NotBeNull();
        vm.UpdateViewModel.ModuleId.Should().Be(FeatureIds.Update);
    }

    [Fact]
    public void NotificationViewModel_IsAssignedFromFeatureViewModels()
    {
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels(),
            _mockBadgeService.Object,
            _mockReviewModeService.Object);

        vm.NotificationViewModel.Should().NotBeNull();
        vm.NotificationViewModel.ModuleId.Should().Be(FeatureIds.Notifications);
    }

    [Fact]
    public void PrivacyViewModel_IsAssignedFromFeatureViewModels()
    {
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels(),
            _mockBadgeService.Object,
            _mockReviewModeService.Object);

        vm.PrivacyViewModel.Should().NotBeNull();
        vm.PrivacyViewModel.ModuleId.Should().Be(FeatureIds.Privacy);
    }

    [Fact]
    public void PowerViewModel_IsAssignedFromFeatureViewModels()
    {
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels(),
            _mockBadgeService.Object,
            _mockReviewModeService.Object);

        vm.PowerViewModel.Should().NotBeNull();
        vm.PowerViewModel.ModuleId.Should().Be(FeatureIds.Power);
    }

    [Fact]
    public void GamingViewModel_IsAssignedFromFeatureViewModels()
    {
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels(),
            _mockBadgeService.Object,
            _mockReviewModeService.Object);

        vm.GamingViewModel.Should().NotBeNull();
        vm.GamingViewModel.ModuleId.Should().Be(FeatureIds.GamingPerformance);
    }

    [Fact]
    public void PageTitle_ReturnsLocalizedString()
    {
        _mockLocalizationService
            .Setup(l => l.GetString("Category_Optimize_Title"))
            .Returns("Optimize");

        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels(),
            _mockBadgeService.Object,
            _mockReviewModeService.Object);

        vm.PageTitle.Should().Be("Optimize");
    }

    [Fact]
    public void PageDescription_ReturnsLocalizedString()
    {
        _mockLocalizationService
            .Setup(l => l.GetString("Category_Optimize_StatusText"))
            .Returns("Optimize your system");

        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels(),
            _mockBadgeService.Object,
            _mockReviewModeService.Object);

        vm.PageDescription.Should().Be("Optimize your system");
    }

    [Fact]
    public void BreadcrumbRootText_ReturnsLocalizedTitleOrFallback()
    {
        _mockLocalizationService
            .Setup(l => l.GetString("Category_Optimize_Title"))
            .Returns("Optimize");

        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels(),
            _mockBadgeService.Object,
            _mockReviewModeService.Object);

        vm.BreadcrumbRootText.Should().Be("Optimize");
    }

    [Fact]
    public void CurrentSectionKey_DefaultsToOverview()
    {
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels(),
            _mockBadgeService.Object,
            _mockReviewModeService.Object);

        vm.CurrentSectionKey.Should().Be("Overview");
    }

    [Fact]
    public void IsLoading_DefaultsToTrue()
    {
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels(),
            _mockBadgeService.Object,
            _mockReviewModeService.Object);

        vm.IsLoading.Should().BeTrue();
    }

    [Fact]
    public void SearchText_DefaultsToEmptyString()
    {
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels(),
            _mockBadgeService.Object,
            _mockReviewModeService.Object);

        vm.SearchText.Should().BeEmpty();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels(),
            _mockBadgeService.Object,
            _mockReviewModeService.Object);

        var action = () => vm.Dispose();

        action.Should().NotThrow();
    }

    [Fact]
    public void OnNavigatedFrom_ClearsSearchText()
    {
        var vm = new OptimizeViewModel(
            _mockLogService.Object,
            _mockLocalizationService.Object,
            CreateFeatureViewModels(),
            _mockBadgeService.Object,
            _mockReviewModeService.Object);

        vm.SearchText = "test";

        vm.OnNavigatedFrom();

        vm.SearchText.Should().BeEmpty();
    }
}
