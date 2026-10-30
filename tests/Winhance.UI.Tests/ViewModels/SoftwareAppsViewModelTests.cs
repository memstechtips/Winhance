using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.Models;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class SoftwareAppsViewModelTests
{
    private readonly Mock<IWindowsAppsService> _windowsAppsService = new();
    private readonly Mock<IAppInstallationService> _appInstallationService = new();
    private readonly Mock<IWindowsAppUninstallService> _windowsAppUninstallService = new();
    private readonly Mock<ITaskProgressService> _progressService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();
    private readonly Mock<IThemeService> _themeService = new();

    private readonly Mock<IExternalAppsService> _externalAppsService = new();
    private readonly Mock<IStagedAppInstaller> _stagedAppInstaller = new();
    private readonly Mock<ITaskProgressService> _extProgressService = new();
    private readonly Mock<ILogService> _extLogService = new();
    private readonly Mock<IDialogService> _extDialogService = new();
    private readonly Mock<ILocalizationService> _extLocalizationService = new();
    private readonly Mock<IDispatcherService> _extDispatcherService = new();

    private readonly Mock<ILocalizationService> _parentLocalizationService = new();
    private readonly Mock<ILogService> _parentLogService = new();
    private readonly Mock<IDialogService> _parentDialogService = new();
    private readonly Mock<IUserPreferencesService> _userPreferencesService = new();
    private readonly Mock<ISaveFilePicker> _saveFilePicker = new();
    private readonly Mock<IConfigReviewModeService> _configReviewModeService = new();
    private readonly Mock<IConfigReviewBadgeService> _configReviewBadgeService = new();
    private readonly Mock<IScheduledTaskService> _scheduledTaskService = new();
    private readonly Mock<IFileSystemService> _fileSystemService = new();
    private readonly Mock<IApplicationModeService> _applicationModeService = new();
    private readonly Mock<IInstalledAppsService> _installedAppsService = new();

    public SoftwareAppsViewModelTests()
    {
        _dispatcherService.Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _dispatcherService.Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(f => f().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);
        _dispatcherService.Setup(d => d.RunOnUIThreadWithContextAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(f => f().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        _extDispatcherService.Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _extDispatcherService.Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(f => f().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);
        _extDispatcherService.Setup(d => d.RunOnUIThreadWithContextAsync(It.IsAny<Func<Task>>()))
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
        _userPreferencesService.Setup(u => u.SetPreferenceAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(OperationResult.Succeeded());
    }

    private WindowsAppsViewModel CreateWindowsAppsVm() => new(
        _windowsAppsService.Object,
        _appInstallationService.Object,
        _windowsAppUninstallService.Object,
        _progressService.Object,
        _logService.Object,
        _dialogService.Object,
        _localizationService.Object,
        _dispatcherService.Object,
        _themeService.Object);

    private ExternalAppsViewModel CreateExternalAppsVm() => new(
        _externalAppsService.Object,
        _installedAppsService.Object,
        _appInstallationService.Object,
        _stagedAppInstaller.Object,
        new Mock<IInstallOptionsService>().Object,
        _extProgressService.Object,
        _extLogService.Object,
        _extDialogService.Object,
        _extLocalizationService.Object,
        _extDispatcherService.Object,
        _themeService.Object);

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
            _fileSystemService.Object,
            _applicationModeService.Object,
            _saveFilePicker.Object);
    }

    private void GivenBuilder(BuilderTarget target)
    {
        _applicationModeService.Setup(m => m.CurrentMode).Returns(WinhanceMode.Builder);
        _applicationModeService.Setup(m => m.CurrentBuilderTarget).Returns(target);
    }

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        var sut = CreateSut();

        sut.IsWindowsAppsTabSelected.Should().BeTrue();
        sut.IsExternalAppsTabSelected.Should().BeFalse();
        sut.SearchText.Should().BeEmpty();
    }

    [Fact]
    public async Task ExportList_WritesTheTabAsUtf8CsvWithAByteOrderMark()
    {
        _saveFilePicker.Setup(p => p.PickSavePath(It.IsAny<string>(), It.IsAny<string>(), "*.csv", It.IsAny<string>(), "csv"))
            .Returns(@"C:\apps.csv");
        string? written = null;
        _fileSystemService.Setup(f => f.WriteAllTextAsync(@"C:\apps.csv", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string contents, CancellationToken _) => written = contents)
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.ExportListCommand.ExecuteAsync(null);

        written.Should().StartWith("\uFEFF" + string.Join(',', sut.WindowsAppsColumns().Select(c => c.Header)) + "\r\n");
    }

    [Fact]
    public void ExportList_ExternalColumns_CarryPublisherAndPackageId()
    {
        var sut = CreateSut();
        var app = new AppItemViewModel(
            new ItemDefinition { Id = "firefox", Name = "Firefox", Description = "", GroupName = "Browsers", WinGetPackageId = ["Mozilla.Firefox"] },
            _localizationService.Object, _dispatcherService.Object, _themeService.Object)
        {
            Installed = new InstalledApp("firefox", "Firefox", InstalledAppKind.Program) { Publisher = "Mozilla" },
        };

        var values = sut.ExternalAppsColumns().ToDictionary(c => c.Header, c => c.Value(app));

        values["Publisher"].Should().Be("Mozilla");
        values["Package ID"].Should().Be("Mozilla.Firefox");
    }

    [Theory]
    [InlineData("firefox", false, true)]
    [InlineData(WinGetPackageItem.IdPrefix + "Mozilla.Firefox", false, false)]
    [InlineData(WinGetPackageItem.IdPrefix + "Mozilla.Firefox", true, true)]
    public void ExportList_LeavesOutSearchResultsThePcDoesNotHave(string id, bool installed, bool exported)
    {
        var app = new AppItemViewModel(
            new ItemDefinition { Id = id, Name = "Firefox", Description = "", GroupName = "Browsers", IsInstalled = installed },
            _localizationService.Object, _dispatcherService.Object, _themeService.Object);

        SoftwareAppsViewModel.IsExported(app).Should().Be(exported);
    }

    [Fact]
    public async Task ExportList_WhenThePickerIsCancelled_WritesNothing()
    {
        var sut = CreateSut();

        await sut.ExportListCommand.ExecuteAsync(null);

        _fileSystemService.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
            _fileSystemService.Object,
            _applicationModeService.Object,
            _saveFilePicker.Object);

        sut.WindowsAppsViewModel.Should().BeSameAs(winVm);
        sut.ExternalAppsViewModel.Should().BeSameAs(extVm);
    }

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

    [Fact]
    public async Task BuilderAutounattend_OnExternalTab_InstallIsTheAccentActionAndInstallsNothing()
    {
        GivenBuilder(BuilderTarget.Autounattend);
        var sut = CreateSut();
        sut.SelectExternalAppsTab();

        await sut.InstallSelectedItemsCommand.ExecuteAsync(null);

        sut.IsInstallAccent.Should().BeTrue();
        sut.IsRemoveAccent.Should().BeFalse();
        sut.CanInstallItems.Should().BeTrue();
        sut.CanRemoveItems.Should().BeFalse();
        _extDialogService.Verify(d => d.ShowWarningAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _extDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
    }

    [Fact]
    public async Task BuilderAutounattend_OnWindowsTab_RemoveIsTheAccentActionAndRemovesNothing()
    {
        GivenBuilder(BuilderTarget.Autounattend);
        var sut = CreateSut();

        await sut.RemoveSelectedItemsCommand.ExecuteAsync(null);

        sut.IsRemoveAccent.Should().BeTrue();
        sut.IsInstallAccent.Should().BeFalse();
        sut.CanRemoveItems.Should().BeTrue();
        sut.CanInstallItems.Should().BeFalse();
        _dialogService.Verify(d => d.ShowWarningAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BuilderConfig_NeitherActionIsAnAccentAndBothAreDisabled(bool onExternalTab)
    {
        GivenBuilder(BuilderTarget.Config);
        var sut = CreateSut();
        if (onExternalTab)
            sut.SelectExternalAppsTab();

        sut.IsInstallAccent.Should().BeFalse();
        sut.IsRemoveAccent.Should().BeFalse();
        sut.CanInstallItems.Should().BeFalse();
        sut.CanRemoveItems.Should().BeFalse();
    }

    [Fact]
    public void BuilderExternalAppsBannerText_ForAutounattend_SaysTheAppsInstallOnTheNewPc()
    {
        GivenBuilder(BuilderTarget.Autounattend);
        var sut = CreateSut();

        sut.BuilderExternalAppsBannerText.Should().Be("Checked apps will be installed for everyone on the new PC after setup.");
    }

    [Fact]
    public async Task SwitchingToAutounattend_OnExternalTab_KeepsTheTabAndRaisesTheAccent()
    {
        GivenBuilder(BuilderTarget.Config);
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
        sut.SelectExternalAppsTab();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        GivenBuilder(BuilderTarget.Autounattend);
        _applicationModeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        sut.IsExternalAppsTabSelected.Should().BeTrue();
        sut.IsInstallAccent.Should().BeTrue();
        sut.CanInstallItems.Should().BeTrue();
        changed.Should().Contain(new[]
        {
            nameof(sut.IsInstallAccent),
            nameof(sut.IsRemoveAccent),
            nameof(sut.BuilderExternalAppsBannerText),
        });
    }

    [Fact]
    public async Task InitializeAsync_LoadsViewPreference()
    {
        _userPreferencesService.Setup(u => u.GetPreference(UserPreferenceKeys.SoftwareAppsViewMode, "Card"))
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

        sut.ViewMode.Should().Be(SoftwareAppsViewMode.Card);
        sut.IsCardView.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_SubscribesOnlyOnce()
    {
        _userPreferencesService.Setup(u => u.GetPreference(UserPreferenceKeys.SoftwareAppsViewMode, "Card"))
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
        await sut.InitializeAsync();

        _userPreferencesService.Verify(u => u.GetPreference(UserPreferenceKeys.SoftwareAppsViewMode, "Card"), Times.Once);
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }

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

    [Fact]
    public async Task DefaultViewMode_IsCard_WhenNoPreferenceSaved()
    {
        _userPreferencesService.Setup(u => u.GetPreference(UserPreferenceKeys.SoftwareAppsViewMode, "Card"))
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

        sut.ViewMode.Should().Be(SoftwareAppsViewMode.Card);
        sut.IsCardView.Should().BeTrue();
        sut.IsTableView.Should().BeFalse();
        sut.IsCompactView.Should().BeFalse();
    }

    [Fact]
    public async Task ViewMode_LoadsTable_WhenPreferenceIsTable()
    {
        _userPreferencesService.Setup(u => u.GetPreference(UserPreferenceKeys.SoftwareAppsViewMode, "Card"))
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

        sut.ViewMode.Should().Be(SoftwareAppsViewMode.Table);
        sut.IsTableView.Should().BeTrue();
        sut.IsCardView.Should().BeFalse();
        sut.IsCompactView.Should().BeFalse();
    }

    [Fact]
    public async Task ViewMode_LoadsCompact_WhenPreferenceIsCompact()
    {
        _userPreferencesService.Setup(u => u.GetPreference(UserPreferenceKeys.SoftwareAppsViewMode, "Card"))
            .Returns("Compact");
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

        sut.ViewMode.Should().Be(SoftwareAppsViewMode.Compact);
        sut.IsCompactView.Should().BeTrue();
    }

    [Fact]
    public async Task ViewMode_FallsBackToCard_WhenPreferenceIsUnknown()
    {
        _userPreferencesService.Setup(u => u.GetPreference(UserPreferenceKeys.SoftwareAppsViewMode, "Card"))
            .Returns("Garbage");
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

        sut.ViewMode.Should().Be(SoftwareAppsViewMode.Card);
    }

    [Fact]
    public void SettingViewMode_PersistsPreference()
    {
        var sut = CreateSut();

        sut.ViewMode = SoftwareAppsViewMode.Compact;

        _userPreferencesService.Verify(
            u => u.SetPreferenceAsync(UserPreferenceKeys.SoftwareAppsViewMode, "Compact"),
            Times.Once);
    }

    [Fact]
    public void ChangingViewMode_RaisesPropertyChangedForAllConveniences()
    {
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sut.ViewMode = SoftwareAppsViewMode.Table;

        changed.Should().Contain(new[]
        {
            nameof(sut.ViewMode),
            nameof(sut.IsCardView),
            nameof(sut.IsTableView),
            nameof(sut.IsCompactView),
        });
    }

    private static InstalledApp WithUpdate(string name) => UpToDate(name) with
    {
        UpdateVia = PackageSources.WinGet,
        AvailableVersion = "2.0",
    };

    private static InstalledApp UpToDate(string name) => new($"reg:machine:{name}", name, InstalledAppKind.Program)
    {
        WinGetId = name,
    };

    private void GivenScan(params InstalledApp[] apps) =>
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new InstalledAppsScan(apps, true));

    private async Task<SoftwareAppsViewModel> CreateSutWithApps(params InstalledApp[] apps)
    {
        GivenScan(apps);
        var sut = CreateSut();
        await sut.InitializeExternalAppsAsync();
        await sut.ExternalAppsViewModel.RefreshInstalledAppsAsync();
        sut.SelectExternalAppsTab();
        return sut;
    }

    private static AppItemViewModel Row(SoftwareAppsViewModel sut, string name) =>
        sut.ExternalAppsViewModel.Items.Single(a => a.Name == name);

    private void GivenUpdatesRun()
    {
        _extProgressService.Setup(p => p.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(new CancellationTokenSource());
        _extProgressService.Setup(p => p.CreateDetailedProgress()).Returns(new Progress<TaskProgressDetail>());
        _installedAppsService.Setup(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));
    }

    [Fact]
    public async Task Showing_app_updates_opens_External_Software_with_the_updates_filter_on()
    {
        var sut = CreateSut();

        sut.ShowAppUpdates();
        await sut.InitializeExternalAppsAsync();

        sut.IsExternalAppsTabSelected.Should().BeTrue();
        sut.ExternalAppsViewModel.Filter.Should().Be(AppFilter.UpdatesAvailable);
        sut.IsFilterUpdates.Should().BeTrue();
    }

    [Fact]
    public void Entering_Builder_swaps_the_updates_filter_for_the_saved_one()
    {
        _userPreferencesService.Setup(u => u.GetPreference(UserPreferenceKeys.SoftwareAppsFilter, It.IsAny<string>()))
            .Returns(nameof(AppFilter.Installed));
        var sut = CreateSut();
        sut.ShowAppUpdates();

        GivenBuilder(BuilderTarget.Config);
        _applicationModeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        sut.ExternalAppsViewModel.Filter.Should().Be(AppFilter.Installed);
        sut.IsFilterUpdates.Should().BeFalse();
        sut.IsFilterInstalled.Should().BeTrue();
    }

    [Fact]
    public void A_mode_change_that_still_applies_to_this_pc_keeps_the_updates_filter()
    {
        var sut = CreateSut();
        sut.ShowAppUpdates();

        _applicationModeService.Raise(m => m.ModeChanged += null, EventArgs.Empty);

        sut.ExternalAppsViewModel.Filter.Should().Be(AppFilter.UpdatesAvailable);
    }

    [Fact]
    public void Choosing_Installed_filters_both_tabs_and_is_saved()
    {
        var sut = CreateSut();
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        sut.SetFilter(AppFilter.Installed);

        sut.WindowsAppsViewModel.Filter.Should().Be(AppFilter.Installed);
        sut.ExternalAppsViewModel.Filter.Should().Be(AppFilter.Installed);
        sut.IsFilterInstalled.Should().BeTrue();
        changed.Should().Contain(new[] { nameof(sut.IsFilterAll), nameof(sut.IsFilterInstalled), nameof(sut.IsFilterUpdates) });
        _userPreferencesService.Verify(u => u.SetPreferenceAsync(UserPreferenceKeys.SoftwareAppsFilter, "Installed"), Times.Once);
    }

    [Fact]
    public void Updates_available_filters_External_Software_only_and_is_not_saved()
    {
        var sut = CreateSut();
        sut.SetFilter(AppFilter.Installed);
        sut.SelectExternalAppsTab();

        sut.SetFilter(AppFilter.UpdatesAvailable);

        sut.ExternalAppsViewModel.Filter.Should().Be(AppFilter.UpdatesAvailable);
        sut.WindowsAppsViewModel.Filter.Should().Be(AppFilter.Installed);
        sut.IsFilterUpdates.Should().BeTrue();
        _userPreferencesService.Verify(u => u.SetPreferenceAsync(UserPreferenceKeys.SoftwareAppsFilter, "UpdatesAvailable"), Times.Never);

        sut.SelectWindowsAppsTab();

        sut.IsFilterInstalled.Should().BeTrue();
        sut.IsFilterUpdates.Should().BeFalse();
    }

    [Fact]
    public async Task The_saved_filter_is_restored_on_both_tabs()
    {
        _userPreferencesService.Setup(u => u.GetPreference(UserPreferenceKeys.SoftwareAppsFilter, It.IsAny<string>()))
            .Returns("Installed");
        var sut = CreateSut();

        await sut.InitializeExternalAppsAsync();

        sut.WindowsAppsViewModel.Filter.Should().Be(AppFilter.Installed);
        sut.ExternalAppsViewModel.Filter.Should().Be(AppFilter.Installed);
        sut.IsFilterInstalled.Should().BeTrue();
    }

    [Fact]
    public async Task An_installed_only_choice_saved_as_the_old_bool_carries_over()
    {
        _userPreferencesService.Setup(u => u.GetPreference("SoftwareAppsShowInstalledOnly", false)).Returns(true);
        _userPreferencesService.Setup(u => u.GetPreference(UserPreferenceKeys.SoftwareAppsFilter, It.IsAny<string>()))
            .Returns((string _, string fallback) => fallback);
        var sut = CreateSut();

        await sut.InitializeExternalAppsAsync();

        sut.WindowsAppsViewModel.Filter.Should().Be(AppFilter.Installed);
        sut.ExternalAppsViewModel.Filter.Should().Be(AppFilter.Installed);
    }

    [Fact]
    public async Task The_install_button_names_what_it_will_do_with_the_ticked_apps()
    {
        var sut = await CreateSutWithApps(WithUpdate("Git"), UpToDate("VLC"));
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        sut.PrimaryActionText.Should().Be("SoftwareApps_Button_InstallSelected");

        Row(sut, "VLC").IsSelected = true;
        sut.PrimaryActionText.Should().Be("SoftwareApps_Button_InstallSelected");
        changed.Should().Contain(nameof(sut.PrimaryActionText));

        Row(sut, "Git").IsSelected = true;
        sut.PrimaryActionText.Should().Be("SoftwareApps_Button_InstallOrUpdateSelected");

        Row(sut, "VLC").IsSelected = false;
        sut.PrimaryActionText.Should().Be("SoftwareApps_Button_UpdateSelected");
        sut.CanInstallItems.Should().BeTrue();
    }

    [Fact]
    public async Task The_install_button_label_follows_an_update_that_goes_away_after_a_scan()
    {
        var sut = await CreateSutWithApps(WithUpdate("Git"));
        Row(sut, "Git").IsSelected = true;
        sut.PrimaryActionText.Should().Be("SoftwareApps_Button_UpdateSelected");
        var changed = new List<string?>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        GivenScan(UpToDate("Git"));
        await sut.ExternalAppsViewModel.RefreshInstalledAppsAsync();

        sut.PrimaryActionText.Should().Be("SoftwareApps_Button_InstallSelected");
        changed.Should().Contain(nameof(sut.PrimaryActionText));
    }

    [Fact]
    public async Task On_Windows_Apps_the_install_button_keeps_the_install_label()
    {
        var sut = await CreateSutWithApps(WithUpdate("Git"));
        Row(sut, "Git").IsSelected = true;

        sut.SelectWindowsAppsTab();

        sut.PrimaryActionText.Should().Be("SoftwareApps_Button_InstallSelected");
    }

    [Fact]
    public async Task Install_on_External_Software_updates_a_ticked_app_that_has_an_update_without_asking()
    {
        var sut = await CreateSutWithApps(WithUpdate("Git"));
        GivenUpdatesRun();
        sut.CanInstallItems.Should().BeFalse();
        Row(sut, "Git").IsSelected = true;
        sut.CanInstallItems.Should().BeTrue();

        await sut.InstallSelectedItemsCommand.ExecuteAsync(null);

        _installedAppsService.Verify(s => s.UpdateAsync(
            It.Is<InstalledApp>(a => a.Name == "Git"), It.IsAny<CancellationToken>()), Times.Once);
        _extDialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
    }

    // Builder authors a file without touching this PC, so the button neither offers nor runs an update.
    [Fact]
    public async Task In_Builder_the_install_button_keeps_the_install_label_and_updates_nothing()
    {
        GivenBuilder(BuilderTarget.Config);
        var sut = await CreateSutWithApps(WithUpdate("Git"));
        GivenUpdatesRun();
        Row(sut, "Git").IsSelected = true;

        await sut.InstallSelectedItemsCommand.ExecuteAsync(null);

        sut.PrimaryActionText.Should().Be("SoftwareApps_Button_InstallSelected");
        sut.CanInstallItems.Should().BeFalse();
        _installedAppsService.Verify(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
