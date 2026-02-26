using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Common.Services;
using Winhance.UI.Features.SoftwareApps.ViewModels;
using System.Collections.ObjectModel;
using Xunit;

namespace Winhance.UI.Tests.Services;

public class ConfigAppSelectionServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IWindowsAppsItemsProvider> _mockWindowsAppsVM = new();
    private readonly Mock<IExternalAppsItemsProvider> _mockExternalAppsVM = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<IDispatcherService> _mockDispatcher = new();

    public ConfigAppSelectionServiceTests()
    {
        _mockDispatcher
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(action => action());

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
    }

    private ConfigAppSelectionService CreateService()
    {
        return new ConfigAppSelectionService(
            _mockLogService.Object,
            _mockWindowsAppsVM.Object,
            _mockExternalAppsVM.Object);
    }

    private AppItemViewModel CreateAppItemViewModel(
        string id, string name,
        string? appxPackageName = null,
        string? capabilityName = null,
        string? optionalFeatureName = null,
        string[]? winGetPackageId = null)
    {
        var definition = new ItemDefinition
        {
            Id = id,
            Name = name,
            Description = "Test",
            AppxPackageName = appxPackageName,
            CapabilityName = capabilityName,
            OptionalFeatureName = optionalFeatureName,
            WinGetPackageId = winGetPackageId
        };

        return new AppItemViewModel(
            definition,
            _mockLocalizationService.Object,
            _mockDispatcher.Object);
    }

    // -------------------------------------------------------
    // SelectWindowsAppsFromConfigAsync
    // -------------------------------------------------------

    [Fact]
    public async Task SelectWindowsAppsFromConfigAsync_LoadsItemsIfNotInitialized()
    {
        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(false);
        _mockWindowsAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        var service = CreateService();
        await service.SelectWindowsAppsFromConfigAsync(new ConfigSection());

        _mockWindowsAppsVM.Verify(v => v.LoadItemsAsync(), Times.Once);
    }

    [Fact]
    public async Task SelectWindowsAppsFromConfigAsync_SkipsLoadIfAlreadyInitialized()
    {
        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockWindowsAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        var service = CreateService();
        await service.SelectWindowsAppsFromConfigAsync(new ConfigSection());

        _mockWindowsAppsVM.Verify(v => v.LoadItemsAsync(), Times.Never);
    }

    [Fact]
    public async Task SelectWindowsAppsFromConfigAsync_ClearsAllSelectionsFirst()
    {
        var app1 = CreateAppItemViewModel("app1", "App 1", appxPackageName: "Package1");
        app1.IsSelected = true;
        var app2 = CreateAppItemViewModel("app2", "App 2", appxPackageName: "Package2");
        app2.IsSelected = true;

        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockWindowsAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { app1, app2 });

        var emptyConfig = new ConfigSection();
        var service = CreateService();
        await service.SelectWindowsAppsFromConfigAsync(emptyConfig);

        app1.IsSelected.Should().BeFalse();
        app2.IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task SelectWindowsAppsFromConfigAsync_MatchesByAppxPackageName()
    {
        var app = CreateAppItemViewModel("app1", "App 1", appxPackageName: "Microsoft.TestApp");

        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockWindowsAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { app });

        var configSection = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                new ConfigurationItem
                {
                    Id = "different-id",
                    Name = "Different Name",
                    AppxPackageName = "Microsoft.TestApp",
                    IsSelected = true
                }
            }
        };

        var service = CreateService();
        await service.SelectWindowsAppsFromConfigAsync(configSection);

        app.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task SelectWindowsAppsFromConfigAsync_MatchesByCapabilityName()
    {
        var app = CreateAppItemViewModel("app1", "App 1", capabilityName: "TestCapability");

        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockWindowsAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { app });

        var configSection = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                new ConfigurationItem
                {
                    Id = "other-id",
                    Name = "Other Name",
                    CapabilityName = "TestCapability",
                    IsSelected = true
                }
            }
        };

        var service = CreateService();
        await service.SelectWindowsAppsFromConfigAsync(configSection);

        app.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task SelectWindowsAppsFromConfigAsync_MatchesByOptionalFeatureName()
    {
        var app = CreateAppItemViewModel("app1", "App 1", optionalFeatureName: "TestFeature");

        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockWindowsAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { app });

        var configSection = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                new ConfigurationItem
                {
                    Id = "other-id",
                    Name = "Other Name",
                    OptionalFeatureName = "TestFeature",
                    IsSelected = true
                }
            }
        };

        var service = CreateService();
        await service.SelectWindowsAppsFromConfigAsync(configSection);

        app.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task SelectWindowsAppsFromConfigAsync_MatchesById()
    {
        var app = CreateAppItemViewModel("matching-id", "App 1");

        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockWindowsAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { app });

        var configSection = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                new ConfigurationItem { Id = "matching-id", Name = "Config App", IsSelected = true }
            }
        };

        var service = CreateService();
        await service.SelectWindowsAppsFromConfigAsync(configSection);

        app.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task SelectWindowsAppsFromConfigAsync_RespectsIsSelectedFalse()
    {
        var app = CreateAppItemViewModel("app1", "App 1", appxPackageName: "Package1");
        app.IsSelected = true; // Start selected

        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockWindowsAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { app });

        var configSection = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                new ConfigurationItem
                {
                    Id = "app1",
                    Name = "App 1",
                    AppxPackageName = "Package1",
                    IsSelected = false
                }
            }
        };

        var service = CreateService();
        await service.SelectWindowsAppsFromConfigAsync(configSection);

        // Cleared first, then set to IsSelected=false from config
        app.IsSelected.Should().BeFalse();
    }

    [Fact]
    public async Task SelectWindowsAppsFromConfigAsync_WithNullSection_ClearsSelections()
    {
        var app = CreateAppItemViewModel("app1", "App 1");
        app.IsSelected = true;

        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockWindowsAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { app });

        var emptySection = new ConfigSection(); // null Items

        var service = CreateService();
        await service.SelectWindowsAppsFromConfigAsync(emptySection);

        app.IsSelected.Should().BeFalse();
    }

    // -------------------------------------------------------
    // ConfirmWindowsAppsRemovalAsync
    // -------------------------------------------------------

    [Fact]
    public async Task ConfirmWindowsAppsRemovalAsync_WhenNoAppsSelected_ReturnsTrueWithSaveScripts()
    {
        _mockWindowsAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        var service = CreateService();
        var (shouldContinue, saveScripts) = await service.ConfirmWindowsAppsRemovalAsync();

        shouldContinue.Should().BeTrue();
        saveScripts.Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmWindowsAppsRemovalAsync_WhenAppsSelected_DelegatesToProvider()
    {
        var app = CreateAppItemViewModel("app1", "App 1");
        app.IsSelected = true;

        _mockWindowsAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { app });
        _mockWindowsAppsVM
            .Setup(v => v.ShowRemovalSummaryAndConfirm())
            .ReturnsAsync((true, false));

        var service = CreateService();
        var (shouldContinue, saveScripts) = await service.ConfirmWindowsAppsRemovalAsync();

        shouldContinue.Should().BeTrue();
        saveScripts.Should().BeFalse();
        _mockWindowsAppsVM.Verify(v => v.ShowRemovalSummaryAndConfirm(), Times.Once);
    }

    // -------------------------------------------------------
    // ClearWindowsAppsSelectionAsync
    // -------------------------------------------------------

    [Fact]
    public async Task ClearWindowsAppsSelectionAsync_UnselectsAllApps()
    {
        var app1 = CreateAppItemViewModel("app1", "App 1");
        app1.IsSelected = true;
        var app2 = CreateAppItemViewModel("app2", "App 2");
        app2.IsSelected = true;

        _mockWindowsAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { app1, app2 });

        var service = CreateService();
        await service.ClearWindowsAppsSelectionAsync();

        app1.IsSelected.Should().BeFalse();
        app2.IsSelected.Should().BeFalse();
    }

    // -------------------------------------------------------
    // SelectExternalAppsFromConfigAsync
    // -------------------------------------------------------

    [Fact]
    public async Task SelectExternalAppsFromConfigAsync_LoadsItemsIfNotInitialized()
    {
        _mockExternalAppsVM.Setup(v => v.IsInitialized).Returns(false);
        _mockExternalAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        var service = CreateService();
        await service.SelectExternalAppsFromConfigAsync(new ConfigSection());

        _mockExternalAppsVM.Verify(v => v.LoadItemsAsync(), Times.Once);
    }

    [Fact]
    public async Task SelectExternalAppsFromConfigAsync_MatchesByWinGetPackageId()
    {
        var app = CreateAppItemViewModel("ext1", "External App 1",
            winGetPackageId: new[] { "Publisher.AppName" });

        _mockExternalAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockExternalAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { app });

        var configSection = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                new ConfigurationItem
                {
                    Id = "different-id",
                    Name = "Different Name",
                    WinGetPackageId = "Publisher.AppName",
                    IsSelected = true
                }
            }
        };

        var service = CreateService();
        await service.SelectExternalAppsFromConfigAsync(configSection);

        app.IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task SelectExternalAppsFromConfigAsync_MatchesById()
    {
        var app = CreateAppItemViewModel("ext1", "External App 1");

        _mockExternalAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockExternalAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { app });

        var configSection = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                new ConfigurationItem { Id = "ext1", Name = "External App", IsSelected = true }
            }
        };

        var service = CreateService();
        await service.SelectExternalAppsFromConfigAsync(configSection);

        app.IsSelected.Should().BeTrue();
    }

    // -------------------------------------------------------
    // ProcessExternalAppsInstallationAsync
    // -------------------------------------------------------

    [Fact]
    public async Task ProcessExternalAppsInstallationAsync_WhenSelectedApps_CallsInstallApps()
    {
        var app = CreateAppItemViewModel("ext1", "External App 1");

        _mockExternalAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockExternalAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { app });

        var configSection = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                new ConfigurationItem { Id = "ext1", Name = "External App", IsSelected = true }
            }
        };

        var service = CreateService();
        await service.ProcessExternalAppsInstallationAsync(configSection);

        _mockExternalAppsVM.Verify(v => v.InstallApps(true), Times.Once);
    }

    [Fact]
    public async Task ProcessExternalAppsInstallationAsync_WhenNoMatch_DoesNotInstall()
    {
        var app = CreateAppItemViewModel("ext1", "External App 1");

        _mockExternalAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockExternalAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { app });

        var configSection = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                new ConfigurationItem { Id = "no-match", Name = "No Match" }
            }
        };

        var service = CreateService();
        await service.ProcessExternalAppsInstallationAsync(configSection);

        _mockExternalAppsVM.Verify(v => v.InstallApps(It.IsAny<bool>()), Times.Never);
    }

    // -------------------------------------------------------
    // ProcessExternalAppsRemovalAsync
    // -------------------------------------------------------

    [Fact]
    public async Task ProcessExternalAppsRemovalAsync_WhenSelectedApps_CallsUninstall()
    {
        var app = CreateAppItemViewModel("ext1", "External App 1");

        _mockExternalAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockExternalAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { app });

        var configSection = new ConfigSection
        {
            Items = new List<ConfigurationItem>
            {
                new ConfigurationItem { Id = "ext1", Name = "External App" }
            }
        };

        var service = CreateService();
        await service.ProcessExternalAppsRemovalAsync(configSection);

        _mockExternalAppsVM.Verify(v => v.UninstallAppsAsync(), Times.Once);
    }

    // -------------------------------------------------------
    // ProcessExternalAppsFromUserSelectionAsync
    // -------------------------------------------------------

    [Fact]
    public async Task ProcessExternalAppsFromUserSelectionAsync_SelectsByIdList()
    {
        var app1 = CreateAppItemViewModel("ext1", "App 1");
        var app2 = CreateAppItemViewModel("ext2", "App 2");

        _mockExternalAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockExternalAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { app1, app2 });

        var service = CreateService();
        await service.ProcessExternalAppsFromUserSelectionAsync(new List<string> { "ext1" });

        app1.IsSelected.Should().BeTrue();
        app2.IsSelected.Should().BeFalse();
        _mockExternalAppsVM.Verify(v => v.InstallApps(true), Times.Once);
    }

    [Fact]
    public async Task ProcessExternalAppsFromUserSelectionAsync_WhenNoMatch_DoesNotInstall()
    {
        var app1 = CreateAppItemViewModel("ext1", "App 1");

        _mockExternalAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockExternalAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { app1 });

        var service = CreateService();
        await service.ProcessExternalAppsFromUserSelectionAsync(new List<string> { "nonexistent" });

        app1.IsSelected.Should().BeFalse();
        _mockExternalAppsVM.Verify(v => v.InstallApps(It.IsAny<bool>()), Times.Never);
    }
}
