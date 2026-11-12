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
    private readonly Mock<IInstallConsent> _installConsent = new();
    private readonly Mock<ITaskProgressService> _taskProgressService = new();
    private readonly Mock<ISettingApplicationService> _settingApplicationService = new();
    private readonly Mock<ICatalogSettingStateProvider> _settingStateProvider = new();
    private readonly Mock<IWindowsRegistryService> _registry = new();

    private WindowsAppsService CreateSut() => new(
        _logService.Object,
        _winGetPackageInstaller.Object,
        _installConsent.Object,
        _taskProgressService.Object,
        _settingApplicationService.Object,
        _settingStateProvider.Object,
        _registry.Object);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetAppsAsync_GameBar_CarriesItsWarningOnlyWhereTheAmdX3dServiceIsInstalled(bool installed)
    {
        _registry.Setup(r => r.KeyExists(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\amd3dvcacheSvc"))
            .Returns(installed);

        var gameBar = (await CreateSut().GetAppsAsync()).Single(app => app.Id == "windows-app-xbox-game-bar");

        gameBar.HasInstabilityWarning.Should().Be(installed);
        gameBar.MachineWarning!.TooltipKey.Should().Be("Card_Pill_X3DGameBarWarning_Tooltip");
    }

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
                "9NBLGGH4NNS1", "msstore", "Test App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
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
                "Publisher.TestApp", "winget", "Test App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
    }

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
    public async Task InstallAppAsync_WinGetFails_ReturnsFailed()
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
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.Other, "Install failed"));

        _settingStateProvider
            .Setup(x => x.GetStatesAsync(It.IsAny<IReadOnlyList<Winhance.Core.Features.Common.Catalog.Setting>>()))
            .ReturnsAsync(new Dictionary<string, SettingStateResult>());

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Test App");
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
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
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
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

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

    [Fact]
    public async Task InstallAppAsync_OnlyAppxPackageName_UsesItAsPackageId()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "test-appx",
            Name = "Test Appx",
            Description = "An appx app",
            AppxPackageName = new[] { "Microsoft.TestApp" }
        };

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Microsoft.TestApp", null, "Test Appx", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            "Microsoft.TestApp", null, "Test Appx", It.IsAny<string?>(), InstallOptions.Default, It.IsAny<CancellationToken>()), Times.Once);
    }
}
