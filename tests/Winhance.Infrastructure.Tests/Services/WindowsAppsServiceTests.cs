using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class WindowsAppsServiceTests
{
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IWinGetPackageInstaller> _winGetPackageInstaller = new();
    private readonly Mock<IWinGetBootstrapper> _winGetBootstrapper = new();
    private readonly Mock<IAppStatusDiscoveryService> _appStatusDiscoveryService = new();
    private readonly Mock<IStoreDownloadService> _storeDownloadService = new();
    private readonly Mock<IDialogService> _dialogService = new();
    private readonly Mock<IUserPreferencesService> _userPreferencesService = new();
    private readonly Mock<ITaskProgressService> _taskProgressService = new();
    private readonly Mock<ILocalizationService> _localizationService = new();
    private readonly Mock<ISettingApplicationService> _settingApplicationService = new();
    private readonly Mock<ISystemSettingsDiscoveryService> _systemSettingsDiscoveryService = new();

    private WindowsAppsService CreateSut() => new(
        _logService.Object,
        _winGetPackageInstaller.Object,
        _winGetBootstrapper.Object,
        _appStatusDiscoveryService.Object,
        _storeDownloadService.Object,
        _dialogService.Object,
        _userPreferencesService.Object,
        _taskProgressService.Object,
        _localizationService.Object,
        _settingApplicationService.Object,
        _systemSettingsDiscoveryService.Object);

    // --- DomainName ---

    [Fact]
    public void DomainName_ReturnsWindowsApps()
    {
        var sut = CreateSut();

        sut.DomainName.Should().Be("WindowsApps");
    }

    // --- GetAppsAsync ---

    [Fact]
    public async Task GetAppsAsync_ReturnsNonEmptyList()
    {
        var sut = CreateSut();

        var result = await sut.GetAppsAsync();

        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAppsAsync_ContainsItemsWithIds()
    {
        var sut = CreateSut();

        var result = await sut.GetAppsAsync();

        result.Should().OnlyContain(item => !string.IsNullOrEmpty(item.Id));
    }

    // --- InstallAppAsync: success via WinGet ---

    [Fact]
    public async Task InstallAppAsync_WinGetSucceeds_ReturnsSuccess()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "test-app",
            Name = "Test App",
            Description = "A test app",
            MsStoreId = "9NBLGGH4NNS1"
        };

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "9NBLGGH4NNS1", "msstore", "Test App", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task InstallAppAsync_WinGetSucceedsWithWinGetPackageId_ReturnsSuccess()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "test-app",
            Name = "Test App",
            Description = "A test app",
            WinGetPackageId = new[] { "Publisher.TestApp" }
        };

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.TestApp", "winget", "Test App", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
    }

    // --- InstallAppAsync: failure / unsupported ---

    [Fact]
    public async Task InstallAppAsync_NoPackageInfo_ReturnsFailed()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "unsupported",
            Name = "Unsupported",
            Description = "No IDs"
        };

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not supported");
    }

    [Fact]
    public async Task InstallAppAsync_WinGetFailsAndUserDeclinesConsent_ReturnsFailed()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "test-app",
            Name = "Test App",
            Description = "A test app",
            MsStoreId = "9NBLGGH4NNS1"
        };

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.Other, "Install failed"));

        // Update policy is not disabled (no blocking)
        _systemSettingsDiscoveryService
            .Setup(x => x.GetSettingStatesAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());

        // User has NOT opted to skip confirmation
        _userPreferencesService
            .Setup(x => x.GetPreferenceAsync("StoreDownloadFallback_DontShowAgain", false))
            .ReturnsAsync(false);

        // User declines fallback dialog
        _dialogService
            .Setup(x => x.ShowConfirmationWithCheckboxAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((false, false));

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled by user");
    }

    [Fact]
    public async Task InstallAppAsync_WinGetFailsAndFallbackSucceeds_ReturnsSuccess()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "test-app",
            Name = "Test App",
            Description = "A test app",
            MsStoreId = "9NBLGGH4NNS1"
        };

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.Other, "Install failed"));

        _systemSettingsDiscoveryService
            .Setup(x => x.GetSettingStatesAsync(It.IsAny<IEnumerable<SettingDefinition>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());

        // User has previously opted to skip confirmation
        _userPreferencesService
            .Setup(x => x.GetPreferenceAsync("StoreDownloadFallback_DontShowAgain", false))
            .ReturnsAsync(true);

        // Fallback download succeeds
        _storeDownloadService
            .Setup(x => x.DownloadAndInstallPackageAsync(
                "9NBLGGH4NNS1", "Test App", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task InstallAppAsync_ExceptionThrown_ReturnsFailed()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "test-app",
            Name = "Test App",
            Description = "A test app",
            MsStoreId = "9NBLGGH4NNS1"
        };

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Unexpected error");
    }

    [Fact]
    public async Task InstallAppAsync_OperationCancelled_ReturnsCancelled()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "test-app",
            Name = "Test App",
            Description = "A test app",
            MsStoreId = "9NBLGGH4NNS1"
        };

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    // --- CheckBatchInstalledAsync ---

    [Fact]
    public async Task CheckBatchInstalledAsync_DelegatesToDiscoveryService()
    {
        var sut = CreateSut();
        var definitions = new List<ItemDefinition>
        {
            new() { Id = "app1", Name = "App1", Description = "Desc1" },
            new() { Id = "app2", Name = "App2", Description = "Desc2" }
        };

        var expected = new Dictionary<string, bool>
        {
            { "app1", true },
            { "app2", false }
        };

        _appStatusDiscoveryService
            .Setup(x => x.GetInstallationStatusBatchAsync(definitions))
            .ReturnsAsync(expected);

        var result = await sut.CheckBatchInstalledAsync(definitions);

        result.Should().BeEquivalentTo(expected);
    }

    // --- InvalidateStatusCache ---

    [Fact]
    public void InvalidateStatusCache_DelegatesToDiscoveryService()
    {
        var sut = CreateSut();

        sut.InvalidateStatusCache();

        _appStatusDiscoveryService.Verify(x => x.InvalidateCache(), Times.Once);
    }

    // --- GetAppByIdAsync ---

    [Fact]
    public async Task GetAppByIdAsync_ExistingApp_ReturnsApp()
    {
        var sut = CreateSut();
        var allApps = await sut.GetAppsAsync();
        var firstApp = allApps.First();

        var result = await sut.GetAppByIdAsync(firstApp.Id);

        result.Should().NotBeNull();
        result!.Id.Should().Be(firstApp.Id);
    }

    [Fact]
    public async Task GetAppByIdAsync_NonExistentApp_ReturnsNull()
    {
        var sut = CreateSut();

        var result = await sut.GetAppByIdAsync("non-existent-app-id-12345");

        result.Should().BeNull();
    }

    // --- InstallAppAsync with AppxPackageName (no MsStoreId/WinGetPackageId) ---

    [Fact]
    public async Task InstallAppAsync_OnlyAppxPackageName_UsesItAsPackageId()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "test-appx",
            Name = "Test Appx",
            Description = "An appx app",
            AppxPackageName = "Microsoft.TestApp"
        };

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Microsoft.TestApp", null, "Test Appx", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            "Microsoft.TestApp", null, "Test Appx", It.IsAny<CancellationToken>()), Times.Once);
    }
}
