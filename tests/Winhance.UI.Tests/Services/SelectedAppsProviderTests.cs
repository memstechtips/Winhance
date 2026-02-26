using System.Collections.ObjectModel;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.SoftwareApps.Services;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class SelectedAppsProviderTests : IDisposable
{
    private readonly Mock<IWindowsAppsService> _mockWindowsAppsService = new();
    private readonly Mock<IAppInstallationService> _mockAppInstallationService = new();
    private readonly Mock<IAppUninstallationService> _mockAppUninstallationService = new();
    private readonly Mock<ITaskProgressService> _mockProgressService = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IInternetConnectivityService> _mockConnectivityService = new();
    private readonly Mock<IDispatcherService> _mockDispatcherService = new();

    private WindowsAppsViewModel? _windowsAppsVm;

    public SelectedAppsProviderTests()
    {
        _mockDispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(a => a());
        _mockDispatcherService
            .Setup(d => d.RunOnUIThreadAsync(It.IsAny<Func<Task>>()))
            .Callback<Func<Task>>(f => f().GetAwaiter().GetResult())
            .Returns(Task.CompletedTask);

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
    }

    public void Dispose()
    {
        _windowsAppsVm?.Dispose();
    }

    private WindowsAppsViewModel CreateWindowsAppsVm()
    {
        _windowsAppsVm = new WindowsAppsViewModel(
            _mockWindowsAppsService.Object,
            _mockAppInstallationService.Object,
            _mockAppUninstallationService.Object,
            _mockProgressService.Object,
            _mockLogService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockConnectivityService.Object,
            _mockDispatcherService.Object);
        return _windowsAppsVm;
    }

    private SelectedAppsProvider CreateSut(WindowsAppsViewModel? vm = null)
    {
        return new SelectedAppsProvider(vm ?? CreateWindowsAppsVm());
    }

    private AppItemViewModel CreateAppItemViewModel(
        string id,
        string name,
        bool isSelected = false,
        string? appxPackageName = null,
        string? capabilityName = null,
        string? optionalFeatureName = null,
        string[]? subPackages = null)
    {
        var definition = new ItemDefinition
        {
            Id = id,
            Name = name,
            Description = $"Description for {name}",
            AppxPackageName = appxPackageName,
            CapabilityName = capabilityName,
            OptionalFeatureName = optionalFeatureName,
            SubPackages = subPackages
        };

        var vm = new AppItemViewModel(
            definition,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object);

        vm.IsSelected = isSelected;
        return vm;
    }

    // -------------------------------------------------------
    // Constructor
    // -------------------------------------------------------

    [Fact]
    public void Constructor_WithValidViewModel_DoesNotThrow()
    {
        var act = () => CreateSut();

        act.Should().NotThrow();
    }

    // -------------------------------------------------------
    // GetSelectedWindowsAppsAsync - empty items
    // -------------------------------------------------------

    [Fact]
    public async Task GetSelectedWindowsAppsAsync_WithNoItems_ReturnsEmptyList()
    {
        var vm = CreateWindowsAppsVm();
        // Mark as initialized so it doesn't try to load
        // We need to use reflection or set up items directly
        _mockWindowsAppsService
            .Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition>());

        var sut = CreateSut(vm);

        // Since IsInitialized is false, it will call LoadItemsAsync first
        var result = await sut.GetSelectedWindowsAppsAsync();

        result.Should().BeEmpty();
    }

    // -------------------------------------------------------
    // GetSelectedWindowsAppsAsync - selected items
    // -------------------------------------------------------

    [Fact]
    public async Task GetSelectedWindowsAppsAsync_WithSelectedAppxApp_ReturnsConfigItemWithAppxPackageName()
    {
        var vm = CreateWindowsAppsVm();

        _mockWindowsAppsService
            .Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition>());

        // Pre-load to set IsInitialized
        await vm.LoadItemsAsync();

        var app = CreateAppItemViewModel(
            "test-app",
            "Test App",
            isSelected: true,
            appxPackageName: "Microsoft.TestApp");
        vm.Items.Add(app);

        var sut = CreateSut(vm);

        var result = await sut.GetSelectedWindowsAppsAsync();

        result.Should().ContainSingle();
        var item = result[0];
        item.Id.Should().Be("test-app");
        item.Name.Should().Be("Test App");
        item.IsSelected.Should().BeTrue();
        item.InputType.Should().Be(InputType.Toggle);
        item.AppxPackageName.Should().Be("Microsoft.TestApp");
    }

    [Fact]
    public async Task GetSelectedWindowsAppsAsync_WithUnselectedApp_DoesNotIncludeIt()
    {
        var vm = CreateWindowsAppsVm();

        _mockWindowsAppsService
            .Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition>());

        await vm.LoadItemsAsync();

        var app = CreateAppItemViewModel(
            "test-app",
            "Test App",
            isSelected: false,
            appxPackageName: "Microsoft.TestApp");
        vm.Items.Add(app);

        var sut = CreateSut(vm);

        var result = await sut.GetSelectedWindowsAppsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSelectedWindowsAppsAsync_WithMixedSelection_ReturnsOnlySelected()
    {
        var vm = CreateWindowsAppsVm();

        _mockWindowsAppsService
            .Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition>());

        await vm.LoadItemsAsync();

        var selectedApp = CreateAppItemViewModel("app1", "Selected App", isSelected: true, appxPackageName: "Microsoft.App1");
        var unselectedApp = CreateAppItemViewModel("app2", "Unselected App", isSelected: false, appxPackageName: "Microsoft.App2");
        var anotherSelectedApp = CreateAppItemViewModel("app3", "Another Selected", isSelected: true, appxPackageName: "Microsoft.App3");

        vm.Items.Add(selectedApp);
        vm.Items.Add(unselectedApp);
        vm.Items.Add(anotherSelectedApp);

        var sut = CreateSut(vm);

        var result = await sut.GetSelectedWindowsAppsAsync();

        result.Should().HaveCount(2);
        result.Select(r => r.Id).Should().Contain("app1");
        result.Select(r => r.Id).Should().Contain("app3");
        result.Select(r => r.Id).Should().NotContain("app2");
    }

    // -------------------------------------------------------
    // GetSelectedWindowsAppsAsync - capability-based apps
    // -------------------------------------------------------

    [Fact]
    public async Task GetSelectedWindowsAppsAsync_WithCapabilityApp_SetsCapabilityName()
    {
        var vm = CreateWindowsAppsVm();

        _mockWindowsAppsService
            .Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition>());

        await vm.LoadItemsAsync();

        var app = CreateAppItemViewModel(
            "cap-app",
            "Capability App",
            isSelected: true,
            capabilityName: "App.WirelessDisplay.Connect~~~~0.0.1.0");
        vm.Items.Add(app);

        var sut = CreateSut(vm);

        var result = await sut.GetSelectedWindowsAppsAsync();

        result.Should().ContainSingle();
        var item = result[0];
        item.CapabilityName.Should().Be("App.WirelessDisplay.Connect~~~~0.0.1.0");
        item.AppxPackageName.Should().BeNull();
        item.OptionalFeatureName.Should().BeNull();
    }

    // -------------------------------------------------------
    // GetSelectedWindowsAppsAsync - optional feature apps
    // -------------------------------------------------------

    [Fact]
    public async Task GetSelectedWindowsAppsAsync_WithOptionalFeatureApp_SetsOptionalFeatureName()
    {
        var vm = CreateWindowsAppsVm();

        _mockWindowsAppsService
            .Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition>());

        await vm.LoadItemsAsync();

        var app = CreateAppItemViewModel(
            "feature-app",
            "Feature App",
            isSelected: true,
            optionalFeatureName: "WindowsMediaPlayer");
        vm.Items.Add(app);

        var sut = CreateSut(vm);

        var result = await sut.GetSelectedWindowsAppsAsync();

        result.Should().ContainSingle();
        var item = result[0];
        item.OptionalFeatureName.Should().Be("WindowsMediaPlayer");
        item.AppxPackageName.Should().BeNull();
        item.CapabilityName.Should().BeNull();
    }

    // -------------------------------------------------------
    // GetSelectedWindowsAppsAsync - appx with sub-packages
    // -------------------------------------------------------

    [Fact]
    public async Task GetSelectedWindowsAppsAsync_WithAppxAndSubPackages_SetsSubPackages()
    {
        var vm = CreateWindowsAppsVm();

        _mockWindowsAppsService
            .Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition>());

        await vm.LoadItemsAsync();

        var subPackages = new[] { "Microsoft.SubPackage1", "Microsoft.SubPackage2" };
        var app = CreateAppItemViewModel(
            "pkg-app",
            "Package App",
            isSelected: true,
            appxPackageName: "Microsoft.MainPackage",
            subPackages: subPackages);
        vm.Items.Add(app);

        var sut = CreateSut(vm);

        var result = await sut.GetSelectedWindowsAppsAsync();

        result.Should().ContainSingle();
        var item = result[0];
        item.AppxPackageName.Should().Be("Microsoft.MainPackage");
        item.SubPackages.Should().BeEquivalentTo(subPackages);
    }

    [Fact]
    public async Task GetSelectedWindowsAppsAsync_WithAppxAndNoSubPackages_DoesNotSetSubPackages()
    {
        var vm = CreateWindowsAppsVm();

        _mockWindowsAppsService
            .Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition>());

        await vm.LoadItemsAsync();

        var app = CreateAppItemViewModel(
            "pkg-app",
            "Package App",
            isSelected: true,
            appxPackageName: "Microsoft.MainPackage",
            subPackages: null);
        vm.Items.Add(app);

        var sut = CreateSut(vm);

        var result = await sut.GetSelectedWindowsAppsAsync();

        result.Should().ContainSingle();
        var item = result[0];
        item.AppxPackageName.Should().Be("Microsoft.MainPackage");
        item.SubPackages.Should().BeNull();
    }

    [Fact]
    public async Task GetSelectedWindowsAppsAsync_WithAppxAndEmptySubPackages_DoesNotSetSubPackages()
    {
        var vm = CreateWindowsAppsVm();

        _mockWindowsAppsService
            .Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition>());

        await vm.LoadItemsAsync();

        var app = CreateAppItemViewModel(
            "pkg-app",
            "Package App",
            isSelected: true,
            appxPackageName: "Microsoft.MainPackage",
            subPackages: Array.Empty<string>());
        vm.Items.Add(app);

        var sut = CreateSut(vm);

        var result = await sut.GetSelectedWindowsAppsAsync();

        result.Should().ContainSingle();
        var item = result[0];
        item.SubPackages.Should().BeNull();
    }

    // -------------------------------------------------------
    // GetSelectedWindowsAppsAsync - priority of package types
    // Appx > Capability > OptionalFeature
    // -------------------------------------------------------

    [Fact]
    public async Task GetSelectedWindowsAppsAsync_WithAppxAndCapability_PrefersAppx()
    {
        var vm = CreateWindowsAppsVm();

        _mockWindowsAppsService
            .Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition>());

        await vm.LoadItemsAsync();

        // An item with both AppxPackageName and CapabilityName set
        // The code checks AppxPackageName first via !string.IsNullOrEmpty
        var definition = new ItemDefinition
        {
            Id = "dual-app",
            Name = "Dual App",
            Description = "Has both",
            AppxPackageName = "Microsoft.DualApp",
            CapabilityName = "DualApp.Capability~~~~0.0.1.0"
        };

        var appVm = new AppItemViewModel(
            definition,
            _mockLocalizationService.Object,
            _mockDispatcherService.Object);
        appVm.IsSelected = true;
        vm.Items.Add(appVm);

        var sut = CreateSut(vm);

        var result = await sut.GetSelectedWindowsAppsAsync();

        result.Should().ContainSingle();
        var item = result[0];
        item.AppxPackageName.Should().Be("Microsoft.DualApp");
        // CapabilityName should NOT be set because the Appx branch ran
        item.CapabilityName.Should().BeNull();
    }

    // -------------------------------------------------------
    // GetSelectedWindowsAppsAsync - loads items when not
    // initialized
    // -------------------------------------------------------

    [Fact]
    public async Task GetSelectedWindowsAppsAsync_WhenNotInitialized_CallsLoadItemsAsync()
    {
        var vm = CreateWindowsAppsVm();

        _mockWindowsAppsService
            .Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition>());

        // Do NOT call LoadItemsAsync - let the provider do it
        var sut = CreateSut(vm);

        var result = await sut.GetSelectedWindowsAppsAsync();

        // The VM should now be initialized
        result.Should().BeEmpty();
        _mockWindowsAppsService.Verify(s => s.GetAppsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetSelectedWindowsAppsAsync_WhenAlreadyInitialized_DoesNotReload()
    {
        var vm = CreateWindowsAppsVm();

        _mockWindowsAppsService
            .Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition>());

        // Pre-initialize the VM
        await vm.LoadItemsAsync();

        var sut = CreateSut(vm);

        await sut.GetSelectedWindowsAppsAsync();

        // Should only have been called once (the explicit call above), not again
        _mockWindowsAppsService.Verify(s => s.GetAppsAsync(), Times.Once);
    }

    // -------------------------------------------------------
    // GetSelectedWindowsAppsAsync - all items set IsSelected
    // and InputType correctly
    // -------------------------------------------------------

    [Fact]
    public async Task GetSelectedWindowsAppsAsync_AllReturnedItems_HaveIsSelectedTrue()
    {
        var vm = CreateWindowsAppsVm();

        _mockWindowsAppsService
            .Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition>());

        await vm.LoadItemsAsync();

        vm.Items.Add(CreateAppItemViewModel("a1", "App 1", isSelected: true, appxPackageName: "pkg1"));
        vm.Items.Add(CreateAppItemViewModel("a2", "App 2", isSelected: true, capabilityName: "cap1"));

        var sut = CreateSut(vm);

        var result = await sut.GetSelectedWindowsAppsAsync();

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(item =>
        {
            item.IsSelected.Should().BeTrue();
            item.InputType.Should().Be(InputType.Toggle);
        });
    }

    // -------------------------------------------------------
    // GetSelectedWindowsAppsAsync - app with no package info
    // -------------------------------------------------------

    [Fact]
    public async Task GetSelectedWindowsAppsAsync_WithNoPackageInfo_ReturnsItemWithoutPackageProperties()
    {
        var vm = CreateWindowsAppsVm();

        _mockWindowsAppsService
            .Setup(s => s.GetAppsAsync())
            .ReturnsAsync(new List<ItemDefinition>());

        await vm.LoadItemsAsync();

        // App with no appx, capability, or optional feature name
        var app = CreateAppItemViewModel(
            "bare-app",
            "Bare App",
            isSelected: true);
        vm.Items.Add(app);

        var sut = CreateSut(vm);

        var result = await sut.GetSelectedWindowsAppsAsync();

        result.Should().ContainSingle();
        var item = result[0];
        item.Id.Should().Be("bare-app");
        item.AppxPackageName.Should().BeNull();
        item.CapabilityName.Should().BeNull();
        item.OptionalFeatureName.Should().BeNull();
    }
}
