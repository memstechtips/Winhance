using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class SoftwareAppsViewModelTests
{
    private readonly Mock<IWindowsAppsService> _windowsAppsService = new();
    private readonly Mock<IAppInstallationService> _appInstallationService = new();
    private readonly Mock<IAppUninstallationService> _appUninstallationService = new();
    private readonly Mock<ITaskProgressService> _progressService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<IInternetConnectivityService> _connectivityService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();

    private readonly Mock<IExternalAppsService> _externalAppsService = new();
    private readonly Mock<ITaskProgressService> _extProgressService = new();
    private readonly Mock<ILogService> _extLogService = new();
    private readonly Mock<IDialogService> _extDialogService = new();
    private readonly Mock<ILocalizationService> _extLocalizationService = new();
    private readonly Mock<IInternetConnectivityService> _extConnectivityService = new();
    private readonly Mock<IDispatcherService> _extDispatcherService = new();

    private readonly Mock<ILocalizationService> _parentLocalizationService = new();
    private readonly Mock<ILogService> _parentLogService = new();
    private readonly Mock<IDialogService> _parentDialogService = new();
    private readonly Mock<IUserPreferencesService> _userPreferencesService = new();
    private readonly Mock<IConfigReviewModeService> _configReviewModeService = new();
    private readonly Mock<IConfigReviewBadgeService> _configReviewBadgeService = new();
    private readonly Mock<IScheduledTaskService> _scheduledTaskService = new();
    private readonly Mock<IFileSystemService> _fileSystemService = new();

    public SoftwareAppsViewModelTests()
    {
        // Set up dispatcher mocks to execute actions synchronously
        _dispatcherService.Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _dispatcherService.Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(f => f().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        _extDispatcherService.Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _extDispatcherService.Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(f => f().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        _parentLocalizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);
        _localizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);
        _extLocalizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);

        _userPreferencesService.Setup(u => u.GetPreference(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("Table");
    }

    private WindowsAppsViewModel CreateWindowsAppsVm() => new(
        _windowsAppsService.Object,
        _appInstallationService.Object,
        _appUninstallationService.Object,
        _progressService.Object,
        _logService.Object,
        _dialogService.Object,
        _localizationService.Object,
        _connectivityService.Object,
        _dispatcherService.Object);

    private ExternalAppsViewModel CreateExternalAppsVm() => new(
        _externalAppsService.Object,
        _extProgressService.Object,
        _extLogService.Object,
        _extDialogService.Object,
        _extLocalizationService.Object,
        _extConnectivityService.Object,
        _extDispatcherService.Object);

    private SoftwareAppsViewModel CreateSut()
    {
        var winVm = CreateWindowsAppsVm();
        var extVm = CreateExternalAppsVm();
        return new SoftwareAppsViewModel(
            winVm,
            extVm,
            _parentLocalizationService.Object,
            _parentLogService.Object,
            _parentDialogService.Object,
            _userPreferencesService.Object,
            _configReviewModeService.Object,
            _configReviewBadgeService.Object,
            _scheduledTaskService.Object,
            _fileSystemService.Object);
    }

    // --- Constructor / defaults ---

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        var sut = CreateSut();

        sut.IsWindowsAppsTabSelected.Should().BeTrue();
        sut.IsExternalAppsTabSelected.Should().BeFalse();
        sut.SearchText.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_AssignsChildViewModels()
    {
        var winVm = CreateWindowsAppsVm();
        var extVm = CreateExternalAppsVm();
        var sut = new SoftwareAppsViewModel(
            winVm, extVm,
            _parentLocalizationService.Object,
            _parentLogService.Object,
            _parentDialogService.Object,
            _userPreferencesService.Object,
            _configReviewModeService.Object,
            _configReviewBadgeService.Object,
            _scheduledTaskService.Object,
            _fileSystemService.Object);

        sut.WindowsAppsViewModel.Should().BeSameAs(winVm);
        sut.ExternalAppsViewModel.Should().BeSameAs(extVm);
    }

    // --- Tab selection ---

    [Fact]
    public void SelectWindowsAppsTab_SetsIsWindowsAppsTabSelected()
    {
        var sut = CreateSut();
        sut.IsExternalAppsTabSelected = true;

        sut.SelectWindowsAppsTabCommand.Execute(null);

        sut.IsWindowsAppsTabSelected.Should().BeTrue();
    }

    [Fact]
    public void SelectExternalAppsTab_SetsIsExternalAppsTabSelected()
    {
        var sut = CreateSut();

        sut.SelectExternalAppsTabCommand.Execute(null);

        sut.IsExternalAppsTabSelected.Should().BeTrue();
    }

    [Fact]
    public void OnIsWindowsAppsTabSelectedChanged_DeselectsExternalTab()
    {
        var sut = CreateSut();
        sut.IsExternalAppsTabSelected = true;

        sut.IsWindowsAppsTabSelected = true;

        sut.IsExternalAppsTabSelected.Should().BeFalse();
    }

    [Fact]
    public void OnIsExternalAppsTabSelectedChanged_DeselectsWindowsTab()
    {
        var sut = CreateSut();

        sut.IsExternalAppsTabSelected = true;

        sut.IsWindowsAppsTabSelected.Should().BeFalse();
    }

    // --- SearchText forwarding ---

    [Fact]
    public void SearchText_WhenWindowsTabSelected_ForwardsToWindowsAppsViewModel()
    {
        var sut = CreateSut();
        sut.IsWindowsAppsTabSelected = true;

        sut.SearchText = "test query";

        sut.WindowsAppsViewModel.SearchText.Should().Be("test query");
    }

    [Fact]
    public void SearchText_WhenExternalTabSelected_ForwardsToExternalAppsViewModel()
    {
        var sut = CreateSut();
        sut.IsExternalAppsTabSelected = true;

        sut.SearchText = "browser";

        sut.ExternalAppsViewModel.SearchText.Should().Be("browser");
    }

    // --- Localized text properties ---

    [Fact]
    public void PageTitle_ReturnsLocalizedString()
    {
        var sut = CreateSut();

        sut.PageTitle.Should().Be("Category_SoftwareApps_Title");
    }

    [Fact]
    public void PageDescription_ReturnsLocalizedString()
    {
        var sut = CreateSut();

        sut.PageDescription.Should().Be("Category_SoftwareApps_StatusText");
    }

    // --- IsLoading delegation ---

    [Fact]
    public void IsLoading_WhenWindowsTabSelected_DelegatesToWindowsAppsViewModel()
    {
        var sut = CreateSut();
        sut.IsWindowsAppsTabSelected = true;
        sut.WindowsAppsViewModel.IsLoading = true;

        sut.IsLoading.Should().BeTrue();
    }

    [Fact]
    public void IsLoading_WhenExternalTabSelected_DelegatesToExternalAppsViewModel()
    {
        var sut = CreateSut();
        sut.IsExternalAppsTabSelected = true;
        sut.ExternalAppsViewModel.IsLoading = true;

        sut.IsLoading.Should().BeTrue();
    }

    // --- Review mode action choices ---

    [Fact]
    public void IsWindowsAppsActionChosen_WhenInstallActionSet_ReturnsTrue()
    {
        var sut = CreateSut();

        sut.IsWindowsAppsInstallAction = true;

        sut.IsWindowsAppsActionChosen.Should().BeTrue();
    }

    [Fact]
    public void IsWindowsAppsActionChosen_WhenRemoveActionSet_ReturnsTrue()
    {
        var sut = CreateSut();

        sut.IsWindowsAppsRemoveAction = true;

        sut.IsWindowsAppsActionChosen.Should().BeTrue();
    }

    [Fact]
    public void IsWindowsAppsActionChosen_WhenNoActionSet_ReturnsFalse()
    {
        var sut = CreateSut();

        sut.IsWindowsAppsActionChosen.Should().BeFalse();
    }

    [Fact]
    public void IsExternalAppsActionChosen_WhenInstallActionSet_ReturnsTrue()
    {
        var sut = CreateSut();

        sut.IsExternalAppsInstallAction = true;

        sut.IsExternalAppsActionChosen.Should().BeTrue();
    }

    [Fact]
    public void IsWindowsAppsInstallAction_WhenSet_ClearsRemoveAction()
    {
        var sut = CreateSut();
        sut.IsWindowsAppsRemoveAction = true;

        sut.IsWindowsAppsInstallAction = true;

        sut.IsWindowsAppsRemoveAction.Should().BeFalse();
    }

    [Fact]
    public void IsWindowsAppsRemoveAction_WhenSet_ClearsInstallAction()
    {
        var sut = CreateSut();
        sut.IsWindowsAppsInstallAction = true;

        sut.IsWindowsAppsRemoveAction = true;

        sut.IsWindowsAppsInstallAction.Should().BeFalse();
    }

    [Fact]
    public void IsExternalAppsInstallAction_WhenSet_ClearsRemoveAction()
    {
        var sut = CreateSut();
        sut.IsExternalAppsRemoveAction = true;

        sut.IsExternalAppsInstallAction = true;

        sut.IsExternalAppsRemoveAction.Should().BeFalse();
    }

    [Fact]
    public void IsExternalAppsRemoveAction_WhenSet_ClearsInstallAction()
    {
        var sut = CreateSut();
        sut.IsExternalAppsInstallAction = true;

        sut.IsExternalAppsRemoveAction = true;

        sut.IsExternalAppsInstallAction.Should().BeFalse();
    }

    // --- CurrentInstallAction / CurrentRemoveAction routing ---

    [Fact]
    public void CurrentInstallAction_WhenWindowsTabSelected_RoutesToWindowsInstallAction()
    {
        var sut = CreateSut();
        sut.IsWindowsAppsTabSelected = true;

        sut.CurrentInstallAction = true;

        sut.IsWindowsAppsInstallAction.Should().BeTrue();
    }

    [Fact]
    public void CurrentInstallAction_WhenExternalTabSelected_RoutesToExternalInstallAction()
    {
        var sut = CreateSut();
        sut.IsExternalAppsTabSelected = true;

        sut.CurrentInstallAction = true;

        sut.IsExternalAppsInstallAction.Should().BeTrue();
    }

    [Fact]
    public void CurrentRemoveAction_WhenWindowsTabSelected_RoutesToWindowsRemoveAction()
    {
        var sut = CreateSut();
        sut.IsWindowsAppsTabSelected = true;

        sut.CurrentRemoveAction = true;

        sut.IsWindowsAppsRemoveAction.Should().BeTrue();
    }

    [Fact]
    public void CurrentRemoveAction_WhenExternalTabSelected_RoutesToExternalRemoveAction()
    {
        var sut = CreateSut();
        sut.IsExternalAppsTabSelected = true;

        sut.CurrentRemoveAction = true;

        sut.IsExternalAppsRemoveAction.Should().BeTrue();
    }

    // --- IsSoftwareAppsReviewed ---

    [Fact]
    public void IsSoftwareAppsReviewed_WhenNotInReviewMode_ReturnsFalse()
    {
        var sut = CreateSut();
        sut.IsInReviewMode = false;

        sut.IsSoftwareAppsReviewed.Should().BeFalse();
    }

    [Fact]
    public void IsSoftwareAppsReviewed_WhenInReviewMode_NoConfigItems_ReturnsTrue()
    {
        var sut = CreateSut();
        sut.IsInReviewMode = true;

        _configReviewBadgeService.Setup(s => s.IsFeatureInConfig(It.IsAny<string>()))
            .Returns(false);

        sut.IsSoftwareAppsReviewed.Should().BeTrue();
    }

    [Fact]
    public void IsSoftwareAppsReviewed_WhenInReviewMode_WindowsAppsInConfigNoAction_ReturnsFalse()
    {
        _configReviewBadgeService.Setup(s => s.IsFeatureInConfig("WindowsApps")).Returns(true);
        _configReviewBadgeService.Setup(s => s.IsFeatureInConfig("ExternalApps")).Returns(false);

        var sut = CreateSut();
        sut.IsInReviewMode = true;
        sut.WindowsAppsSelectedCount = 2;

        sut.IsSoftwareAppsReviewed.Should().BeFalse();
    }

    [Fact]
    public void IsSoftwareAppsReviewed_WhenInReviewMode_AllActionsChosen_ReturnsTrue()
    {
        _configReviewBadgeService.Setup(s => s.IsFeatureInConfig("WindowsApps")).Returns(true);
        _configReviewBadgeService.Setup(s => s.IsFeatureInConfig("ExternalApps")).Returns(true);

        var sut = CreateSut();
        sut.IsInReviewMode = true;
        sut.WindowsAppsSelectedCount = 2;
        sut.ExternalAppsSelectedCount = 1;
        sut.IsWindowsAppsInstallAction = true;
        sut.IsExternalAppsRemoveAction = true;

        sut.IsSoftwareAppsReviewed.Should().BeTrue();
    }

    // --- ReviewWindowsAppsBannerText ---

    [Fact]
    public void ReviewWindowsAppsBannerText_WhenInstallAction_ReturnsInstallText()
    {
        var sut = CreateSut();
        sut.IsWindowsAppsInstallAction = true;

        sut.ReviewWindowsAppsBannerText.Should().Be("Review_Mode_Action_Install");
    }

    [Fact]
    public void ReviewWindowsAppsBannerText_WhenRemoveAction_ReturnsRemoveText()
    {
        var sut = CreateSut();
        sut.IsWindowsAppsRemoveAction = true;

        sut.ReviewWindowsAppsBannerText.Should().Be("Review_Mode_Action_Remove");
    }

    [Fact]
    public void ReviewWindowsAppsBannerText_WhenNoAction_ReturnsSelectActionText()
    {
        var sut = CreateSut();

        sut.ReviewWindowsAppsBannerText.Should().Be("Review_Mode_Select_Action");
    }

    // --- ReviewExternalAppsBannerText ---

    [Fact]
    public void ReviewExternalAppsBannerText_WhenInstallAction_ReturnsInstallText()
    {
        var sut = CreateSut();
        sut.IsExternalAppsInstallAction = true;

        sut.ReviewExternalAppsBannerText.Should().Be("Review_Mode_Action_Install");
    }

    [Fact]
    public void ReviewExternalAppsBannerText_WhenRemoveAction_ReturnsRemoveText()
    {
        var sut = CreateSut();
        sut.IsExternalAppsRemoveAction = true;

        sut.ReviewExternalAppsBannerText.Should().Be("Review_Mode_Action_Remove");
    }

    // --- Button state management ---

    [Fact]
    public void CanInstallItems_InReviewMode_IsFalse()
    {
        var sut = CreateSut();
        sut.IsInReviewMode = true;

        // Trigger UpdateButtonStates via tab change
        sut.IsWindowsAppsTabSelected = true;

        sut.CanInstallItems.Should().BeFalse();
    }

    [Fact]
    public void CanRemoveItems_InReviewMode_IsFalse()
    {
        var sut = CreateSut();
        sut.IsInReviewMode = true;

        sut.IsWindowsAppsTabSelected = true;

        sut.CanRemoveItems.Should().BeFalse();
    }

    // --- InitializeAsync ---

    [Fact]
    public async Task InitializeAsync_LoadsViewPreference()
    {
        _userPreferencesService.Setup(u => u.GetPreference("SoftwareAppsViewMode", "Card"))
            .Returns("Card");
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>());
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>());

        var sut = CreateSut();

        await sut.InitializeAsync();

        sut.IsCardViewMode.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_SubscribesOnlyOnce()
    {
        _userPreferencesService.Setup(u => u.GetPreference("SoftwareAppsViewMode", "Card"))
            .Returns("Table");
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>());
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>());

        var sut = CreateSut();

        await sut.InitializeAsync();
        await sut.InitializeAsync(); // second call should not re-subscribe

        _userPreferencesService.Verify(u => u.GetPreference("SoftwareAppsViewMode", "Card"), Times.Once);
    }

    // --- Dispose ---

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }

    // --- SyncSoftwareAppsReviewedState ---

    [Fact]
    public void ActionChoice_SyncsSoftwareAppsReviewedState()
    {
        _configReviewBadgeService.Setup(s => s.IsFeatureInConfig(It.IsAny<string>())).Returns(false);

        var sut = CreateSut();
        sut.IsInReviewMode = true;
        sut.IsWindowsAppsInstallAction = true;

        _configReviewBadgeService.VerifySet(s => s.IsSoftwareAppsReviewed = It.IsAny<bool>(), Times.AtLeastOnce);
        _configReviewBadgeService.Verify(s => s.NotifyBadgeStateChanged(), Times.AtLeastOnce);
    }
}
