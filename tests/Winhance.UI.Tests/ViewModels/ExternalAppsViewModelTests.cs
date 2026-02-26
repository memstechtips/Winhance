using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class ExternalAppsViewModelTests
{
    private readonly Mock<IExternalAppsService> _externalAppsService = new();
    private readonly Mock<ITaskProgressService> _progressService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<IInternetConnectivityService> _connectivityService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();

    public ExternalAppsViewModelTests()
    {
        _dispatcherService.Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _dispatcherService.Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(f => f().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        _localizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);
    }

    private ExternalAppsViewModel CreateSut() => new(
        _externalAppsService.Object,
        _progressService.Object,
        _logService.Object,
        _dialogService.Object,
        _localizationService.Object,
        _connectivityService.Object,
        _dispatcherService.Object);

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

    // --- Constructor / defaults ---

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        var sut = CreateSut();

        sut.StatusText.Should().Be("Ready");
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

    // --- HasSelectedItems ---

    [Fact]
    public void HasSelectedItems_WhenNoItems_ReturnsFalse()
    {
        var sut = CreateSut();

        sut.HasSelectedItems.Should().BeFalse();
    }

    // --- IsAllSelected ---

    [Fact]
    public void IsAllSelected_WhenNoItems_ReturnsFalse()
    {
        var sut = CreateSut();

        sut.IsAllSelected.Should().BeFalse();
    }

    // --- Localized labels ---

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

    // --- LoadAppsAndCheckInstallationStatusAsync ---

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
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>
            {
                ["app1"] = true,
                ["app2"] = false
            });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.Items.Should().HaveCount(2);
        sut.IsInitialized.Should().BeTrue();
        sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_WhenAlreadyInitialized_SkipsLoading()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>());

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

        // The error is logged; finalization still sets "Loaded 0 items"
        _logService.Verify(l => l.LogError(
            It.Is<string>(s => s.Contains("Error loading app definitions")),
            It.IsAny<Exception>()), Times.Once);
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
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>
            {
                ["app1"] = false,
                ["app2"] = false,
                ["app3"] = false
            });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.Categories.Should().HaveCount(2);
    }

    // --- LoadItemsAsync ---

    [Fact]
    public async Task LoadItemsAsync_DelegatesToLoadAppsAndCheckInstallationStatus()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>());

        var sut = CreateSut();
        await sut.LoadItemsAsync();

        sut.IsInitialized.Should().BeTrue();
    }

    // --- RefreshInstallationStatusAsync ---

    [Fact]
    public async Task RefreshInstallationStatusAsync_WhenNotInitialized_SetsWaitMessage()
    {
        var sut = CreateSut();

        await sut.RefreshInstallationStatusAsync();

        sut.StatusText.Should().Be("Progress_WaitForInitialLoad");
    }

    [Fact]
    public async Task RefreshInstallationStatusAsync_WhenInitialized_InvalidatesCacheAndRefreshes()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>());

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        await sut.RefreshInstallationStatusAsync();

        _externalAppsService.Verify(s => s.InvalidateStatusCache(), Times.Once);
        sut.IsLoading.Should().BeFalse();
    }

    // --- InstallAppsAsync ---

    [Fact]
    public async Task InstallAppsAsync_WhenNoItemsSelected_ShowsWarning()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = false });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        await sut.InstallAppsAsync();

        _dialogService.Verify(d => d.ShowWarningAsync(
            It.Is<string>(s => s.Contains("select at least one")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task InstallAppsAsync_WhenNoInternet_ShowsWarning()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = false });
        _connectivityService.Setup(c => c.IsInternetConnectedAsync(true))
            .ReturnsAsync(false);

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        sut.Items[0].IsSelected = true;

        await sut.InstallAppsAsync();

        _dialogService.Verify(d => d.ShowWarningAsync(
            It.Is<string>(s => s.Contains("internet connection")),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task InstallAppsAsync_WhenUserCancelsConfirmation_DoesNotInstall()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = false });
        _connectivityService.Setup(c => c.IsInternetConnectedAsync(true))
            .ReturnsAsync(true);
        _dialogService.Setup(d => d.ShowAppOperationConfirmationAsync(
            It.IsAny<string>(), It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), It.IsAny<string?>()))
            .ReturnsAsync((false, false));

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        sut.Items[0].IsSelected = true;

        await sut.InstallAppsAsync();

        _externalAppsService.Verify(s => s.InstallAppAsync(
            It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>>()), Times.Never);
    }

    // --- UninstallAppsAsync ---

    [Fact]
    public async Task UninstallAppsAsync_WhenNoItemsSelected_ShowsWarning()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = true });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        await sut.UninstallAppsAsync();

        _dialogService.Verify(d => d.ShowWarningAsync(
            It.Is<string>(s => s.Contains("select at least one")),
            It.IsAny<string>()), Times.Once);
    }

    // --- ClearSelections ---

    [Fact]
    public async Task ClearSelections_DeselectsAllItems()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1", "Firefox"),
                CreateTestItem("app2", "Chrome")
            });
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = true, ["app2"] = false });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        foreach (var item in sut.Items) item.IsSelected = true;
        sut.HasSelectedItems.Should().BeTrue();

        sut.ClearSelections();

        sut.Items.Should().OnlyContain(i => !i.IsSelected);
        sut.HasSelectedItems.Should().BeFalse();
    }

    // --- SelectedItemsChanged event ---

    [Fact]
    public async Task SelectedItemsChanged_RaisedWhenItemSelectionChanges()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = false });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        bool eventRaised = false;
        sut.SelectedItemsChanged += (_, _) => eventRaised = true;

        sut.Items[0].IsSelected = true;

        eventRaised.Should().BeTrue();
    }

    // --- ToggleSelectAll ---

    [Fact]
    public async Task ToggleSelectAll_SelectsAllItems()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1"),
                CreateTestItem("app2")
            });
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = false, ["app2"] = false });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.ToggleSelectAllCommand.Execute(null);

        sut.Items.Should().OnlyContain(i => i.IsSelected);
        sut.IsAllSelected.Should().BeTrue();
    }

    // --- ToggleSelectAllInstalled ---

    [Fact]
    public async Task ToggleSelectAllInstalled_SelectsOnlyInstalledItems()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1", isInstalled: true),
                CreateTestItem("app2", isInstalled: false)
            });
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = true, ["app2"] = false });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.ToggleSelectAllInstalledCommand.Execute(null);

        var installedItem = sut.Items.First(i => i.IsInstalled);
        var notInstalledItem = sut.Items.First(i => !i.IsInstalled);

        installedItem.IsSelected.Should().BeTrue();
        notInstalledItem.IsSelected.Should().BeFalse();
    }

    // --- ToggleSelectAllNotInstalled ---

    [Fact]
    public async Task ToggleSelectAllNotInstalled_SelectsOnlyNotInstalledItems()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1", isInstalled: true),
                CreateTestItem("app2", isInstalled: false)
            });
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = true, ["app2"] = false });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.ToggleSelectAllNotInstalledCommand.Execute(null);

        var installedItem = sut.Items.First(i => i.IsInstalled);
        var notInstalledItem = sut.Items.First(i => !i.IsInstalled);

        installedItem.IsSelected.Should().BeFalse();
        notInstalledItem.IsSelected.Should().BeTrue();
    }

    // --- InstallApps (overload with skipConfirmation) ---

    [Fact]
    public async Task InstallApps_WhenNoItemsSelected_ReturnsEarly()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = false });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        // No items selected
        await sut.InstallApps(skipConfirmation: true);

        _externalAppsService.Verify(s => s.InstallAppAsync(
            It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>>()), Times.Never);
    }

    [Fact]
    public async Task InstallApps_WhenNoInternetAndSkipConfirmation_ReturnsEarly()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = false });
        _connectivityService.Setup(c => c.IsInternetConnectedAsync(true))
            .ReturnsAsync(false);

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();
        sut.Items[0].IsSelected = true;

        await sut.InstallApps(skipConfirmation: true);

        // Should not show warning when skipConfirmation=true
        _dialogService.Verify(d => d.ShowWarningAsync(
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    // --- CheckInstallationStatusAsync ---

    [Fact]
    public async Task CheckInstallationStatusAsync_UpdatesInstalledStatus()
    {
        _externalAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _externalAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = true });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.Items[0].IsInstalled.Should().BeTrue();
    }

    // --- Dispose ---

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var sut = CreateSut();

        var act = () => sut.Dispose();

        act.Should().NotThrow();
    }
}
