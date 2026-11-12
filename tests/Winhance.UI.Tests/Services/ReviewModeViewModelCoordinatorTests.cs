using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.TestSupport;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Models;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Customize.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.UI.Features.SoftwareApps.Services;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class ReviewModeViewModelCoordinatorTests
{
    private static readonly string[] PendingAndRejected = ["pending", "rejected"];

    private readonly Mock<IWindowsAppsService> _windowsAppsService = new();
    private readonly Mock<IAppInstallationService> _appInstallationService = new();
    private readonly Mock<IWindowsAppUninstallService> _windowsAppUninstallService = new();
    private readonly Mock<ITaskProgressService> _winProgressService = new();
    private readonly Mock<ILogService> _winLogService = new();
    private readonly Mock<IDialogService> _winDialogService = new();
    private readonly Mock<ILocalizationService> _winLocalizationService = new();
    private readonly Mock<IDispatcherService> _winDispatcherService = new();
    private readonly Mock<IThemeService> _themeService = new();

    private readonly Mock<IExternalAppsService> _externalAppsService = new();
    private readonly Mock<IStagedAppInstaller> _stagedAppInstaller = new();
    private readonly Mock<ITaskProgressService> _extProgressService = new();
    private readonly Mock<ILogService> _extLogService = new();
    private readonly Mock<IDialogService> _extDialogService = new();
    private readonly Mock<ILocalizationService> _extLocalizationService = new();
    private readonly Mock<IDispatcherService> _extDispatcherService = new();

    private readonly Mock<ILocalizationService> _softLocalizationService = new();
    private readonly Mock<ILogService> _softLogService = new();
    private readonly Mock<IDialogService> _softDialogService = new();
    private readonly Mock<IUserPreferencesService> _userPreferencesService = new();
    private readonly Mock<IConfigReviewModeService> _configReviewModeService = new();
    private readonly Mock<IConfigReviewBadgeService> _configReviewBadgeService = new();
    private readonly Mock<IFileSystemService> _fileSystemService = new();
    private readonly Mock<IApplicationModeService> _applicationModeService = new();

    private readonly Mock<ISettingReviewDiffApplier> _reviewDiffApplier = new();
    private readonly Mock<IConfigReviewDiffService> _diffService = new();

    public ReviewModeViewModelCoordinatorTests()
    {
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool>()));
        _winDispatcherService.Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _winDispatcherService.Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(f => f().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);
        _winDispatcherService.Setup(d => d.RunOnUIThreadWithContextAsync(It.IsAny<Func<Task>>()))
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

        _winLocalizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);
        _extLocalizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);
        _softLocalizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);
        _softLocalizationService.MirrorTryGetString();

        _userPreferencesService.Setup(u => u.GetPreference(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("Table");
    }

    private readonly Mock<IInstalledAppsService> _installedAppsService = new();

    private WindowsAppsViewModel CreateWindowsAppsVm() => new(
        _windowsAppsService.Object,
        _installedAppsService.Object,
        _appInstallationService.Object,
        _windowsAppUninstallService.Object,
        _winProgressService.Object,
        _winLogService.Object,
        _winDialogService.Object,
        _winLocalizationService.Object,
        _winDispatcherService.Object,
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

    private SoftwareAppsViewModel CreateSoftwareAppsVm(
        WindowsAppsViewModel winVm, ExternalAppsViewModel extVm) => new(
        winVm, extVm,
        _softLocalizationService.Object,
        _softLogService.Object,
        _softDialogService.Object,
        _userPreferencesService.Object,
        _configReviewModeService.Object,
        _configReviewBadgeService.Object,
        _fileSystemService.Object,
        _applicationModeService.Object,
        new Mock<IFilePickerService>().Object,
        new AppUpdateMonitor(
            extVm,
            winVm,
            _userPreferencesService.Object,
            _softLocalizationService.Object,
            new Mock<IVersionService>().Object,
            _softLogService.Object));

    private (ReviewModeViewModelCoordinator Sut, WindowsAppsViewModel WinVm,
        ExternalAppsViewModel ExtVm, SoftwareAppsViewModel SoftVm)
        CreateSutWithVms(params SettingItemViewModel[] privacySettings)
    {
        var winVm = CreateWindowsAppsVm();
        var extVm = CreateExternalAppsVm();
        var softVm = CreateSoftwareAppsVm(winVm, extVm);

        var privacy = new Mock<ISettingsFeatureViewModel>();
        privacy.Setup(f => f.ModuleId).Returns(FeatureIds.Privacy);
        privacy.Setup(f => f.Settings).Returns(new ObservableCollection<SettingItemViewModel>(privacySettings));
        var optimize = new OptimizeViewModel(
            _softLogService.Object, _softLocalizationService.Object, [privacy.Object], _configReviewBadgeService.Object,
            _configReviewModeService.Object, new Mock<ICatalogSettingsRegistry>().Object,
            new Mock<ICatalogScopeProvider>().Object, _applicationModeService.Object);
        var customize = new CustomizeViewModel(
            _softLogService.Object, _softLocalizationService.Object, [], _configReviewBadgeService.Object,
            _configReviewModeService.Object, new Mock<ICatalogSettingsRegistry>().Object,
            new Mock<ICatalogScopeProvider>().Object, _applicationModeService.Object);

        var sut = new ReviewModeViewModelCoordinator(
            softVm, winVm, extVm, optimize, customize,
            _reviewDiffApplier.Object, _diffService.Object, _winDispatcherService.Object);

        return (sut, winVm, extVm, softVm);
    }

    private SettingItemViewModel CreateReviewedSetting(string id)
    {
        var setting = new Setting
        {
            Id = id,
            Display = new() { Name = TestKeys.Of(id), Description = TestKeys.Of("d") },
        };
        var vm = new SettingItemViewModel(
            new SettingItemViewModelConfig
            {
                Setting = setting,
                SettingId = id,
                Name = id,
                Description = "d",
                InputType = InputType.Toggle,
            },
            SettingWriteStrategies.Selector(
                new Mock<ISettingApplicationService>().Object, _softDialogService.Object,
                _softLocalizationService.Object, _softLogService.Object),
            _softLogService.Object,
            _winDispatcherService.Object,
            _softDialogService.Object,
            _softLocalizationService.Object);
        vm.IsInReviewMode = true;
        vm.HasReviewDiff = true;
        vm.HasReviewAction = true;
        return vm;
    }

    private ItemDefinition CreateTestItem(string id, string name = "TestApp",
        bool isInstalled = false) => new()
    {
        Id = id,
        Name = name,
        Description = $"Description for {name}",
        AppxPackageName = new[] { "Microsoft.Test" },
        IsInstalled = isInstalled
    };

    [Fact]
    public void HasSelectedWindowsApps_WhenNoItems_ReturnsFalse()
    {
        var (sut, _, _, _) = CreateSutWithVms();

        sut.HasSelectedWindowsApps.Should().BeFalse();
    }

    [Fact]
    public async Task HasSelectedWindowsApps_WhenItemSelected_ReturnsTrue()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = false }));

        var (sut, winVm, _, _) = CreateSutWithVms();
        await winVm.LoadAppsAndCheckInstallationStatusAsync();
        winVm.Items[0].IsSelected = true;

        sut.HasSelectedWindowsApps.Should().BeTrue();
    }

    [Fact]
    public void HasSelectedExternalApps_WhenNoItems_ReturnsFalse()
    {
        var (sut, _, _, _) = CreateSutWithVms();

        sut.HasSelectedExternalApps.Should().BeFalse();
    }

    [Fact]
    public async Task HasSelectedExternalApps_WhenItemSelected_ReturnsTrue()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("ext1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["ext1"] = false }));

        var (sut, _, extVm, _) = CreateSutWithVms();
        await extVm.LoadAppsAndCheckInstallationStatusAsync();
        extVm.Items[0].IsSelected = true;

        sut.HasSelectedExternalApps.Should().BeTrue();
    }

    [Fact]
    public void IsWindowsAppsInstallAction_DelegatesToSoftwareAppsViewModel()
    {
        var (sut, _, _, softVm) = CreateSutWithVms();

        softVm.IsWindowsAppsInstallAction = true;

        sut.IsWindowsAppsInstallAction.Should().BeTrue();
    }

    [Fact]
    public void IsWindowsAppsRemoveAction_DelegatesToSoftwareAppsViewModel()
    {
        var (sut, _, _, softVm) = CreateSutWithVms();

        softVm.IsWindowsAppsRemoveAction = true;

        sut.IsWindowsAppsRemoveAction.Should().BeTrue();
    }

    [Fact]
    public void IsExternalAppsInstallAction_DelegatesToSoftwareAppsViewModel()
    {
        var (sut, _, _, softVm) = CreateSutWithVms();

        softVm.IsExternalAppsInstallAction = true;

        sut.IsExternalAppsInstallAction.Should().BeTrue();
    }

    [Fact]
    public void IsExternalAppsRemoveAction_DelegatesToSoftwareAppsViewModel()
    {
        var (sut, _, _, softVm) = CreateSutWithVms();

        softVm.IsExternalAppsRemoveAction = true;

        sut.IsExternalAppsRemoveAction.Should().BeTrue();
    }

    [Fact]
    public void GetSelectedExternalAppIds_WhenNoItems_ReturnsEmptyList()
    {
        var (sut, _, _, _) = CreateSutWithVms();

        var result = sut.GetSelectedExternalAppIds();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSelectedExternalAppIds_WhenItemsSelected_ReturnsIds()
    {
        var item = CreateTestItem("ext1", "Firefox");
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { item });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["ext1"] = false }));

        var (sut, _, extVm, _) = CreateSutWithVms();
        await extVm.LoadAppsAndCheckInstallationStatusAsync();
        extVm.Items[0].IsSelected = true;

        var result = sut.GetSelectedExternalAppIds();

        result.Should().Contain("ext1");
    }

    [Fact]
    public async Task RemoveWindowsAppsAsync_DelegatesToWindowsAppsViewModel()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool>()));

        var (sut, winVm, _, _) = CreateSutWithVms();
        await winVm.LoadAppsAndCheckInstallationStatusAsync();

        // Since no items are selected, RemoveApps should return early
        await sut.RemoveWindowsAppsAsync(skipConfirmation: true, saveRemovalScripts: false);

        _windowsAppUninstallService.Verify(s => s.UninstallAppsInParallelAsync(
            It.IsAny<List<ItemDefinition>>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task InstallWindowsAppsAsync_DelegatesToWindowsAppsViewModel()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool>()));

        var (sut, winVm, _, _) = CreateSutWithVms();
        await winVm.LoadAppsAndCheckInstallationStatusAsync();

        // Since no items are selected, InstallAppsAsync shows a warning
        await sut.InstallWindowsAppsAsync();

        _winDialogService.Verify(d => d.ShowWarningAsync(
            It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public void AcceptAllRemaining_AcceptsWhatTheReviewStillHasOpen_AndLeavesDecisionsAlone()
    {
        var pending = CreateReviewedSetting("pending");
        var rejected = CreateReviewedSetting("rejected");
        rejected.IsReviewRejected = true;
        rejected.IsReviewActionRejected = true;
        var untouched = CreateReviewedSetting("untouched");
        _diffService.Setup(d => d.ApproveAllPending()).Returns(PendingAndRejected);
        var (sut, _, _, _) = CreateSutWithVms(pending, rejected, untouched);

        sut.AcceptAllRemaining();

        _diffService.Verify(d => d.ApproveAllPending(), Times.Once);
        pending.IsReviewApproved.Should().BeTrue();
        pending.IsReviewActionApproved.Should().BeTrue();
        rejected.IsReviewRejected.Should().BeTrue();
        rejected.IsReviewActionRejected.Should().BeTrue();
        untouched.IsReviewDecisionMade.Should().BeFalse();
    }

    [Fact]
    public async Task AcceptAllRemaining_GivesSelectedAppsTheImportDefaults()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync()).ReturnsAsync(new[] { CreateTestItem("app1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = true }));
        _externalAppsService.Setup(s => s.GetAppsAsync()).ReturnsAsync(new[] { CreateTestItem("ext1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["ext1"] = false }));
        _diffService.Setup(d => d.ApproveAllPending()).Returns(Array.Empty<string>());
        var (sut, winVm, extVm, softVm) = CreateSutWithVms();
        await winVm.LoadAppsAndCheckInstallationStatusAsync();
        await extVm.LoadAppsAndCheckInstallationStatusAsync();
        winVm.Items[0].IsSelected = true;
        extVm.Items[0].IsSelected = true;

        sut.AcceptAllRemaining();

        softVm.IsWindowsAppsRemoveAction.Should().BeTrue();
        softVm.IsExternalAppsInstallAction.Should().BeTrue();
    }

    [Fact]
    public async Task AcceptAllRemaining_KeepsAnAppActionAlreadyChosen_AndSkipsListsWithNothingSelected()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync()).ReturnsAsync(new[] { CreateTestItem("app1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = false }));
        _diffService.Setup(d => d.ApproveAllPending()).Returns(Array.Empty<string>());
        var (sut, winVm, _, softVm) = CreateSutWithVms();
        await winVm.LoadAppsAndCheckInstallationStatusAsync();
        winVm.Items[0].IsSelected = true;
        softVm.IsWindowsAppsInstallAction = true;

        sut.AcceptAllRemaining();

        softVm.IsWindowsAppsInstallAction.Should().BeTrue();
        softVm.IsWindowsAppsRemoveAction.Should().BeFalse();
        softVm.IsExternalAppsInstallAction.Should().BeFalse();
    }

    [Fact]
    public void ActionStates_DefaultToFalse()
    {
        var (sut, _, _, _) = CreateSutWithVms();

        sut.IsWindowsAppsInstallAction.Should().BeFalse();
        sut.IsWindowsAppsRemoveAction.Should().BeFalse();
        sut.IsExternalAppsInstallAction.Should().BeFalse();
        sut.IsExternalAppsRemoveAction.Should().BeFalse();
    }
}
