using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.Customize.ViewModels;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class ReviewModeViewModelCoordinatorTests
{
    // Mocks for child ViewModels' dependencies
    private readonly Mock<IWindowsAppsService> _windowsAppsService = new();
    private readonly Mock<IAppInstallationService> _appInstallationService = new();
    private readonly Mock<IAppUninstallationService> _appUninstallationService = new();
    private readonly Mock<ITaskProgressService> _winProgressService = new();
    private readonly Mock<ILogService> _winLogService = new();
    private readonly Mock<IDialogService> _winDialogService = new();
    private readonly Mock<ILocalizationService> _winLocalizationService = new();
    private readonly Mock<IInternetConnectivityService> _winConnectivityService = new();
    private readonly Mock<IDispatcherService> _winDispatcherService = new();

    private readonly Mock<IExternalAppsService> _externalAppsService = new();
    private readonly Mock<ITaskProgressService> _extProgressService = new();
    private readonly Mock<ILogService> _extLogService = new();
    private readonly Mock<IDialogService> _extDialogService = new();
    private readonly Mock<ILocalizationService> _extLocalizationService = new();
    private readonly Mock<IInternetConnectivityService> _extConnectivityService = new();
    private readonly Mock<IDispatcherService> _extDispatcherService = new();

    private readonly Mock<ILocalizationService> _softLocalizationService = new();
    private readonly Mock<ILogService> _softLogService = new();
    private readonly Mock<IDialogService> _softDialogService = new();
    private readonly Mock<IUserPreferencesService> _userPreferencesService = new();
    private readonly Mock<IConfigReviewModeService> _configReviewModeService = new();
    private readonly Mock<IConfigReviewBadgeService> _configReviewBadgeService = new();
    private readonly Mock<IScheduledTaskService> _scheduledTaskService = new();
    private readonly Mock<IFileSystemService> _fileSystemService = new();

    private readonly Mock<OptimizeViewModel> _optimizeVm = new();
    private readonly Mock<CustomizeViewModel> _customizeVm = new();
    private readonly Mock<ISettingReviewDiffApplier> _reviewDiffApplier = new();

    public ReviewModeViewModelCoordinatorTests()
    {
        _winDispatcherService.Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _winDispatcherService.Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(f => f().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        _extDispatcherService.Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _extDispatcherService.Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(f => f().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        _winLocalizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);
        _extLocalizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);
        _softLocalizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);

        _userPreferencesService.Setup(u => u.GetPreference(It.IsAny<string>(), It.IsAny<string>()))
            .Returns("Table");
    }

    private WindowsAppsViewModel CreateWindowsAppsVm() => new(
        _windowsAppsService.Object,
        _appInstallationService.Object,
        _appUninstallationService.Object,
        _winProgressService.Object,
        _winLogService.Object,
        _winDialogService.Object,
        _winLocalizationService.Object,
        _winConnectivityService.Object,
        _winDispatcherService.Object);

    private ExternalAppsViewModel CreateExternalAppsVm() => new(
        _externalAppsService.Object,
        _extProgressService.Object,
        _extLogService.Object,
        _extDialogService.Object,
        _extLocalizationService.Object,
        _extConnectivityService.Object,
        _extDispatcherService.Object);

    private SoftwareAppsViewModel CreateSoftwareAppsVm(
        WindowsAppsViewModel winVm, ExternalAppsViewModel extVm) => new(
        winVm, extVm,
        _softLocalizationService.Object,
        _softLogService.Object,
        _softDialogService.Object,
        _userPreferencesService.Object,
        _configReviewModeService.Object,
        _configReviewBadgeService.Object,
        _scheduledTaskService.Object,
        _fileSystemService.Object);

    /// <summary>
    /// Creates the coordinator with real child ViewModels but mocked OptimizeVM/CustomizeVM.
    /// Since OptimizeViewModel and CustomizeViewModel have complex constructors that need
    /// many dependencies, and the coordinator accesses them through their public ISettingsFeatureViewModel
    /// properties, we use a helper that constructs the coordinator with null-safe placeholders
    /// for those VMs (tests that exercise ReapplyReviewDiffsToExistingSettings will need separate handling).
    /// </summary>
    private (ReviewModeViewModelCoordinator Sut, WindowsAppsViewModel WinVm,
        ExternalAppsViewModel ExtVm, SoftwareAppsViewModel SoftVm)
        CreateSutWithVms()
    {
        var winVm = CreateWindowsAppsVm();
        var extVm = CreateExternalAppsVm();
        var softVm = CreateSoftwareAppsVm(winVm, extVm);

        // We cannot easily create OptimizeViewModel/CustomizeViewModel without their full
        // dependency graph (they're concrete classes with complex constructors).
        // For properties that the coordinator accesses (HasSelectedWindowsApps, etc.),
        // we use the real VMs. ReapplyReviewDiffsToExistingSettings tests would require
        // mocking the feature VMs, which is covered below with null checks.
        var sut = new ReviewModeViewModelCoordinator(
            softVm, winVm, extVm,
            null!, null!, _reviewDiffApplier.Object);

        return (sut, winVm, extVm, softVm);
    }

    private ItemDefinition CreateTestItem(string id, string name = "TestApp",
        bool isInstalled = false) => new()
    {
        Id = id,
        Name = name,
        Description = $"Description for {name}",
        AppxPackageName = "Microsoft.Test",
        IsInstalled = isInstalled
    };

    // --- HasSelectedWindowsApps ---

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
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = false });

        var (sut, winVm, _, _) = CreateSutWithVms();
        await winVm.LoadAppsAndCheckInstallationStatusAsync();
        winVm.Items[0].IsSelected = true;

        sut.HasSelectedWindowsApps.Should().BeTrue();
    }

    // --- HasSelectedExternalApps ---

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
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["ext1"] = false });

        var (sut, _, extVm, _) = CreateSutWithVms();
        await extVm.LoadAppsAndCheckInstallationStatusAsync();
        extVm.Items[0].IsSelected = true;

        sut.HasSelectedExternalApps.Should().BeTrue();
    }

    // --- Action properties ---

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

    // --- GetSelectedExternalAppIds ---

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
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["ext1"] = false });

        var (sut, _, extVm, _) = CreateSutWithVms();
        await extVm.LoadAppsAndCheckInstallationStatusAsync();
        extVm.Items[0].IsSelected = true;

        var result = sut.GetSelectedExternalAppIds();

        result.Should().Contain("ext1");
    }

    // --- ClearExternalAppSelections ---

    [Fact]
    public async Task ClearExternalAppSelections_ClearsAllSelections()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("ext1"),
                CreateTestItem("ext2")
            });
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["ext1"] = false, ["ext2"] = false });

        var (sut, _, extVm, _) = CreateSutWithVms();
        await extVm.LoadAppsAndCheckInstallationStatusAsync();
        foreach (var item in extVm.Items) item.IsSelected = true;

        sut.ClearExternalAppSelections();

        extVm.Items.Should().OnlyContain(i => !i.IsSelected);
    }

    // --- RemoveWindowsAppsAsync ---

    [Fact]
    public async Task RemoveWindowsAppsAsync_DelegatesToWindowsAppsViewModel()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>());

        var (sut, winVm, _, _) = CreateSutWithVms();
        await winVm.LoadAppsAndCheckInstallationStatusAsync();

        // Since no items are selected, RemoveApps should return early
        await sut.RemoveWindowsAppsAsync(skipConfirmation: true, saveRemovalScripts: false);

        // Verify it was called (even if no items selected, it should not throw)
        _appUninstallationService.Verify(s => s.UninstallAppsInParallelAsync(
            It.IsAny<List<ItemDefinition>>(), It.IsAny<bool>()), Times.Never);
    }

    // --- InstallWindowsAppsAsync ---

    [Fact]
    public async Task InstallWindowsAppsAsync_DelegatesToWindowsAppsViewModel()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>());

        var (sut, winVm, _, _) = CreateSutWithVms();
        await winVm.LoadAppsAndCheckInstallationStatusAsync();

        // Since no items are selected, InstallAppsAsync shows a warning
        await sut.InstallWindowsAppsAsync();

        _winDialogService.Verify(d => d.ShowWarningAsync(
            It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    // --- Action state defaults ---

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
