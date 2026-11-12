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

public class ExternalAppsViewModelTests
{
    private readonly Mock<IExternalAppsService> _externalAppsService = new();
    private readonly Mock<IInstalledAppsService> _installedAppsService = new();
    private readonly Mock<IAppInstallationService> _appInstallationService = new();
    private readonly Mock<IStagedAppInstaller> _stagedAppInstaller = new();
    private readonly Mock<IInstallOptionsService> _installOptions = new();
    private readonly Mock<ITaskProgressService> _progressService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();
    private readonly Mock<IThemeService> _themeService = new();

    public ExternalAppsViewModelTests()
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
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>())).ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool>()));
    }

    private ExternalAppsViewModel CreateSut() => new(
        _externalAppsService.Object,
        _installedAppsService.Object,
        _appInstallationService.Object,
        _stagedAppInstaller.Object,
        _installOptions.Object,
        _progressService.Object,
        _logService.Object,
        _dialogService.Object,
        _localizationService.Object,
        _dispatcherService.Object,
        _themeService.Object);

    private ItemDefinition CreateTestItem(string id, string name = "Test App",
        string group = "Browsers", bool isInstalled = false) => new()
    {
        Id = id,
        Name = name,
        Description = $"Description for {name}",
        GroupName = group,
        WinGetPackageId = new[] { $"Publisher.{name.Replace(" ", "")}" },
        IsInstalled = isInstalled
    };

    private void GivenCatalogForInstall(params ItemDefinition[] items)
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(items);
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool>()));
        _progressService.Setup(p => p.CreateDetailedProgress())
            .Returns(new Progress<TaskProgressDetail>());
        _progressService.Setup(p => p.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(new CancellationTokenSource());
        _localizationService.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns<string, object[]>((key, args) => $"{key}: {string.Join(", ", args)}");
    }

    private void GivenWinGetSearch(string query, params ItemDefinition[] results) =>
        _externalAppsService.Setup(s => s.SearchPackagesAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageSearchResult(results, SearchedChocolatey: true));

    private void GivenNewPcInstall(string id, bool succeeds) =>
        _appInstallationService.Setup(s => s.InstallOnNewPcAsync(
                It.Is<ItemDefinition>(d => d.Id == id), It.IsAny<IProgress<TaskProgressDetail>>()))
            .ReturnsAsync(succeeds ? OperationResult<bool>.Succeeded(true) : OperationResult<bool>.Failed("install failed"));

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
        sut.Categories.Should().NotBeNull();
        sut.Categories.Should().BeEmpty();
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
    public void SelectAllLabel_ReturnsLocalizedString()
    {
        var sut = CreateSut();

        sut.SelectAllLabel.Should().Be("Common_SelectAll");
    }

    [Fact]
    public void SelectAllInstalledLabel_ReturnsLocalizedString()
    {
        var sut = CreateSut();

        sut.SelectAllInstalledLabel.Should().Be("Common_SelectAll_Installed");
    }

    [Fact]
    public void SelectAllNotInstalledLabel_ReturnsLocalizedString()
    {
        var sut = CreateSut();

        sut.SelectAllNotInstalledLabel.Should().Be("Common_SelectAll_NotInstalled");
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_LoadsItems()
    {
        var items = new List<ItemDefinition>
        {
            CreateTestItem("app1", "Firefox", "Browsers"),
            CreateTestItem("app2", "7-Zip", "Compression")
        };
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(items);
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
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

    private static readonly string[] UnavailableIds = ["gone", "gone-installed"];

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_AppNoSourceCanInstall_IsHiddenUnlessThisPcHasIt()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync()).ReturnsAsync(new[]
        {
            CreateTestItem("gone", "Gone App"),
            CreateTestItem("gone-installed", "Kept App"),
            CreateTestItem("fine", "Fine App"),
        });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["gone-installed"] = true }));
        _externalAppsService.Setup(s => s.GetUnavailableAppIdsAsync(It.IsAny<IEnumerable<ItemDefinition>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UnavailableIds);

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.Items.Select(i => i.Id).Should().BeEquivalentTo("gone-installed", "fine");
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_WhenAlreadyInitialized_SkipsLoading()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool>()));

        var sut = CreateSut();

        await sut.LoadAppsAndCheckInstallationStatusAsync();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        _externalAppsService.Verify(s => s.GetAppsAsync(), Times.Once);
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_OnError_LogsErrorAndCompletes()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ThrowsAsync(new InvalidOperationException("Service error"));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        _logService.Verify(l => l.LogError(
            It.Is<string>(s => s.Contains("Error loading app definitions")),
            It.IsAny<Exception>(), It.IsAny<string>()), Times.Once);
        sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_BuildsCategories()
    {
        var items = new List<ItemDefinition>
        {
            CreateTestItem("app1", "Firefox", "Browsers"),
            CreateTestItem("app2", "Chrome", "Browsers"),
            CreateTestItem("app3", "7-Zip", "Compression")
        };
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(items);
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool>
            {
                ["app1"] = false,
                ["app2"] = false,
                ["app3"] = false
            }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.Categories.Should().HaveCount(2);
    }

    [Fact]
    public async Task Filter_Installed_HidesAppsThatAreNotInstalled()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1", "Firefox", "Browsers"),
                CreateTestItem("app2", "7-Zip", "Compression"),
            });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = true, ["app2"] = false }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        sut.Filter = AppFilter.Installed;

        sut.Categories.SelectMany(c => c.Apps).Select(a => a.Id).Should().Equal("app1");

        sut.Filter = AppFilter.All;

        sut.Categories.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_SortsAppsAlphabeticallyWithinCategories()
    {
        var items = new List<ItemDefinition>
        {
            CreateTestItem("app1", "Firefox", "Browsers"),
            CreateTestItem("app2", "Arc", "Browsers"),
            CreateTestItem("app3", "Chrome", "Browsers")
        };
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(items);
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool>
            {
                ["app1"] = false,
                ["app2"] = false,
                ["app3"] = false
            }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.Categories.Should().HaveCount(1);
        var category = sut.Categories[0];
        var names = category.Apps.Select(i => i.Name).ToList();
        names.Should().BeInAscendingOrder();
        names.Should().ContainInOrder("Arc", "Chrome", "Firefox");
    }

    [Fact]
    public async Task LoadItemsAsync_DelegatesToLoadAppsAndCheckInstallationStatus()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
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
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
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
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = false }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        await sut.InstallAppsAsync();

        _dialogService.Verify(d => d.ShowWarningAsync(
            It.Is<string>(s => s.Contains("ExternalApps_NoSelection_Install")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task InstallAppsAsync_WhenUserCancelsConfirmation_DoesNotInstall()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = false }));
        _dialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false, CheckboxChecked = false });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        sut.Items[0].IsSelected = true;

        await sut.InstallAppsAsync();

        _externalAppsService.Verify(s => s.InstallAppAsync(
            It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>>()), Times.Never);
    }

    [Fact]
    public async Task UninstallAppsAsync_WhenNoItemsSelected_ShowsWarning()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = true }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        await sut.UninstallAppsAsync();

        _dialogService.Verify(d => d.ShowWarningAsync(
            It.Is<string>(s => s.Contains("ExternalApps_NoSelection_Uninstall")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task ClearSelections_DeselectsAllItems()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1", "Firefox"),
                CreateTestItem("app2", "Chrome")
            });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
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
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
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
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1"),
                CreateTestItem("app2")
            });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = false, ["app2"] = false }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.ToggleSelectAllCommand.Execute(null);

        sut.Items.Should().OnlyContain(i => i.IsSelected);
        sut.IsAllSelected.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleSelectAllInstalled_SelectsOnlyInstalledItems()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1", isInstalled: true),
                CreateTestItem("app2", isInstalled: false)
            });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
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
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1", isInstalled: true),
                CreateTestItem("app2", isInstalled: false)
            });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
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
    public async Task InstallApps_WhenNoItemsSelected_ReturnsEarly()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = false }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        await sut.InstallApps(skipConfirmation: true);

        _externalAppsService.Verify(s => s.InstallAppAsync(
            It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>>()), Times.Never);
    }

    [Fact]
    public async Task InstallApps_InstallsTheSelectionThroughTheLiveRouteAndClearsIt()
    {
        GivenCatalogForInstall(CreateTestItem("app1", "Firefox"));
        _appInstallationService.Setup(s => s.InstallAppAsync(
                It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<bool>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));
        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        sut.Items[0].IsSelected = true;

        await sut.InstallApps(skipConfirmation: true);

        _appInstallationService.Verify(s => s.InstallAppAsync(
            It.Is<ItemDefinition>(d => d.Id == "app1"), It.IsAny<IProgress<TaskProgressDetail>>(), false), Times.Once);
        _appInstallationService.Verify(s => s.InstallOnNewPcAsync(
            It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>>()), Times.Never);
        sut.HasSelectedItems.Should().BeFalse();
    }

    [Fact]
    public async Task InstallApps_KeepsTheAppsThatFailedChecked()
    {
        GivenCatalogForInstall(CreateTestItem("app1", "Firefox"), CreateTestItem("app2", "Chrome"));
        _appInstallationService.Setup(s => s.InstallAppAsync(
                It.Is<ItemDefinition>(d => d.Id == "app1"), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<bool>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));
        _appInstallationService.Setup(s => s.InstallAppAsync(
                It.Is<ItemDefinition>(d => d.Id == "app2"), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<bool>()))
            .ReturnsAsync(OperationResult<bool>.Failed("install failed"));
        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        foreach (var item in sut.Items)
            item.IsSelected = true;

        await sut.InstallApps(skipConfirmation: true);

        sut.Items.Single(i => i.Id == "app1").IsSelected.Should().BeFalse();
        sut.Items.Single(i => i.Id == "app2").IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task InstallOnNewPcAsync_InstallsExactlyTheGivenIdsThroughTheNewPcRoute()
    {
        GivenCatalogForInstall(
            CreateTestItem("app1", "Firefox"),
            CreateTestItem("app2", "Chrome"),
            CreateTestItem("app3", "7-Zip"));
        GivenNewPcInstall("app1", succeeds: true);
        GivenNewPcInstall("app3", succeeds: true);
        var sut = CreateSut();

        await sut.InstallOnNewPcAsync(["app1", "app3"], null);

        _appInstallationService.Verify(s => s.InstallOnNewPcAsync(
            It.Is<ItemDefinition>(d => d.Id == "app1"), It.IsAny<IProgress<TaskProgressDetail>>()), Times.Once);
        _appInstallationService.Verify(s => s.InstallOnNewPcAsync(
            It.Is<ItemDefinition>(d => d.Id == "app3"), It.IsAny<IProgress<TaskProgressDetail>>()), Times.Once);
        _appInstallationService.Verify(s => s.InstallOnNewPcAsync(
            It.Is<ItemDefinition>(d => d.Id == "app2"), It.IsAny<IProgress<TaskProgressDetail>>()), Times.Never);
        _appInstallationService.Verify(s => s.InstallAppAsync(
            It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<bool>()), Times.Never);
        _stagedAppInstaller.Verify(s => s.FindAll(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InstallOnNewPcAsync_RunsTheOwnInstallersAndShowsTheDoneDialog()
    {
        GivenCatalogForInstall(CreateTestItem("app1", "Firefox"));
        GivenNewPcInstall("app1", succeeds: true);
        var reader = new StagedInstaller("Reader", @"C:\Custom\Reader");
        _stagedAppInstaller.Setup(s => s.FindAll(@"C:\Custom"))
            .Returns(new[] { reader });
        _stagedAppInstaller.Setup(s => s.InstallAsync(
                reader, It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        using var cancellation = new CancellationTokenSource();
        _progressService.Setup(p => p.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(cancellation);
        var sut = CreateSut();

        await sut.InstallOnNewPcAsync(["app1"], @"C:\Custom");

        _stagedAppInstaller.Verify(s => s.InstallAsync(
            reader, It.IsAny<IProgress<TaskProgressDetail>>(), cancellation.Token), Times.Once);
        _dialogService.Verify(d => d.ShowInformationAsync(
            "ExternalApps_NewPc_Done", "ExternalApps_NewPc_Title", It.IsAny<string>()), Times.Once);
        _dialogService.Verify(d => d.ShowWarningAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InstallOnNewPcAsync_WhenAnAppFails_NamesItAndTheUnknownIdAndKeepsItChecked()
    {
        GivenCatalogForInstall(CreateTestItem("app1", "Firefox"), CreateTestItem("app2", "Chrome"));
        GivenNewPcInstall("app1", succeeds: true);
        GivenNewPcInstall("app2", succeeds: false);
        var sut = CreateSut();

        await sut.InstallOnNewPcAsync(["app1", "app2", "retired-app"], null);

        _dialogService.Verify(d => d.ShowWarningAsync(
            It.Is<string>(m => m.StartsWith("ExternalApps_NewPc_Failed: 1, 3,")
                && m.Contains("Chrome") && m.Contains("retired-app") && !m.Contains("Firefox")),
            "ExternalApps_NewPc_Title",
            It.IsAny<string>()), Times.Once);
        sut.Items.Single(i => i.Id == "app1").IsSelected.Should().BeFalse();
        sut.Items.Single(i => i.Id == "app2").IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task SearchPackagesAsync_ShowsEachManagersResultsInTheirOwnCategoryAfterTheCatalog()
    {
        GivenCatalogForInstall(CreateTestItem("app1", "Firefox", "Browsers"));
        GivenWinGetSearch("player", ChocoPackageItem.Create("mpv"), WinGetPackageItem.Create("VideoLAN.VLC", "VLC media player"));
        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        await sut.SearchPackagesAsync("player", CancellationToken.None);

        sut.Items.Should().Contain(i => i.Id == "winget-VideoLAN.VLC").And.Contain(i => i.Id == "choco-mpv");
        sut.Categories.Select(c => c.GroupName).Should().Equal("Browsers", WinGetPackageItem.GroupName, ChocoPackageItem.GroupName);
        sut.IsSearchingPackages.Should().BeFalse();
        sut.IsChocolateyNoteVisible.Should().BeFalse();
    }

    [Fact]
    public async Task SearchPackagesAsync_SaysSoWhenChocolateyCouldNotBeSearched()
    {
        GivenCatalogForInstall(CreateTestItem("app1", "Firefox"));
        _externalAppsService.Setup(s => s.SearchPackagesAsync("player", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PackageSearchResult([], SearchedChocolatey: false));
        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        await sut.SearchPackagesAsync("player", CancellationToken.None);

        sut.IsChocolateyNoteVisible.Should().BeTrue();
    }

    [Fact]
    public async Task InstallOnNewPcAsync_InstallsAChocolateyPickSavedByItsId()
    {
        GivenCatalogForInstall(CreateTestItem("app1", "Firefox"));
        GivenNewPcInstall("choco-mpv", succeeds: true);
        var sut = CreateSut();

        await sut.InstallOnNewPcAsync(["choco-mpv"], null);

        _appInstallationService.Verify(s => s.InstallOnNewPcAsync(
            It.Is<ItemDefinition>(d => d.ChocoPackageId == "mpv"), It.IsAny<IProgress<TaskProgressDetail>>()), Times.Once);
    }

    [Fact]
    public async Task SearchText_TooShortForWinGet_DropsUntickedResultsAndKeepsTickedOnesVisible()
    {
        GivenCatalogForInstall(CreateTestItem("app1", "Firefox"));
        GivenWinGetSearch("player",
            WinGetPackageItem.Create("VideoLAN.VLC", "VLC media player"),
            WinGetPackageItem.Create("Contoso.Player", "Contoso Player"));
        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        await sut.SearchPackagesAsync("player", CancellationToken.None);
        sut.Items.Single(i => i.Id == "winget-VideoLAN.VLC").IsSelected = true;

        sut.SearchText = "zz";

        sut.Items.Select(i => i.Id).Should().BeEquivalentTo("app1", "winget-VideoLAN.VLC");
        sut.Categories.Should().ContainSingle().Which.GroupName.Should().Be(WinGetPackageItem.GroupName);
    }

    [Fact]
    public async Task InstallOnNewPcAsync_InstallsAWinGetPickSavedByItsId()
    {
        GivenCatalogForInstall(CreateTestItem("app1", "Firefox"));
        GivenNewPcInstall("winget-VideoLAN.VLC", succeeds: true);
        var sut = CreateSut();

        await sut.InstallOnNewPcAsync(["winget-VideoLAN.VLC"], null);

        _appInstallationService.Verify(s => s.InstallOnNewPcAsync(
            It.Is<ItemDefinition>(d => d.WinGetPackageId![0] == "VideoLAN.VLC"), It.IsAny<IProgress<TaskProgressDetail>>()), Times.Once);
        _dialogService.Verify(d => d.ShowInformationAsync(
            "ExternalApps_NewPc_Done", "ExternalApps_NewPc_Title", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task Load_UpdatesInstalledStatus()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = true }));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.Items[0].IsInstalled.Should().BeTrue();
    }

    private static InstalledApp Scanned(string key, string name, string? catalogId = null, string? availableVersion = null) =>
        new(key, name, InstalledAppKind.Program)
        {
            Version = "1.0",
            WinGetId = name,
            CatalogId = catalogId,
            UpdateVia = availableVersion is null ? PackageSources.None : PackageSources.WinGet,
            AvailableVersion = availableVersion,
        };

    private void GivenScan(params InstalledApp[] apps) =>
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>())).ReturnsAsync(new InstalledAppsScan(apps, true, new Dictionary<string, bool>()));

    private async Task<ExternalAppsViewModel> CreateScannedSut(params InstalledApp[] apps)
    {
        GivenCatalogForInstall(CreateTestItem("app1", "Firefox", isInstalled: true), CreateTestItem("app2", "7-Zip", "Compression"));
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = true, ["app2"] = false }));
        GivenScan(apps);
        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        await sut.RefreshInstalledAppsAsync();
        return sut;
    }

    [Fact]
    public async Task ToggleSelectAllWithUpdates_SelectsOnlyAppsWithAnUpdate()
    {
        var sut = await CreateScannedSut(FirefoxScan(availableVersion: "2.0"));

        sut.UpdatesCount.Should().Be(1);
        sut.HasUpdates.Should().BeTrue();

        sut.ToggleSelectAllWithUpdatesCommand.Execute(null);

        sut.Items.Single(i => i.Id == "app1").IsSelected.Should().BeTrue();
        sut.Items.Single(i => i.Id == "app2").IsSelected.Should().BeFalse();
        sut.IsAllSelectedWithUpdates.Should().BeTrue();

        sut.ToggleSelectAllWithUpdatesCommand.Execute(null);

        sut.Items.Single(i => i.Id == "app1").IsSelected.Should().BeFalse();
        sut.IsAllSelectedWithUpdates.Should().BeFalse();
    }

    [Fact]
    public async Task SelectAllWithUpdates_IsOff_WhenNoAppHasAnUpdate()
    {
        var sut = await CreateScannedSut(FirefoxScan());

        sut.HasUpdates.Should().BeFalse();
        sut.UpdatesCount.Should().Be(0);
        sut.IsAllSelectedWithUpdates.Should().BeFalse();
    }

    [Fact]
    public async Task SelectAllWithUpdates_Notifies_WhenAnAppGainsAnUpdate()
    {
        var sut = await CreateScannedSut(FirefoxScan());
        var changed = new List<string>();
        sut.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        sut.Items.Single(i => i.Id == "app1").Installed = FirefoxScan(availableVersion: "2.0");

        changed.Should().Contain([nameof(sut.UpdatesCount), nameof(sut.HasUpdates), nameof(sut.SelectAllWithUpdatesLabel), nameof(sut.IsAllSelectedWithUpdates)]);
    }

    [Fact]
    public async Task RefreshInstalledAppsAsync_ScanResult_GivesCatalogAppsTheirUpdateAndListsTheRestInTheirOwnGroup()
    {
        var sut = await CreateScannedSut(
            Scanned("reg:machine:ff", "Firefox", catalogId: "app1", availableVersion: "2.0"),
            Scanned("reg:machine:tool", "Tool"));

        var firefox = sut.Items.Single(i => i.Id == "app1");
        firefox.HasUpdate.Should().BeTrue();
        firefox.Version.Should().Be("1.0");
        firefox.AvailableVersion.Should().Be("2.0");
        sut.Items.Single(i => i.Id == "reg:machine:tool").IsOtherInstalled.Should().BeTrue();
        sut.Categories.Select(c => c.GroupName).Should().Equal("Browsers", "Compression", OtherInstalledItem.GroupName);
    }

    [Fact]
    public async Task RefreshInstalledAppsAsync_OtherInstalledAppGone_DropsIt()
    {
        var sut = await CreateScannedSut(Scanned("reg:machine:tool", "Tool"));

        GivenScan();
        await sut.RefreshInstalledAppsAsync();

        sut.Items.Select(i => i.Id).Should().BeEquivalentTo("app1", "app2");
    }

    [Fact]
    public async Task Filter_UpdatesAvailable_ListsOnlyTheAppsWithAnUpdate()
    {
        var sut = await CreateScannedSut(
            Scanned("reg:machine:ff", "Firefox", catalogId: "app1", availableVersion: "2.0"),
            Scanned("reg:machine:tool", "Tool"));

        sut.Filter = AppFilter.UpdatesAvailable;

        sut.Categories.SelectMany(c => c.Apps).Select(a => a.Id).Should().Equal("app1");
    }

    [Fact]
    public async Task SearchPackagesAsync_ResultThePcAlreadyHas_IsNotListedTwice()
    {
        var sut = await CreateScannedSut(Scanned("reg:machine:vlc", "VideoLAN.VLC"));
        GivenWinGetSearch("player", WinGetPackageItem.Create("VideoLAN.VLC", "VLC media player"), WinGetPackageItem.Create("Contoso.Player", "Contoso Player"));

        await sut.SearchPackagesAsync("player", CancellationToken.None);

        sut.Items.Should().NotContain(i => i.Id == "winget-VideoLAN.VLC").And.Contain(i => i.Id == "winget-Contoso.Player");
        sut.PackageSearchStatus.Should().Be("ExternalApps_PackageSearch_Found: 1");
    }

    [Fact]
    public async Task ToggleSelectAllCommand_BuilderAppNoPackageManagerOffers_CannotBeTicked()
    {
        var sut = await CreateScannedSut(Scanned("reg:machine:tool", "Tool") with { WinGetId = null }, Scanned("reg:machine:git", "Git"));
        var tool = sut.Items.Single(i => i.Id == "reg:machine:tool");
        tool.IsSelected = true;

        sut.AuthorsIntent = true;

        tool.IsSelected.Should().BeFalse();
        tool.NoPackageTooltip.Should().NotBeNull();
        sut.ToggleSelectAllCommand.Execute(null);
        tool.IsSelected.Should().BeFalse();
        sut.Items.Single(i => i.Id == "reg:machine:git").IsSelected.Should().BeTrue();
        sut.IsAllSelected.Should().BeTrue();
    }

    [Fact]
    public async Task AddSearchItems_SavedPackageIdOfAnInstalledApp_AddsNoSecondRow()
    {
        var sut = await CreateScannedSut(Scanned("reg:machine:git", "Git.Git"));

        sut.AddSearchItems(["winget-Git.Git"]);

        sut.Items.Should().NotContain(i => i.Id == "winget-Git.Git");
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_WhileLoading_ContentIsReadyOnlyAfterTheLoadFinishes()
    {
        bool? readyMidLoad = null;
        _externalAppsService.Setup(s => s.GetAppsAsync()).ReturnsAsync(new[] { CreateTestItem("app1") });
        var sut = CreateSut();
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .Callback(() => readyMidLoad = sut.IsContentReady)
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = true }));

        sut.IsContentReady.Should().BeFalse();

        await sut.LoadAppsAndCheckInstallationStatusAsync();

        readyMidLoad.Should().BeFalse();
        sut.IsContentReady.Should().BeTrue();
    }

    [Fact]
    public async Task RecheckInstallationStatusAsync_WhileRefreshing_HidesTheContentUntilItFinishes()
    {
        bool? readyMidRefresh = null;
        var sut = await CreateScannedSut();
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .Callback(() => readyMidRefresh = sut.IsContentReady)
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = true, ["app2"] = false }));

        await sut.RecheckInstallationStatusAsync();

        readyMidRefresh.Should().BeFalse();
        sut.IsContentReady.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_ListenerThrowsAsTheLoadEnds_DoesNotLeaveTheContentHidden()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync()).ReturnsAsync(new[] { CreateTestItem("app1", "Firefox") });
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = true }));
        var sut = CreateSut();
        sut.SelectedItemsChanged += (_, _) => throw new InvalidOperationException("listener failed");

        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.IsInitialized.Should().BeTrue();
        sut.IsContentReady.Should().BeTrue();
        sut.Categories.Should().ContainSingle();
    }

    [Fact]
    public async Task RecheckInstallationStatusAsync_UpdatesFilterOnAndAppGone_DropsTheApp()
    {
        var sut = await CreateScannedSut(Scanned("reg:machine:ff", "Firefox", catalogId: "app1", availableVersion: "2.0"));
        sut.Filter = AppFilter.UpdatesAvailable;
        sut.Categories.SelectMany(c => c.Apps).Select(a => a.Id).Should().Equal("app1");
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = false, ["app2"] = false }));

        await sut.RecheckInstallationStatusAsync();

        sut.Categories.Should().BeEmpty();
    }

    [Fact]
    public async Task RefreshInstalledAppsAsync_Always_SaysWhetherTheScanWorked()
    {
        var sut = await CreateScannedSut(Scanned("reg:machine:tool", "Tool"));

        (await sut.RefreshInstalledAppsAsync()).Should().BeTrue();

        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("winget is broken"));

        (await sut.RefreshInstalledAppsAsync()).Should().BeFalse();
        sut.Items.Should().Contain(i => i.Id == "reg:machine:tool");
    }

    [Fact]
    public async Task RefreshInstalledAppsAsync_ScanCouldNotReadTheUpdates_ListsItsAppsAndSaysItIsIncomplete()
    {
        var sut = await CreateScannedSut();
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([Scanned("reg:machine:tool", "Tool")], false, new Dictionary<string, bool>()));

        (await sut.RefreshInstalledAppsAsync()).Should().BeFalse();

        sut.Items.Should().Contain(i => i.Id == "reg:machine:tool");
    }

    [Fact]
    public async Task RefreshInstalledAppsAsync_TickedSearchPickNowInstalled_GetsNoSecondRow()
    {
        var sut = await CreateScannedSut();
        GivenWinGetSearch("player", WinGetPackageItem.Create("VideoLAN.VLC", "VLC media player"));
        await sut.SearchPackagesAsync("player", CancellationToken.None);
        sut.Items.Single(i => i.Id == "winget-VideoLAN.VLC").IsSelected = true;

        GivenScan(Scanned("reg:machine:vlc", "VideoLAN.VLC"));
        await sut.RefreshInstalledAppsAsync();

        sut.Items.Should().NotContain(i => i.Id == "reg:machine:vlc");
        sut.Items.Single(i => i.Id == "winget-VideoLAN.VLC").Version.Should().Be("1.0");
    }

    [Fact]
    public async Task RefreshInstalledAppsAsync_InstalledCatalogAppTheScanCouldNotMap_GetsNoSecondRow()
    {
        var sut = await CreateScannedSut(Scanned("reg:machine:ff", "Firefox"), Scanned("reg:machine:7z", "7-Zip"));

        sut.Items.Should().NotContain(i => i.Id == "reg:machine:ff");
        sut.Items.Single(i => i.Id == "app1").Version.Should().Be("1.0");
        sut.Items.Should().Contain(i => i.Id == "reg:machine:7z", "the catalog's 7-Zip row is not detected as installed");
    }

    [Fact]
    public async Task InstallApps_WhenSomeAppsFail_NamesEveryFailedAppInOneWarning()
    {
        GivenCatalogForInstall(CreateTestItem("app1", "Firefox"), CreateTestItem("app2", "Chrome"), CreateTestItem("app3", "7-Zip"));
        _appInstallationService.Setup(s => s.InstallAppAsync(
                It.Is<ItemDefinition>(d => d.Id == "app1"), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<bool>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));
        _appInstallationService.Setup(s => s.InstallAppAsync(
                It.Is<ItemDefinition>(d => d.Id != "app1"), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<bool>()))
            .ReturnsAsync(OperationResult<bool>.Failed("install failed"));
        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        foreach (var item in sut.Items)
            item.IsSelected = true;

        await sut.InstallApps(skipConfirmation: true);

        _dialogService.Verify(d => d.ShowWarningAsync(
            It.Is<string>(m => m.Contains("ExternalApps_InstallFailed") && m.Contains("Chrome") && m.Contains("7-Zip") && !m.Contains("Firefox")),
            It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task InstallApps_WhenEveryAppInstalled_WarnsOfNothing()
    {
        GivenCatalogForInstall(CreateTestItem("app1", "Firefox"));
        _appInstallationService.Setup(s => s.InstallAppAsync(
                It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<bool>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));
        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        sut.Items[0].IsSelected = true;

        await sut.InstallApps(skipConfirmation: true);

        _dialogService.Verify(d => d.ShowWarningAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task InstallApps_TwoRowsShareAName_KeepsOnlyTheFailedRowTicked()
    {
        GivenCatalogForInstall(CreateTestItem("app1", "Firefox"), CreateTestItem("app2", "Firefox"));
        _appInstallationService.Setup(s => s.InstallAppAsync(
                It.Is<ItemDefinition>(d => d.Id == "app1"), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<bool>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));
        _appInstallationService.Setup(s => s.InstallAppAsync(
                It.Is<ItemDefinition>(d => d.Id == "app2"), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<bool>()))
            .ReturnsAsync(OperationResult<bool>.Failed("install failed"));
        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        foreach (var item in sut.Items)
            item.IsSelected = true;

        await sut.InstallApps(skipConfirmation: true);

        sut.Items.Single(i => i.Id == "app1").IsSelected.Should().BeFalse();
        sut.Items.Single(i => i.Id == "app2").IsSelected.Should().BeTrue();
    }

    private static InstalledApp FirefoxScan(string? availableVersion = null) =>
        Scanned("reg:machine:ff", "Firefox", catalogId: "app1", availableVersion: availableVersion);

    private void GivenUpdatesRun()
    {
        _progressService.Setup(p => p.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(new CancellationTokenSource());
        _installedAppsService.Setup(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));
    }

    private void GivenInstall(string id, bool succeeds) =>
        _appInstallationService.Setup(s => s.InstallAppAsync(
                It.Is<ItemDefinition>(d => d.Id == id), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<bool>()))
            .ReturnsAsync(succeeds ? OperationResult<bool>.Succeeded(true) : OperationResult<bool>.Failed("install failed"));

    private void GivenInstallConfirmed() =>
        _dialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

    [Fact]
    public async Task InstallOrUpdateSelectedAsync_MixedSelection_UpdatesTheAppsWithAnUpdateAndInstallsTheRest()
    {
        var sut = await CreateScannedSut(FirefoxScan("2.0"));
        GivenUpdatesRun();
        GivenInstallConfirmed();
        GivenInstall("app2", succeeds: true);
        GivenScan(FirefoxScan());
        foreach (var item in sut.Items)
            item.IsSelected = true;

        await sut.InstallOrUpdateSelectedAsync();

        _installedAppsService.Verify(s => s.UpdateAsync(
            It.Is<InstalledApp>(a => a.Key == "reg:machine:ff"), It.IsAny<CancellationToken>()), Times.Once);
        _appInstallationService.Verify(s => s.InstallAppAsync(
            It.Is<ItemDefinition>(d => d.Id == "app2"), It.IsAny<IProgress<TaskProgressDetail>>(), false), Times.Once);
        _appInstallationService.Verify(s => s.InstallAppAsync(
            It.Is<ItemDefinition>(d => d.Id == "app1"), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<bool>()), Times.Never);
        _dialogService.Verify(d => d.ShowConfirmationAsync(
            It.Is<ConfirmationRequest>(r => r.Items!.Count == 1 && r.Items[0] == "7-Zip")), Times.Once);
        sut.HasSelectedItems.Should().BeFalse();
    }

    [Fact]
    public async Task InstallOrUpdateSelectedAsync_EveryTickedAppOnlyNeedsAnUpdate_AsksNothing()
    {
        var sut = await CreateScannedSut(FirefoxScan("2.0"));
        GivenUpdatesRun();
        GivenScan(FirefoxScan());
        sut.Items.Single(i => i.Id == "app1").IsSelected = true;

        await sut.InstallOrUpdateSelectedAsync();

        _installedAppsService.Verify(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()), Times.Once);
        _dialogService.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
        _appInstallationService.Verify(s => s.InstallAppAsync(
            It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<bool>()), Times.Never);
        sut.HasSelectedItems.Should().BeFalse();
    }

    [Fact]
    public async Task InstallOrUpdateSelectedAsync_WhenTheUpdateIsCancelled_CancelsTheTaskInsteadOfFailingIt()
    {
        var sut = await CreateScannedSut(FirefoxScan("2.0"));
        GivenUpdatesRun();
        _installedAppsService.Setup(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        GivenScan(FirefoxScan("2.0"));
        sut.Items.Single(i => i.Id == "app1").IsSelected = true;

        await sut.InstallOrUpdateSelectedAsync();

        _progressService.Verify(p => p.CancelTask(), Times.Once);
        _progressService.Verify(p => p.FailTask(), Times.Never);
    }

    [Fact]
    public async Task InstallOrUpdateSelectedAsync_WhenAnInstallFails_KeepsWhatFailedTicked()
    {
        var sut = await CreateScannedSut(FirefoxScan("2.0"));
        GivenUpdatesRun();
        GivenInstallConfirmed();
        GivenInstall("app2", succeeds: false);
        foreach (var item in sut.Items)
            item.IsSelected = true;

        await sut.InstallOrUpdateSelectedAsync();

        sut.Items.Single(i => i.Id == "app1").IsSelected.Should().BeTrue("the rescan still shows its update");
        sut.Items.Single(i => i.Id == "app2").IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task InstallOrUpdateSelectedAsync_InstallDeclined_DoesNothing()
    {
        var sut = await CreateScannedSut(FirefoxScan("2.0"));
        GivenUpdatesRun();
        _dialogService.Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false });
        foreach (var item in sut.Items)
            item.IsSelected = true;

        await sut.InstallOrUpdateSelectedAsync();

        _installedAppsService.Verify(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()), Times.Never);
        sut.Items.Should().OnlyContain(i => i.IsSelected);
    }

    [Fact]
    public async Task UpdateAppsAsync_Always_PassesTheTaskTokenAndLeavesTheTicksAlone()
    {
        var sut = await CreateScannedSut(FirefoxScan("2.0"));
        GivenUpdatesRun();
        using var cancellation = new CancellationTokenSource();
        _progressService.Setup(p => p.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(cancellation);
        GivenScan(FirefoxScan());
        foreach (var item in sut.Items)
            item.IsSelected = true;

        var ran = await sut.UpdateAppsAsync([sut.Items.Single(i => i.Id == "app1")]);

        ran.Started.Should().BeTrue();
        ran.Cancelled.Should().BeFalse();
        _installedAppsService.Verify(s => s.UpdateAsync(It.IsAny<InstalledApp>(), cancellation.Token), Times.Once);
        sut.Items.Should().OnlyContain(i => i.IsSelected);
    }

    [Fact]
    public async Task UpdateAppsAsync_AnotherTaskRunning_SaysNothingRan()
    {
        var sut = await CreateScannedSut(FirefoxScan("2.0"));
        GivenUpdatesRun();
        _progressService.Setup(p => p.IsTaskRunning).Returns(true);

        var ran = await sut.UpdateAppsAsync([sut.Items.Single(i => i.Id == "app1")]);

        ran.Started.Should().BeFalse();
        _installedAppsService.Verify(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RecheckInstallationStatusAsync_TickedAppLosesItsUpdate_RaisesSelectedItemsChanged()
    {
        var sut = await CreateScannedSut(FirefoxScan("2.0"));
        var firefox = sut.Items.Single(i => i.Id == "app1");
        firefox.IsSelected = true;
        var raised = 0;
        sut.SelectedItemsChanged += (_, _) => raised++;
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), StatusRules.External, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([], true, new Dictionary<string, bool> { ["app1"] = false, ["app2"] = false }));

        await sut.RecheckInstallationStatusAsync();

        firefox.HasUpdate.Should().BeFalse();
        raised.Should().BePositive();
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task InstallOrUpdateSelectedAsync_UpdatesCancelled_InstallsNothing()
    {
        var sut = await CreateScannedSut(FirefoxScan("2.0"));
        GivenUpdatesRun();
        GivenInstallConfirmed();
        GivenInstall("app2", succeeds: true);
        using var cancellation = new CancellationTokenSource();
        _progressService.Setup(p => p.StartTask(It.IsAny<string>(), It.IsAny<bool>())).Returns(cancellation);
        _installedAppsService.Setup(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                cancellation.Cancel();
                return Task.FromException<OperationResult<bool>>(new OperationCanceledException());
            });
        foreach (var item in sut.Items)
            item.IsSelected = true;

        await sut.InstallOrUpdateSelectedAsync();

        _appInstallationService.Verify(s => s.InstallAppAsync(
            It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<bool>()), Times.Never);
        sut.Items.Should().OnlyContain(i => i.IsSelected);
    }

    [Fact]
    public async Task InstallOrUpdateSelectedAsync_AnotherTaskRunning_SaysSoInsteadOfSkippingTheUpdates()
    {
        var sut = await CreateScannedSut(FirefoxScan("2.0"));
        GivenUpdatesRun();
        GivenInstallConfirmed();
        GivenInstall("app2", succeeds: true);
        _progressService.Setup(p => p.IsTaskRunning).Returns(true);
        foreach (var item in sut.Items)
            item.IsSelected = true;

        await sut.InstallOrUpdateSelectedAsync();

        _dialogService.Verify(d => d.ShowWarningAsync("ExternalApps_UpdateBusy_Message", It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        _installedAppsService.Verify(s => s.UpdateAsync(It.IsAny<InstalledApp>(), It.IsAny<CancellationToken>()), Times.Never);
        _appInstallationService.Verify(s => s.InstallAppAsync(
            It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<bool>()), Times.Never);
        sut.Items.Should().OnlyContain(i => i.IsSelected);
    }

    [Fact]
    public async Task RefreshInstalledAppsAsync_ScanCouldNotReadTheUpdates_KeepsTheUpdatesTheRowsShowed()
    {
        var sut = await CreateScannedSut(FirefoxScan("2.0"), Scanned("reg:machine:tool", "Tool", availableVersion: "3.0"));
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([FirefoxScan(), Scanned("reg:machine:tool", "Tool")], false, new Dictionary<string, bool>()));

        await sut.RefreshInstalledAppsAsync();

        sut.Items.Single(i => i.Id == "app1").HasUpdate.Should().BeTrue();
        sut.Items.Single(i => i.Id == "app1").AvailableVersion.Should().Be("2.0");
        sut.Items.Single(i => i.Id == "reg:machine:tool").AvailableVersion.Should().Be("3.0");
    }

    [Fact]
    public async Task RefreshInstalledAppsAsync_ScanCouldNotReadTheUpdatesAndVersionChanged_DropsTheAppsUpdate()
    {
        var sut = await CreateScannedSut(FirefoxScan("2.0"));
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([FirefoxScan() with { Version = "2.0" }], false, new Dictionary<string, bool>()));

        await sut.RefreshInstalledAppsAsync();

        sut.Items.Single(i => i.Id == "app1").HasUpdate.Should().BeFalse();
        sut.Items.Single(i => i.Id == "app1").Version.Should().Be("2.0");
    }

    [Fact]
    public async Task InstallOrUpdateSelectedAsync_ScanAfterItCouldNotReadTheUpdates_KeepsTheTicks()
    {
        var sut = await CreateScannedSut(FirefoxScan("2.0"));
        GivenUpdatesRun();
        _installedAppsService.Setup(s => s.GetInstalledAppsAsync(It.IsAny<IReadOnlyList<ItemDefinition>>(), It.IsAny<StatusRules>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InstalledAppsScan([FirefoxScan()], false, new Dictionary<string, bool>()));
        var firefox = sut.Items.Single(i => i.Id == "app1");
        firefox.IsSelected = true;

        await sut.InstallOrUpdateSelectedAsync();

        firefox.IsSelected.Should().BeTrue();
    }

    private static readonly InstallOptions MachineX86 = new() { Scope = InstallScope.Machine, Architecture = InstallArchitecture.X86 };

    private AppItemViewModel InstallOptionsApp(string id = "winget-Mozilla.Firefox") =>
        new(CreateTestItem(id, "Firefox"), _localizationService.Object, _dispatcherService.Object, _themeService.Object);

    private void GivenDialogReturns(InstallOptionsChoice? choice) =>
        _dialogService.Setup(d => d.ShowInstallOptionsDialogAsync(It.IsAny<string?>(), It.IsAny<InstallOptionsChoice>()))
            .ReturnsAsync(choice);

    [Fact]
    public async Task EditDefaultInstallOptionsAsync_Always_OpensWithTheCurrentDefaultsAndSavesTheChoice()
    {
        _installOptions.Setup(o => o.Defaults).Returns(InstallOptions.Default);
        GivenDialogReturns(new InstallOptionsChoice(false, MachineX86));

        await CreateSut().EditDefaultInstallOptionsAsync();

        _dialogService.Verify(d => d.ShowInstallOptionsDialogAsync(null, new InstallOptionsChoice(false, InstallOptions.Default)));
        _installOptions.Verify(o => o.SetDefaultsAsync(MachineX86));
    }

    [Fact]
    public async Task EditDefaultInstallOptionsAsync_WhenCancelled_WritesNothing()
    {
        _installOptions.Setup(o => o.Defaults).Returns(InstallOptions.Default);
        GivenDialogReturns(null);

        await CreateSut().EditDefaultInstallOptionsAsync();

        _installOptions.Verify(o => o.SetDefaultsAsync(It.IsAny<InstallOptions>()), Times.Never);
    }

    [Fact]
    public async Task EditInstallOptionsAsync_AppWithoutItsOwnOptions_OpensFollowingTheDefaults()
    {
        _installOptions.Setup(o => o.Defaults).Returns(MachineX86);
        _installOptions.Setup(o => o.ForApp("winget-Mozilla.Firefox")).Returns((InstallOptions?)null);
        GivenDialogReturns(null);

        await CreateSut().EditInstallOptionsAsync(InstallOptionsApp());

        _dialogService.Verify(d => d.ShowInstallOptionsDialogAsync("Firefox", new InstallOptionsChoice(true, MachineX86)));
    }

    [Fact]
    public async Task EditInstallOptionsAsync_AppWithItsOwnOptions_OpensShowingThem()
    {
        var own = new InstallOptions { Interactive = true };
        _installOptions.Setup(o => o.Defaults).Returns(MachineX86);
        _installOptions.Setup(o => o.ForApp("winget-Mozilla.Firefox")).Returns(own);
        GivenDialogReturns(null);

        await CreateSut().EditInstallOptionsAsync(InstallOptionsApp());

        _dialogService.Verify(d => d.ShowInstallOptionsDialogAsync("Firefox", new InstallOptionsChoice(false, own)));
    }

    [Fact]
    public async Task EditInstallOptionsAsync_ChoosingToFollowTheDefaults_ClearsTheAppsOwnOptions()
    {
        _installOptions.Setup(o => o.ForApp(It.IsAny<string>())).Returns(MachineX86);
        GivenDialogReturns(new InstallOptionsChoice(true, InstallOptions.Default));

        await CreateSut().EditInstallOptionsAsync(InstallOptionsApp());

        _installOptions.Verify(o => o.SetForAppAsync("winget-Mozilla.Firefox", null));
    }

    [Fact]
    public async Task EditInstallOptionsAsync_ChoosingOwnOptions_SavesThemForThatApp()
    {
        _installOptions.Setup(o => o.Defaults).Returns(InstallOptions.Default);
        GivenDialogReturns(new InstallOptionsChoice(false, MachineX86));

        await CreateSut().EditInstallOptionsAsync(InstallOptionsApp());

        _installOptions.Verify(o => o.SetForAppAsync("winget-Mozilla.Firefox", MachineX86));
    }

    [Fact]
    public async Task EditInstallOptionsAsync_WhenCancelled_WritesNothing()
    {
        _installOptions.Setup(o => o.Defaults).Returns(InstallOptions.Default);
        GivenDialogReturns(null);

        await CreateSut().EditInstallOptionsAsync(InstallOptionsApp());

        _installOptions.Verify(o => o.SetForAppAsync(It.IsAny<string>(), It.IsAny<InstallOptions?>()), Times.Never);
    }

    [Fact]
    public void CanEditInstallOptions_OnlyAppsWinGetOrChocolateyInstall_ReturnTrue()
    {
        var storeOnly = new AppItemViewModel(new ItemDefinition { Id = "msstore-9NBLGGH4NNS1", Name = "Store app", Description = string.Empty, MsStoreId = "9NBLGGH4NNS1" },
            _localizationService.Object, _dispatcherService.Object, _themeService.Object);
        var choco = new AppItemViewModel(new ItemDefinition { Id = "choco-7zip", Name = "7-Zip", Description = string.Empty, ChocoPackageId = "7zip" },
            _localizationService.Object, _dispatcherService.Object, _themeService.Object);

        ExternalAppsViewModel.CanEditInstallOptions(InstallOptionsApp()).Should().BeTrue();
        ExternalAppsViewModel.CanEditInstallOptions(choco).Should().BeTrue();
        ExternalAppsViewModel.CanEditInstallOptions(storeOnly).Should().BeFalse();
        ExternalAppsViewModel.CanEditInstallOptions(null).Should().BeFalse();
    }
}
