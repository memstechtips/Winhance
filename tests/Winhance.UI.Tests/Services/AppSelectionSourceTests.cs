using System.Collections.ObjectModel;
using FluentAssertions;
using Microsoft.UI.Xaml;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class AppSelectionSourceTests
{
    private static readonly string[] AppxPackages = ["Microsoft.MainPackage", "Microsoft.SubPackage"];
    private static readonly string[] DualAppPackage = ["Microsoft.DualApp"];
    private static readonly string[] TwoWinGetIds = ["Publisher.A", "Publisher.B"];

    private readonly Mock<IWindowsAppsItemsProvider> _windowsApps = new();
    private readonly Mock<IExternalAppsItemsProvider> _externalApps = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<IDispatcherService> _dispatcherService = new();
    private readonly Mock<IThemeService> _themeService = new();

    public AppSelectionSourceTests()
    {
        _dispatcherService
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(action => action());

        _localizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _themeService
            .Setup(t => t.GetEffectiveTheme())
            .Returns(ElementTheme.Dark);

        _windowsApps.Setup(p => p.IsInitialized).Returns(true);
        _windowsApps.Setup(p => p.Items).Returns(new ObservableCollection<AppItemViewModel>());
        _externalApps.Setup(p => p.IsInitialized).Returns(true);
        _externalApps.Setup(p => p.Items).Returns(new ObservableCollection<AppItemViewModel>());
    }

    private AppSelectionSource CreateSut() => new(_windowsApps.Object, _externalApps.Object);

    private AppItemViewModel CreateAppItemViewModel(
        string id, string name,
        bool isSelected = false, bool isInstalled = false,
        string[]? appxPackageName = null,
        string? capabilityName = null,
        string? optionalFeatureName = null,
        string[]? winGetPackageId = null)
    {
        var definition = new ItemDefinition
        {
            Id = id,
            Name = name,
            Description = "Test",
            IsInstalled = isInstalled,
            AppxPackageName = appxPackageName,
            CapabilityName = capabilityName,
            OptionalFeatureName = optionalFeatureName,
            WinGetPackageId = winGetPackageId,
        };

        var vm = new AppItemViewModel(
            definition,
            _localizationService.Object,
            _dispatcherService.Object,
            _themeService.Object);

        vm.IsSelected = isSelected;
        return vm;
    }

    [Fact]
    public async Task CheckedWindowsApps_MapsAppx_ThenCapability_ThenOptionalFeature_InThatPrecedence()
    {
        var appx = CreateAppItemViewModel("appx-app", "Appx App", isSelected: true, appxPackageName: AppxPackages);
        var capability = CreateAppItemViewModel("cap-app", "Capability App", isSelected: true, capabilityName: "App.WirelessDisplay.Connect~~~~0.0.1.0");
        var feature = CreateAppItemViewModel("feature-app", "Feature App", isSelected: true, optionalFeatureName: "WindowsMediaPlayer");
        var notChecked = CreateAppItemViewModel("skip-app", "Skipped App", appxPackageName: DualAppPackage);

        _windowsApps.Setup(p => p.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { appx, capability, feature, notChecked });

        var result = await CreateSut().CheckedWindowsAppsAsync();

        result.Select(a => a.Id).Should().Equal("appx-app", "cap-app", "feature-app");

        result[0].AppxPackageName.Should().BeEquivalentTo(AppxPackages);
        result[0].CapabilityName.Should().BeNull();
        result[0].OptionalFeatureName.Should().BeNull();

        result[1].CapabilityName.Should().Be("App.WirelessDisplay.Connect~~~~0.0.1.0");
        result[1].AppxPackageName.Should().BeNull();
        result[1].OptionalFeatureName.Should().BeNull();

        result[2].OptionalFeatureName.Should().Be("WindowsMediaPlayer");
        result[2].AppxPackageName.Should().BeNull();
        result[2].CapabilityName.Should().BeNull();
    }

    [Fact]
    public async Task CheckedWindowsApps_WithAppxAndCapability_PrefersAppx()
    {
        var dual = CreateAppItemViewModel(
            "dual-app", "Dual App",
            isSelected: true,
            appxPackageName: DualAppPackage,
            capabilityName: "DualApp.Capability~~~~0.0.1.0");

        _windowsApps.Setup(p => p.Items).Returns(new ObservableCollection<AppItemViewModel> { dual });

        var result = await CreateSut().CheckedWindowsAppsAsync();

        result.Should().ContainSingle();
        result[0].AppxPackageName.Should().BeEquivalentTo(DualAppPackage);
        result[0].CapabilityName.Should().BeNull();
    }

    [Fact]
    public async Task InstalledWindowsApps_UsesIsInstalledNotIsSelected()
    {
        var installed = CreateAppItemViewModel("installed", "Installed", isInstalled: true, appxPackageName: AppxPackages);
        var checkedOnly = CreateAppItemViewModel("checked", "Checked", isSelected: true, appxPackageName: AppxPackages);

        _windowsApps.Setup(p => p.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { installed, checkedOnly });

        var result = await CreateSut().InstalledWindowsAppsAsync();

        result.Should().ContainSingle();
        result[0].Id.Should().Be("installed");
    }

    [Fact]
    public async Task CheckedExternalApps_UsesFirstWinGetPackageId()
    {
        var twoIds = CreateAppItemViewModel("two-ids", "Two Ids", isSelected: true, winGetPackageId: TwoWinGetIds);
        var noIds = CreateAppItemViewModel("no-ids", "No Ids", isSelected: true);

        _externalApps.Setup(p => p.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { twoIds, noIds });

        var result = await CreateSut().CheckedExternalAppsAsync();

        result.Should().HaveCount(2);
        result[0].WinGetPackageId.Should().Be("Publisher.A");
        result[1].WinGetPackageId.Should().BeNull();
    }

    [Fact]
    public async Task CheckedExternalApps_SkipsUncheckedItems()
    {
        var notChecked = CreateAppItemViewModel("skip", "Skipped", winGetPackageId: TwoWinGetIds);

        _externalApps.Setup(p => p.Items).Returns(new ObservableCollection<AppItemViewModel> { notChecked });

        var result = await CreateSut().CheckedExternalAppsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadsItemsWhenNotInitialized()
    {
        _windowsApps.Setup(p => p.IsInitialized).Returns(false);
        _externalApps.Setup(p => p.IsInitialized).Returns(false);

        var sut = CreateSut();
        await sut.CheckedWindowsAppsAsync();
        await sut.CheckedExternalAppsAsync();

        _windowsApps.Verify(p => p.LoadItemsAsync(), Times.Once);
        _externalApps.Verify(p => p.LoadItemsAsync(), Times.Once);
    }

    [Fact]
    public async Task InstalledWindowsApps_LoadsItemsWhenNotInitialized()
    {
        _windowsApps.Setup(p => p.IsInitialized).Returns(false);

        await CreateSut().InstalledWindowsAppsAsync();

        _windowsApps.Verify(p => p.LoadItemsAsync(), Times.Once);
    }

    [Fact]
    public async Task DoesNotLoadItemsWhenAlreadyInitialized()
    {
        var sut = CreateSut();
        await sut.CheckedWindowsAppsAsync();
        await sut.InstalledWindowsAppsAsync();
        await sut.CheckedExternalAppsAsync();

        _windowsApps.Verify(p => p.LoadItemsAsync(), Times.Never);
        _externalApps.Verify(p => p.LoadItemsAsync(), Times.Never);
    }
}
