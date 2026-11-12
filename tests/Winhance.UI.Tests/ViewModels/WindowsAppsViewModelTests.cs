using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.Models;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Xunit;
using Winhance.TestSupport;

namespace Winhance.UI.Tests.ViewModels;

public class WindowsAppsViewModelTests
{
    private readonly Mock<IWindowsAppsService> _windowsAppsService = new();
    private readonly Mock<IInstalledAppsService> _installedAppsService = new();
    private readonly Mock<IAppInstallationService> _appInstallationService = new();
    private readonly Mock<IWindowsAppUninstallService> _windowsAppUninstallService = new();
    private readonly Mock<ITaskProgressService> _progressService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();
    private readonly Mock<IThemeService> _themeService = new();

    public WindowsAppsViewModelTests()
    {
        _dispatcherService.Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _dispatcherService.Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(f => f().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);
        _dispatcherService.Setup(d => d.RunOnUIThreadWithContextAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(f => f().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        _localizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);
        _localizationService.MirrorTryGetString();
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool>()));
    }

    private WindowsAppsViewModel CreateSut(IAppIconResolver? iconResolver = null) => new(
        _windowsAppsService.Object,
        _installedAppsService.Object,
        _appInstallationService.Object,
        _windowsAppUninstallService.Object,
        _progressService.Object,
        _logService.Object,
        _dialogService.Object,
        _localizationService.Object,
        _dispatcherService.Object,
        _themeService.Object,
        iconResolver);

    private ItemDefinition CreateTestItem(string id, string name = "Test App",
        string[]? appxPackageName = null, bool isInstalled = false) => new()
    {
        Id = id,
        Name = name,
        Description = $"Description for {name}",
        AppxPackageName = appxPackageName ?? new[] { "Microsoft.Test" },
        IsInstalled = isInstalled
    };

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        var sut = CreateSut();

        sut.StatusText.Should().BeEmpty();
        sut.SearchText.Should().BeEmpty();
        sut.IsLoading.Should().BeFalse();
        sut.IsInitialized.Should().BeFalse();
        sut.IsTaskRunning.Should().BeFalse();
    }

    [Fact]
    public void Constructor_InitializesCollections()
    {
        var sut = CreateSut();

        sut.Items.Should().NotBeNull();
        sut.Items.Should().BeEmpty();
        sut.ItemsView.Should().NotBeNull();
    }

    [Fact]
    public void HasSelectedItems_WhenNoItems_ReturnsFalse()
    {
        var sut = CreateSut();

        sut.HasSelectedItems.Should().BeFalse();
    }

    [Fact]
    public void IsAllSelected_WhenNoItems_ReturnsFalse()
    {
        var sut = CreateSut();

        sut.IsAllSelected.Should().BeFalse();
    }

    [Fact]
    public void SectionAppsHeader_ReturnsLocalizedString()
    {
        var sut = CreateSut();

        sut.SectionAppsHeader.Should().Be("WindowsApps_Section_Apps");
    }

    [Fact]
    public void SectionCapabilitiesHeader_ReturnsLocalizedString()
    {
        var sut = CreateSut();

        sut.SectionCapabilitiesHeader.Should().Be("WindowsApps_Section_Capabilities");
    }

    [Fact]
    public void SectionOptionalFeaturesHeader_ReturnsLocalizedString()
    {
        var sut = CreateSut();

        sut.SectionOptionalFeaturesHeader.Should().Be("WindowsApps_Section_OptionalFeatures");
    }

    [Fact]
    public void SelectAllLabel_ReturnsLocalizedString()
    {
        var sut = CreateSut();

        sut.SelectAllLabel.Should().Be("Common_SelectAll");
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_LoadsItems()
    {
        var items = new List<ItemDefinition>
        {
            CreateTestItem("app1", "App One"),
            CreateTestItem("app2", "App Two")
        };
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(items);
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool>
            {
                ["app1"] = true,
                ["app2"] = false
            }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.Items.Should().HaveCount(2);
        sut.IsInitialized.Should().BeTrue();
        sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_MidLoad_ListsNoCardsUntilStatusCheckSortsThem()
    {
        IEnumerable<string>? listedMidLoad = null;
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition> { CreateTestItem("a", "Alpha"), CreateTestItem("z", "Zulu") });
        var sut = CreateSut();
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .Callback(() => listedMidLoad = sut.WindowsAppsFiltered.Select(a => a.Definition.Id).ToList())
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["a"] = false, ["z"] = true }));

        await sut.LoadAppsAndCheckInstallationStatusAsync();

        listedMidLoad.Should().BeEmpty();
        sut.WindowsAppsFiltered.Select(a => a.Definition.Id).Should().Equal("z", "a");
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_WhenAlreadyInitialized_SkipsLoading()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool>()));

        var sut = CreateSut();

        await sut.LoadAppsAndCheckInstallationStatusAsync();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        _windowsAppsService.Verify(s => s.GetAppsAsync(), Times.Once);
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_OnError_LogsErrorAndCompletes()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ThrowsAsync(new InvalidOperationException("Service error"));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        _logService.Verify(l => l.LogError(
            It.Is<string>(s => s.Contains("Error loading app definitions")),
            It.IsAny<Exception>(), It.IsAny<string>()), Times.Once);
        sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_SetsIsLoadingDuringOperation()
    {
        var sut = CreateSut();
        bool wasLoadingDuringGet = false;

        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(() =>
            {
                wasLoadingDuringGet = sut.IsLoading;
                return Enumerable.Empty<ItemDefinition>();
            });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool>()));

        await sut.LoadAppsAndCheckInstallationStatusAsync();

        wasLoadingDuringGet.Should().BeTrue();
        sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_ClearsTheLoaderBeforeIconsFinish()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition> { CreateTestItem("app1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = true }));
        var iconsDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Bounded so a load that awaits the icons fails this test instead of hanging the run.
        var icons = iconsDone.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var iconResolver = new Mock<IAppIconResolver>();
        iconResolver
            .Setup(r => r.ResolveBatchAsync(It.IsAny<IEnumerable<ItemDefinition>>(), true, It.IsAny<CancellationToken>()))
            .Returns(icons);

        var sut = CreateSut(iconResolver.Object);
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.IsContentReady.Should().BeTrue();
        icons.IsCompleted.Should().BeFalse();
        iconResolver.Verify(
            r => r.ResolveBatchAsync(It.IsAny<IEnumerable<ItemDefinition>>(), true, It.IsAny<CancellationToken>()),
            Times.Once);
        iconsDone.SetResult();
    }

    [Fact]
    public async Task LoadItemsAsync_DelegatesToLoadAppsAndCheckInstallationStatus()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool>()));

        var sut = CreateSut();
        await sut.LoadItemsAsync();

        sut.IsInitialized.Should().BeTrue();
    }

    [Fact]
    public async Task RecheckInstallationStatusAsync_WhenNotInitialized_SetsWaitMessage()
    {
        var sut = CreateSut();

        await sut.RecheckInstallationStatusAsync();

        sut.StatusText.Should().Be("Progress_WaitForInitialLoad");
    }

    [Fact]
    public async Task RecheckInstallationStatusAsync_WhenInitialized_InvalidatesCacheAndRefreshes()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool>()));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        await sut.RecheckInstallationStatusAsync();

        _installedAppsService.Verify(s => s.Invalidate(), Times.Once);
        sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task InstallAppsAsync_WhenNoItemsSelected_ShowsWarning()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = false }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        await sut.InstallAppsAsync();

        _dialogService.Verify(d => d.ShowWarningAsync(
            It.Is<string>(s => s.Contains("WindowsApps_NoSelection_Install")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAppsAsync_WhenNoItemsSelected_ShowsWarning()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = true }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        await sut.RemoveAppsAsync();

        _dialogService.Verify(d => d.ShowWarningAsync(
            It.Is<string>(s => s.Contains("WindowsApps_NoSelection_Remove")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RemoveAppsAsync_MarksWarnedItemsAndShowsOneLegend()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("installer", "App Installer") with { HasInstabilityWarning = true },
                CreateTestItem("plain", "Plain App"),
            });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["installer"] = true, ["plain"] = true }));
        ConfirmationRequest? shown = null;
        _dialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .Callback<ConfirmationRequest>(r => shown = r)
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        foreach (var item in sut.Items)
        {
            item.IsSelected = true;
        }

        await sut.RemoveAppsAsync();

        shown!.Items.Should().HaveCount(2);
        shown.ItemDetails!.Should().ContainSingle(i => i.HasInstabilityWarning).Which.Name.Should().Be("App Installer");
        shown.InstabilityLegend.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ClearSelections_DeselectsAllItems()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1", "App1"),
                CreateTestItem("app2", "App2")
            });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = true, ["app2"] = false }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        foreach (var item in sut.Items) item.IsSelected = true;
        sut.HasSelectedItems.Should().BeTrue();

        sut.ClearSelections();

        sut.Items.Should().OnlyContain(i => !i.IsSelected);
        sut.HasSelectedItems.Should().BeFalse();
    }

    [Fact]
    public async Task SelectedItemsChanged_RaisedWhenItemSelectionChanges()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = false }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        bool eventRaised = false;
        sut.SelectedItemsChanged += (_, _) => eventRaised = true;

        sut.Items[0].IsSelected = true;

        eventRaised.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleSelectAll_SelectsAllItems()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1"),
                CreateTestItem("app2")
            });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = false, ["app2"] = false }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.ToggleSelectAllCommand.Execute(null);

        sut.Items.Should().OnlyContain(i => i.IsSelected);
        sut.IsAllSelected.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleSelectAll_WhenAllSelected_DeselectsAll()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1"),
                CreateTestItem("app2")
            });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = false, ["app2"] = false }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.ToggleSelectAllCommand.Execute(null);
        sut.IsAllSelected.Should().BeTrue();

        sut.ToggleSelectAllCommand.Execute(null);

        sut.Items.Should().OnlyContain(i => !i.IsSelected);
    }

    [Fact]
    public async Task ToggleSelectAllInstalled_SelectsOnlyInstalledItems()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1", isInstalled: true),
                CreateTestItem("app2", isInstalled: false)
            });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = true, ["app2"] = false }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.ToggleSelectAllInstalledCommand.Execute(null);

        var installedItem = sut.Items.First(i => i.IsInstalled);
        var notInstalledItem = sut.Items.First(i => !i.IsInstalled);

        installedItem.IsSelected.Should().BeTrue();
        notInstalledItem.IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleSelectAllNotInstalled_SelectsOnlyNotInstalledItems()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1", isInstalled: true),
                CreateTestItem("app2", isInstalled: false)
            });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = true, ["app2"] = false }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.ToggleSelectAllNotInstalledCommand.Execute(null);

        var installedItem = sut.Items.First(i => i.IsInstalled);
        var notInstalledItem = sut.Items.First(i => !i.IsInstalled);

        installedItem.IsSelected.Should().BeFalse();
        notInstalledItem.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task Load_UpdatesInstalledStatus()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = true }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.Items[0].IsInstalled.Should().BeTrue();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_CleansUpEventSubscriptions()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = false }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.Dispose();

        sut.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task InstallAppsAsync_ServicingItems_ShareOneWindowAcrossBothKinds()
    {
        var feature = new ItemDefinition
        {
            Id = "feature-app",
            Name = "Windows Sandbox",
            Description = "Sandbox",
            OptionalFeatureName = "Containers-DisposableClientVM"
        };
        var capability = new ItemDefinition
        {
            Id = "cap-app",
            Name = "OpenSSH Client",
            Description = "OpenSSH",
            CapabilityName = "OpenSSH.Client"
        };

        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { feature, capability });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["feature-app"] = false, ["cap-app"] = false }));
        _dialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true, CheckboxChecked = false });
        _progressService.Setup(p => p.CreateDetailedProgress())
            .Returns(Mock.Of<IProgress<TaskProgressDetail>>());
        _appInstallationService
            .Setup(s => s.EnableServicingBatchAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<IProgress<TaskProgressDetail>?>()))
            .ReturnsAsync(OperationResult<bool>.DeferredSuccess(true, "handed off"));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        foreach (var item in sut.Items)
        {
            item.IsSelected = true;
        }

        await sut.InstallAppsAsync();

        _appInstallationService.Verify(s => s.EnableServicingBatchAsync(
            It.Is<IReadOnlyList<ItemDefinition>>(a => a.Count == 2
                && a.Any(x => x.Id == "feature-app") && a.Any(x => x.Id == "cap-app")),
            It.IsAny<IProgress<TaskProgressDetail>?>()), Times.Once);
        _appInstallationService.Verify(s => s.InstallAppAsync(
            It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<bool>()), Times.Never);
        sut.IsEnableHandedOffNoteVisible.Should().BeTrue();
        sut.EnableHandedOffNoteText.Should().Be("WindowsApps_EnableHandedOff");
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_Always_ContentReadyOnlyAfterLoadFinishes()
    {
        bool? readyMidLoad = null;
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition> { CreateTestItem("a", "Alpha") });
        var sut = CreateSut();
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .Callback(() => readyMidLoad = sut.IsContentReady)
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["a"] = true }));

        sut.IsContentReady.Should().BeFalse();

        await sut.LoadAppsAndCheckInstallationStatusAsync();

        readyMidLoad.Should().BeFalse();
        sut.IsContentReady.Should().BeTrue();
    }

    [Fact]
    public async Task RecheckInstallationStatusAsync_Always_HidesContentUntilItFinishes()
    {
        bool? readyMidRefresh = null;
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition> { CreateTestItem("a", "Alpha") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["a"] = true }));
        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .Callback(() => readyMidRefresh = sut.IsContentReady)
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["a"] = true }));

        await sut.RecheckInstallationStatusAsync();

        readyMidRefresh.Should().BeFalse();
        sut.IsContentReady.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_ListenerThrowsAtLoadEnd_DoesNotLeaveContentHidden()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition> { CreateTestItem("a", "Alpha") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["a"] = true }));
        var sut = CreateSut();
        sut.SelectedItemsChanged += (_, _) => throw new InvalidOperationException("listener failed");

        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.IsInitialized.Should().BeTrue();
        sut.IsContentReady.Should().BeTrue();
    }

    [Fact]
    public async Task Filter_Installed_ListsOnlyInstalledApps()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition> { CreateTestItem("a", "Alpha"), CreateTestItem("z", "Zulu") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["a"] = false, ["z"] = true }));
        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.Filter = AppFilter.Installed;

        sut.WindowsAppsFiltered.Select(a => a.Id).Should().Equal("z");

        sut.Filter = AppFilter.All;

        sut.WindowsAppsFiltered.Select(a => a.Id).Should().Equal("z", "a");
    }

    [Fact]
    public async Task RemoveApps_RemovalDeferredToNextStartup_ShowsNoteAndMarksNothingUninstalled()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition> { CreateTestItem("a", "Alpha") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["a"] = true }));
        _windowsAppUninstallService.Setup(s => s.UninstallAppsInParallelAsync(It.IsAny<List<ItemDefinition>>(), It.IsAny<bool>()))
            .ReturnsAsync(OperationResult<int>.DeferredSuccess(1, "Items will be removed at next startup"));
        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        sut.Items[0].IsSelected = true;

        await sut.RemoveApps(skipConfirmation: true);

        sut.IsRemovalDeferredNoteVisible.Should().BeTrue();
        sut.RemovalDeferredNoteText.Should().Be("WindowsApps_RemovalDeferred");
        sut.Items[0].IsInstalled.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveApps_RemovalRanNow_ShowsNoNote()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition> { CreateTestItem("a", "Alpha") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.Windows, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["a"] = true }));
        _windowsAppUninstallService.Setup(s => s.UninstallAppsInParallelAsync(It.IsAny<List<ItemDefinition>>(), It.IsAny<bool>()))
            .ReturnsAsync(OperationResult<int>.Succeeded(1));
        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        sut.Items[0].IsSelected = true;

        await sut.RemoveApps(skipConfirmation: true);

        sut.IsRemovalDeferredNoteVisible.Should().BeFalse();
    }

    private static InstalledApp Row(string catalogId, string installed, string? available, PackageSources via = PackageSources.WinGet) =>
        new($"key:{catalogId}", catalogId, InstalledAppKind.StoreApp)
        {
            CatalogId = catalogId,
            Version = installed,
            AvailableVersion = available,
            UpdateVia = available is null ? PackageSources.None : via,
        };

    private async Task<WindowsAppsViewModel> LoadedWithScanAsync(IEnumerable<ItemDefinition> items, params InstalledApp[] scan)
    {
        var definitions = items.ToList();
        _windowsAppsService.Setup(s => s.GetAppsAsync()).ReturnsAsync(definitions);
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan(scan, true, definitions.ToDictionary(d => d.Id, _ => true)));
        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        await sut.RefreshUpdatesAsync();
        return sut;
    }

    [Fact]
    public async Task InstallAppsAsync_WhenSomeInstallsFail_NamesThemInOneWarning()
    {
        var sut = await LoadedWithScanAsync([CreateTestItem("a", "Alpha"), CreateTestItem("b", "Beta"), CreateTestItem("c", "Gamma")]);
        _progressService.Setup(p => p.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(new CancellationTokenSource());
        _progressService.Setup(p => p.CreateDetailedProgress()).Returns(new Progress<TaskProgressDetail>());
        _localizationService.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns<string, object[]>((key, args) => $"{key}: {string.Join(", ", args)}");
        _dialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });
        _appInstallationService.Setup(s => s.InstallAppAsync(
                It.Is<ItemDefinition>(d => d.Id == "a"), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<bool>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));
        _appInstallationService.Setup(s => s.InstallAppAsync(
                It.Is<ItemDefinition>(d => d.Id != "a"), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<bool>()))
            .ReturnsAsync(OperationResult<bool>.Failed("WinGet could not install"));
        foreach (var item in sut.Items)
            item.IsSelected = true;

        await sut.InstallAppsAsync();

        _dialogService.Verify(d => d.ShowWarningAsync(
            It.Is<string>(m => m.Contains("WindowsApps_InstallFailed") && m.Contains("Beta") && m.Contains("Gamma") && !m.Contains("Alpha")),
            It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RefreshUpdatesAsync_WithWingetUpdate_MarksTheRowAndCountsIt()
    {
        var sut = await LoadedWithScanAsync(
            [CreateTestItem("a", "Alpha"), CreateTestItem("b", "Beta")],
            Row("a", "1.0.0", "2.0.0"), Row("b", "3.0.0", null));

        sut.Items.Single(i => i.Id == "a").HasUpdate.Should().BeTrue();
        sut.Items.Single(i => i.Id == "b").HasUpdate.Should().BeFalse();
        sut.UpdatesCount.Should().Be(1);
        sut.HasUpdates.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshUpdatesAsync_WithPlaceholderOrEqualVersions_NeverClaimsAnUpdate()
    {
        var sut = await LoadedWithScanAsync(
            [CreateTestItem("stub"), CreateTestItem("same")],
            Row("stub", "1.0.0.1", "26276.5"),
            Row("same", "2.0.0", "2.0.0"));

        sut.HasUpdates.Should().BeFalse();
        sut.UpdatesCount.Should().Be(0);
    }

    [Fact]
    public async Task RefreshUpdatesAsync_WithStoreCatalogUpdate_ClaimsAnUpdate()
    {
        var sut = await LoadedWithScanAsync(
            [CreateTestItem("store")],
            Row("store", "1.0.0", "2.0.0", PackageSources.MsStore));

        sut.Items.Single().HasUpdate.Should().BeTrue();
        sut.UpdatesCount.Should().Be(1);
    }

    [Fact]
    public async Task ToggleSelectAllWithUpdatesCommand_WhenExecuted_TicksOnlyRowsWithAnUpdateAndTogglesBack()
    {
        var sut = await LoadedWithScanAsync(
            [CreateTestItem("a"), CreateTestItem("b")],
            Row("a", "1.0.0", "2.0.0"), Row("b", "3.0.0", null));

        sut.ToggleSelectAllWithUpdatesCommand.Execute(null);

        sut.Items.Single(i => i.Id == "a").IsSelected.Should().BeTrue();
        sut.Items.Single(i => i.Id == "b").IsSelected.Should().BeFalse();
        sut.IsAllSelectedWithUpdates.Should().BeTrue();

        sut.ToggleSelectAllWithUpdatesCommand.Execute(null);

        sut.Items.Should().OnlyContain(i => !i.IsSelected);
        sut.IsAllSelectedWithUpdates.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleSelectAllWithUpdatesCommand_WhenNothingHasAnUpdate_StaysOff()
    {
        var sut = await LoadedWithScanAsync([CreateTestItem("a")], Row("a", "1.0.0", null));

        sut.ToggleSelectAllWithUpdatesCommand.Execute(null);

        sut.IsAllSelectedWithUpdates.Should().BeFalse();
        sut.Items.Should().OnlyContain(i => !i.IsSelected);
    }

    [Fact]
    public async Task RefreshUpdatesAsync_WithCapabilityOrOptionalFeature_DoesNotTreatItAsUpdatable()
    {
        var capability = CreateTestItem("cap") with { CapabilityName = "Some.Capability~~~~0.0.1.0", AppxPackageName = null };
        var sut = await LoadedWithScanAsync([capability], Row("cap", "1.0.0", "2.0.0"));

        sut.HasUpdates.Should().BeFalse();
        sut.Items.Single().HasUpdate.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshUpdatesAsync_WhenScanCouldNotReadUpdates_KeepsTheRowsAsTheyWere()
    {
        var sut = await LoadedWithScanAsync([CreateTestItem("a")], Row("a", "1.0.0", "2.0.0"));
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], false, new Dictionary<string, bool>()));

        var known = await sut.RefreshUpdatesAsync();

        known.Should().BeFalse();
        sut.UpdatesCount.Should().Be(1);
    }

    [Fact]
    public async Task UpdateAppsAsync_ForARow_RunsTheInstalledAppsUpdateWithItsScanRow()
    {
        var row = Row("a", "1.0.0", "2.0.0");
        var sut = await LoadedWithScanAsync([CreateTestItem("a")], row);
        _progressService.Setup(p => p.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(new CancellationTokenSource());
        _progressService.Setup(p => p.CreateDetailedProgress()).Returns(new Progress<TaskProgressDetail>());
        _installedAppsService.Setup(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        var run = await sut.UpdateAppsAsync([sut.Items.Single()]);

        run.Started.Should().BeTrue();
        _installedAppsService.Verify(s => s.UpdateAsync(row, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAppsAsync_WhileAnotherTaskRuns_DoesNotStart()
    {
        var sut = await LoadedWithScanAsync([CreateTestItem("a")], Row("a", "1.0.0", "2.0.0"));
        _progressService.Setup(p => p.IsTaskRunning).Returns(true);

        var run = await sut.UpdateAppsAsync([sut.Items.Single()]);

        run.Started.Should().BeFalse();
        _installedAppsService.Verify(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAppsAsync_WhenAskingToCloseOpenApps_AsksOnceAndClosesThemFirst()
    {
        var row = Row("a", "1.0.0", "2.0.0");
        var sut = await LoadedWithScanAsync([CreateTestItem("a")], row);
        _progressService.Setup(p => p.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(new CancellationTokenSource());
        _progressService.Setup(p => p.CreateDetailedProgress()).Returns(new Progress<TaskProgressDetail>());
        _installedAppsService.Setup(s => s.FindOpenAppsAsync(It.IsAny<IReadOnlyList<InstalledApp>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([row]);
        _dialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });
        _installedAppsService.Setup(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        await sut.UpdateAppsAsync([sut.Items.Single()], askToCloseOpenApps: true);

        _dialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Once);
        _installedAppsService.Verify(s => s.CloseAsync(It.Is<IReadOnlyList<InstalledApp>>(l => l.Single() == row), It.IsAny<CancellationToken>()), Times.Once);
        _installedAppsService.Verify(s => s.UpdateAsync(row, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAppsAsync_WhenUserLeavesAppsOpen_StillUpdatesThem()
    {
        var row = Row("a", "1.0.0", "2.0.0");
        var sut = await LoadedWithScanAsync([CreateTestItem("a")], row);
        _progressService.Setup(p => p.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(new CancellationTokenSource());
        _progressService.Setup(p => p.CreateDetailedProgress()).Returns(new Progress<TaskProgressDetail>());
        _installedAppsService.Setup(s => s.FindOpenAppsAsync(It.IsAny<IReadOnlyList<InstalledApp>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([row]);
        _dialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });
        _installedAppsService.Setup(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        await sut.UpdateAppsAsync([sut.Items.Single()], askToCloseOpenApps: true);

        _installedAppsService.Verify(s => s.CloseAsync(It.IsAny<IReadOnlyList<InstalledApp>>(), It.IsAny<CancellationToken>()), Times.Never);
        _installedAppsService.Verify(s => s.UpdateAsync(row, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAppsAsync_FromTheTray_NeverAsksToCloseApps()
    {
        var row = Row("a", "1.0.0", "2.0.0");
        var sut = await LoadedWithScanAsync([CreateTestItem("a")], row);
        _progressService.Setup(p => p.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(new CancellationTokenSource());
        _progressService.Setup(p => p.CreateDetailedProgress()).Returns(new Progress<TaskProgressDetail>());
        _installedAppsService.Setup(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        await sut.UpdateAppsAsync([sut.Items.Single()]);

        _installedAppsService.Verify(s => s.FindOpenAppsAsync(It.IsAny<IReadOnlyList<InstalledApp>>(), It.IsAny<CancellationToken>()), Times.Never);
        _installedAppsService.Verify(s => s.CloseAsync(It.IsAny<IReadOnlyList<InstalledApp>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
