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

public class ConfigExportServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IDialogService> _mockDialogService = new();
    private readonly Mock<ILocalizationService> _mockLocalizationService = new();
    private readonly Mock<ICompatibleSettingsRegistry> _mockCompatibleSettingsRegistry = new();
    private readonly Mock<IGlobalSettingsPreloader> _mockSettingsPreloader = new();
    private readonly Mock<ISystemSettingsDiscoveryService> _mockDiscoveryService = new();
    private readonly Mock<IInteractiveUserService> _mockInteractiveUserService = new();
    private readonly Mock<IWindowsAppsItemsProvider> _mockWindowsAppsVM = new();
    private readonly Mock<IExternalAppsItemsProvider> _mockExternalAppsVM = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IMainWindowProvider> _mockMainWindowProvider = new();
    private readonly Mock<IDispatcherService> _mockDispatcher = new();

    public ConfigExportServiceTests()
    {
        _mockDispatcher
            .Setup(d => d.RunOnUIThread(It.IsAny<Action>()))
            .Callback<Action>(action => action());

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => string.Format(key, args));
    }

    private ConfigExportService CreateService()
    {
        return new ConfigExportService(
            _mockLogService.Object,
            _mockDialogService.Object,
            _mockLocalizationService.Object,
            _mockCompatibleSettingsRegistry.Object,
            _mockSettingsPreloader.Object,
            _mockDiscoveryService.Object,
            _mockInteractiveUserService.Object,
            _mockWindowsAppsVM.Object,
            _mockExternalAppsVM.Object,
            _mockFileSystemService.Object,
            _mockMainWindowProvider.Object);
    }

    // -------------------------------------------------------
    // CreateConfigurationFromSystemAsync
    // -------------------------------------------------------

    [Fact]
    public async Task CreateConfigurationFromSystemAsync_ReturnsVersion2Config()
    {
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetAllFilteredSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>());

        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockWindowsAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        _mockExternalAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockExternalAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        var service = CreateService();
        var result = await service.CreateConfigurationFromSystemAsync();

        result.Should().NotBeNull();
        result.Version.Should().Be("2.0");
    }

    [Fact]
    public async Task CreateConfigurationFromSystemAsync_WithNoSettings_HasEmptyFeatureSections()
    {
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetAllFilteredSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>());

        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockWindowsAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        _mockExternalAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockExternalAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        var service = CreateService();
        var result = await service.CreateConfigurationFromSystemAsync();

        result.Optimize.Features.Should().BeEmpty();
        result.Customize.Features.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateConfigurationFromSystemAsync_LoadsWindowsAppsWhenNotInitialized()
    {
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetAllFilteredSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>());

        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(false);
        _mockWindowsAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        _mockExternalAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockExternalAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        var service = CreateService();
        await service.CreateConfigurationFromSystemAsync();

        _mockWindowsAppsVM.Verify(v => v.LoadItemsAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateConfigurationFromSystemAsync_IsBackup_ExportsInstalledStatusNotSelectedStatus()
    {
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetAllFilteredSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>());

        var installedApp = CreateAppItemViewModel("app1", "App 1", isInstalled: true, isSelected: false);
        var selectedApp = CreateAppItemViewModel("app2", "App 2", isInstalled: false, isSelected: true);

        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockWindowsAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { installedApp, selectedApp });

        _mockExternalAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockExternalAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        var service = CreateService();
        var result = await service.CreateConfigurationFromSystemAsync(isBackup: true);

        // In backup mode, only installed apps should appear (not selected ones)
        result.WindowsApps.Items.Should().ContainSingle()
            .Which.Id.Should().Be("app1");
    }

    [Fact]
    public async Task CreateConfigurationFromSystemAsync_NotBackup_ExportsSelectedStatus()
    {
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetAllFilteredSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>());

        var installedApp = CreateAppItemViewModel("app1", "App 1", isInstalled: true, isSelected: false);
        var selectedApp = CreateAppItemViewModel("app2", "App 2", isInstalled: false, isSelected: true);

        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockWindowsAppsVM.Setup(v => v.Items)
            .Returns(new ObservableCollection<AppItemViewModel> { installedApp, selectedApp });

        _mockExternalAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockExternalAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        var service = CreateService();
        var result = await service.CreateConfigurationFromSystemAsync(isBackup: false);

        // In normal mode, only selected apps should appear
        result.WindowsApps.Items.Should().ContainSingle()
            .Which.Id.Should().Be("app2");
    }

    [Fact]
    public async Task CreateConfigurationFromSystemAsync_IsBackup_SkipsExternalApps()
    {
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetAllFilteredSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>());

        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockWindowsAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        _mockExternalAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockExternalAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        var service = CreateService();
        var result = await service.CreateConfigurationFromSystemAsync(isBackup: true);

        // External apps are not included in backups
        result.ExternalApps.Items.Should().BeEmpty();
    }

    // -------------------------------------------------------
    // ExportConfigurationAsync
    // -------------------------------------------------------

    [Fact]
    public async Task ExportConfigurationAsync_WhenNoMainWindow_ShowsError()
    {
        _mockCompatibleSettingsRegistry.Setup(r => r.IsInitialized).Returns(true);
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetAllFilteredSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>());

        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockWindowsAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        _mockExternalAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockExternalAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        _mockMainWindowProvider
            .Setup(p => p.MainWindow)
            .Returns((Microsoft.UI.Xaml.Window?)null);

        var service = CreateService();
        await service.ExportConfigurationAsync();

        _mockDialogService.Verify(
            d => d.ShowErrorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    // -------------------------------------------------------
    // CreateUserBackupConfigAsync
    // -------------------------------------------------------

    [Fact]
    public async Task CreateUserBackupConfigAsync_CreatesBackupDirectory()
    {
        _mockCompatibleSettingsRegistry.Setup(r => r.IsInitialized).Returns(true);
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetAllFilteredSettings())
            .Returns(new Dictionary<string, IEnumerable<SettingDefinition>>());

        _mockWindowsAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockWindowsAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        _mockExternalAppsVM.Setup(v => v.IsInitialized).Returns(true);
        _mockExternalAppsVM.Setup(v => v.Items).Returns(new ObservableCollection<AppItemViewModel>());

        _mockInteractiveUserService
            .Setup(s => s.GetInteractiveUserFolderPath(It.IsAny<Environment.SpecialFolder>()))
            .Returns(@"C:\Users\Test\AppData\Local");

        _mockFileSystemService
            .Setup(fs => fs.CombinePath(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(@"C:\Users\Test\AppData\Local\Winhance\Backup");

        _mockFileSystemService
            .Setup(fs => fs.CombinePath(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(@"C:\Users\Test\AppData\Local\Winhance\Backup\UserBackup_test.winhance");

        var service = CreateService();
        await service.CreateUserBackupConfigAsync();

        _mockFileSystemService.Verify(
            fs => fs.CreateDirectory(@"C:\Users\Test\AppData\Local\Winhance\Backup"),
            Times.Once);
    }

    [Fact]
    public async Task CreateUserBackupConfigAsync_WhenExceptionOccurs_LogsError()
    {
        _mockCompatibleSettingsRegistry.Setup(r => r.IsInitialized).Returns(true);
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetAllFilteredSettings())
            .Throws(new Exception("Test error"));

        var service = CreateService();
        await service.CreateUserBackupConfigAsync();

        _mockLogService.Verify(
            l => l.Log(LogLevel.Error, It.Is<string>(s => s.Contains("Test error"))),
            Times.Once);
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    private AppItemViewModel CreateAppItemViewModel(
        string id, string name,
        bool isInstalled = false, bool isSelected = false,
        string? appxPackageName = null)
    {
        var definition = new ItemDefinition
        {
            Id = id,
            Name = name,
            Description = "Test",
            IsInstalled = isInstalled,
            AppxPackageName = appxPackageName ?? $"{id}.Package"
        };

        var vm = new AppItemViewModel(
            definition,
            _mockLocalizationService.Object,
            _mockDispatcher.Object);

        vm.IsSelected = isSelected;
        return vm;
    }
}
