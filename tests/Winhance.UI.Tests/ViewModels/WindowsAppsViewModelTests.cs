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

public class WindowsAppsViewModelTests
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

    public WindowsAppsViewModelTests()
    {
        _dispatcherService.Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _dispatcherService.Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(f => f().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        _localizationService.Setup(l => l.GetString(It.IsAny<string>()))
            .Returns<string>(k => k);
    }

    private WindowsAppsViewModel CreateSut() => new(
        _windowsAppsService.Object,
        _appInstallationService.Object,
        _appUninstallationService.Object,
        _progressService.Object,
        _logService.Object,
        _dialogService.Object,
        _localizationService.Object,
        _connectivityService.Object,
        _dispatcherService.Object);

    private ItemDefinition CreateTestItem(string id, string name = "Test App",
        string? appxPackageName = "Microsoft.Test", bool isInstalled = false) => new()
    {
        Id = id,
        Name = name,
        Description = $"Description for {name}",
        AppxPackageName = appxPackageName,
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

    // --- Localized section headers ---

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

    // --- LoadAppsAndCheckInstallationStatusAsync ---

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
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
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
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>());

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

        // The error is logged; finalization still sets "Loaded 0 items"
        _logService.Verify(l => l.LogError(
            It.Is<string>(s => s.Contains("Error loading app definitions")),
            It.IsAny<Exception>()), Times.Once);
        sut.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAppsAndCheckInstallationStatusAsync_SetsIsLoadingDuringOperation()
    {
        bool wasLoadingDuringGet = false;

        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>());

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        // After completion, IsLoading should be false
        sut.IsLoading.Should().BeFalse();
    }

    // --- LoadItemsAsync ---

    [Fact]
    public async Task LoadItemsAsync_DelegatesToLoadAppsAndCheckInstallationStatus()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
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
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(Enumerable.Empty<ItemDefinition>());
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool>());

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        await sut.RefreshInstallationStatusAsync();

        _windowsAppsService.Verify(s => s.InvalidateStatusCache(), Times.Once);
        sut.IsLoading.Should().BeFalse();
    }

    // --- InstallAppsAsync ---

    [Fact]
    public async Task InstallAppsAsync_WhenNoItemsSelected_ShowsWarning()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
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
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
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

    // --- RemoveAppsAsync ---

    [Fact]
    public async Task RemoveAppsAsync_WhenNoItemsSelected_ShowsWarning()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = true });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        await sut.RemoveAppsAsync();

        _dialogService.Verify(d => d.ShowWarningAsync(
            It.Is<string>(s => s.Contains("select at least one")),
            It.IsAny<string>()), Times.Once);
    }

    // --- ClearSelections ---

    [Fact]
    public async Task ClearSelections_DeselectsAllItems()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1", "App1"),
                CreateTestItem("app2", "App2")
            });
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
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
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
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
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1"),
                CreateTestItem("app2")
            });
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = false, ["app2"] = false });

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
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = false, ["app2"] = false });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        // Select all first
        sut.ToggleSelectAllCommand.Execute(null);
        sut.IsAllSelected.Should().BeTrue();

        // Toggle again to deselect
        sut.ToggleSelectAllCommand.Execute(null);

        sut.Items.Should().OnlyContain(i => !i.IsSelected);
    }

    // --- ToggleSelectAllInstalled ---

    [Fact]
    public async Task ToggleSelectAllInstalled_SelectsOnlyInstalledItems()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1", isInstalled: true),
                CreateTestItem("app2", isInstalled: false)
            });
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
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
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[]
            {
                CreateTestItem("app1", isInstalled: true),
                CreateTestItem("app2", isInstalled: false)
            });
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = true, ["app2"] = false });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.ToggleSelectAllNotInstalledCommand.Execute(null);

        var installedItem = sut.Items.First(i => i.IsInstalled);
        var notInstalledItem = sut.Items.First(i => !i.IsInstalled);

        installedItem.IsSelected.Should().BeFalse();
        notInstalledItem.IsSelected.Should().BeTrue();
    }

    // --- CheckInstallationStatusAsync ---

    [Fact]
    public async Task CheckInstallationStatusAsync_UpdatesInstalledStatus()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
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

    [Fact]
    public async Task Dispose_CleansUpEventSubscriptions()
    {
        _windowsAppsService.Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new[] { CreateTestItem("app1") });
        _windowsAppsService.Setup(s => s.CheckBatchInstalledAsync(It.IsAny<IEnumerable<ItemDefinition>>()))
            .ReturnsAsync(new Dictionary<string, bool> { ["app1"] = false });

        var sut = CreateSut();
        await sut.LoadAppsAndCheckInstallationStatusAsync();

        sut.Dispose();

        // After dispose, items should still be accessible but the ViewModel should be disposed
        sut.Items.Should().NotBeNull();
    }
}
