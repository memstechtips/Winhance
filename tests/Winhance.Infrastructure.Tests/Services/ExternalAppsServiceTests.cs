using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ExternalAppsServiceTests
{
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IWinGetPackageInstaller> _winGetPackageInstaller = new();
    private readonly Mock<IWinGetDetectionService> _winGetDetectionService = new();
    private readonly Mock<IWinGetBootstrapper> _winGetBootstrapper = new();
    private readonly Mock<IAppStatusDiscoveryService> _appStatusDiscoveryService = new();
    private readonly Mock<IAppUninstallService> _appUninstallService = new();
    private readonly Mock<IDirectDownloadService> _directDownloadService = new();
    private readonly Mock<ITaskProgressService> _taskProgressService = new();
    private readonly Mock<IChocolateyService> _chocolateyService = new();
    private readonly Mock<IChocolateyConsentService> _chocolateyConsentService = new();
    private readonly Mock<IInteractiveUserService> _interactiveUserService = new();
    private readonly Mock<IFileSystemService> _fileSystemService = new();
    private readonly Mock<IProcessExecutor> _processExecutor = new();

    private ExternalAppsService CreateSut() => new(
        _logService.Object,
        _winGetPackageInstaller.Object,
        _winGetDetectionService.Object,
        _winGetBootstrapper.Object,
        _appStatusDiscoveryService.Object,
        _appUninstallService.Object,
        _directDownloadService.Object,
        _taskProgressService.Object,
        _chocolateyService.Object,
        _chocolateyConsentService.Object,
        _interactiveUserService.Object,
        _fileSystemService.Object,
        _processExecutor.Object);

    // --- DomainName ---

    [Fact]
    public void DomainName_ReturnsExternalApps()
    {
        var sut = CreateSut();

        sut.DomainName.Should().Be("ExternalApps");
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
    public async Task GetAppsAsync_AllItemsHaveIds()
    {
        var sut = CreateSut();

        var result = await sut.GetAppsAsync();

        result.Should().OnlyContain(item => !string.IsNullOrEmpty(item.Id));
    }

    // --- InstallAppAsync: WinGet success ---

    [Fact]
    public async Task InstallAppAsync_WinGetSucceeds_ReturnsSuccess()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "ext-app",
            Name = "External App",
            Description = "An external app",
            WinGetPackageId = new[] { "Publisher.ExtApp" }
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.ExtApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ExtApp", "winget", "External App", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task InstallAppAsync_WinGetSucceedsWithMsStoreId_ReturnsSuccess()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "ext-store-app",
            Name = "Store App",
            Description = "An app from the store",
            MsStoreId = "9NBLGGH4NNS1"
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("9NBLGGH4NNS1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("msix");

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "9NBLGGH4NNS1", "msstore", "Store App", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
    }

    // --- InstallAppAsync: direct download ---

    [Fact]
    public async Task InstallAppAsync_RequiresDirectDownload_UsesDirectDownloadService()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "direct-app",
            Name = "Direct App",
            Description = "Needs direct download",
            ExternalApp = new ExternalAppMetadata { RequiresDirectDownload = true }
        };

        _directDownloadService
            .Setup(x => x.DownloadAndInstallAsync(
                item, It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
        _directDownloadService.Verify(x => x.DownloadAndInstallAsync(
            item, It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_DirectDownloadFails_ReturnsFailed()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "direct-app",
            Name = "Direct App",
            Description = "Needs direct download",
            ExternalApp = new ExternalAppMetadata { RequiresDirectDownload = true }
        };

        _directDownloadService
            .Setup(x => x.DownloadAndInstallAsync(
                item, It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Direct download installation failed");
    }

    // --- InstallAppAsync: no package IDs ---

    [Fact]
    public async Task InstallAppAsync_NoPackageIds_ReturnsFailed()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "no-id-app",
            Name = "No ID App",
            Description = "No package IDs"
        };

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No WinGet package ID or Store ID specified");
    }

    // --- InstallAppAsync: Chocolatey fallback ---

    [Fact]
    public async Task InstallAppAsync_WinGetFailsWithChocolateyCandidate_FallsBackToChocolatey()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "choco-app",
            Name = "Choco App",
            Description = "App with choco fallback",
            WinGetPackageId = new[] { "Publisher.ChocoApp" },
            ChocoPackageId = "chocoapp"
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.ChocoApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");

        // WinGet fails with a chocolatey-fallback-eligible reason
        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ChocoApp", "winget", "Choco App", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.HashMismatchOrInstallError, "Hash mismatch"));

        // User consents to Chocolatey
        _chocolateyConsentService
            .Setup(x => x.RequestConsentAsync())
            .ReturnsAsync(true);

        // Chocolatey is already installed
        _chocolateyService
            .Setup(x => x.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Chocolatey install succeeds
        _chocolateyService
            .Setup(x => x.InstallPackageAsync("chocoapp", "Choco App", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
        _chocolateyService.Verify(x => x.InstallPackageAsync("chocoapp", "Choco App", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_ChocolateyNotInstalledAndInstallSucceeds_InstallsChocolateyFirst()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "choco-app",
            Name = "Choco App",
            Description = "App with choco fallback",
            WinGetPackageId = new[] { "Publisher.ChocoApp" },
            ChocoPackageId = "chocoapp"
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.ChocoApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ChocoApp", "winget", "Choco App", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.DownloadError, "Download error"));

        _chocolateyConsentService
            .Setup(x => x.RequestConsentAsync())
            .ReturnsAsync(true);

        _chocolateyService
            .Setup(x => x.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _chocolateyService
            .Setup(x => x.InstallChocolateyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _chocolateyService
            .Setup(x => x.InstallPackageAsync("chocoapp", "Choco App", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
        _chocolateyService.Verify(x => x.InstallChocolateyAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_ChocolateyInstallFails_ReturnsOriginalFailure()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "choco-app",
            Name = "Choco App",
            Description = "App with choco fallback",
            WinGetPackageId = new[] { "Publisher.ChocoApp" },
            ChocoPackageId = "chocoapp"
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.ChocoApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ChocoApp", "winget", "Choco App", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.Other, "WinGet failed"));

        _chocolateyConsentService
            .Setup(x => x.RequestConsentAsync())
            .ReturnsAsync(true);

        _chocolateyService
            .Setup(x => x.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Chocolatey bootstrap fails
        _chocolateyService
            .Setup(x => x.InstallChocolateyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("WinGet failed");
    }

    [Fact]
    public async Task InstallAppAsync_UserDeclinesChocolateyConsent_ReturnsFailed()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "choco-app",
            Name = "Choco App",
            Description = "App with choco fallback",
            WinGetPackageId = new[] { "Publisher.ChocoApp" },
            ChocoPackageId = "chocoapp"
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.ChocoApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ChocoApp", "winget", "Choco App", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.HashMismatchOrInstallError, "Hash mismatch"));

        _chocolateyConsentService
            .Setup(x => x.RequestConsentAsync())
            .ReturnsAsync(false);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
        _chocolateyService.Verify(
            x => x.InstallPackageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InstallAppAsync_WinGetFailsNotChocolateyCandidate_DoesNotFallback()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "choco-app",
            Name = "Choco App",
            Description = "App with choco fallback",
            WinGetPackageId = new[] { "Publisher.ChocoApp" },
            ChocoPackageId = "chocoapp"
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.ChocoApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");

        // PackageNotFound is NOT a chocolatey fallback candidate
        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ChocoApp", "winget", "Choco App", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.PackageNotFound, "Not found"));

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
        _chocolateyConsentService.Verify(x => x.RequestConsentAsync(), Times.Never);
    }

    // --- InstallAppAsync: cancellation ---

    [Fact]
    public async Task InstallAppAsync_OperationCancelled_ReturnsCancelled()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "ext-app",
            Name = "External App",
            Description = "An external app",
            WinGetPackageId = new[] { "Publisher.ExtApp" }
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    // --- UninstallAppAsync ---

    [Fact]
    public async Task UninstallAppAsync_Success_ReturnsSuccess()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "ext-app",
            Name = "External App",
            Description = "An external app"
        };

        _appUninstallService
            .Setup(x => x.UninstallAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        // Mock the file system for shortcut cleanup
        _interactiveUserService
            .Setup(x => x.GetInteractiveUserFolderPath(Environment.SpecialFolder.Programs))
            .Returns(@"C:\Users\Test\AppData\Roaming\Microsoft\Windows\Start Menu\Programs");
        _fileSystemService
            .Setup(x => x.CombinePath(It.IsAny<string[]>()))
            .Returns(@"C:\Users\Test\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\External App");
        _fileSystemService
            .Setup(x => x.DirectoryExists(It.IsAny<string>()))
            .Returns(true);

        var result = await sut.UninstallAppAsync(item);

        result.Success.Should().BeTrue();
        _appUninstallService.Verify(
            x => x.UninstallAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UninstallAppAsync_Failure_ReturnsFailure()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "ext-app",
            Name = "External App",
            Description = "An external app"
        };

        _appUninstallService
            .Setup(x => x.UninstallAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.Failed("Uninstall failed"));

        var result = await sut.UninstallAppAsync(item);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task UninstallAppAsync_OperationCancelled_ReturnsCancelled()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "ext-app",
            Name = "External App",
            Description = "An external app"
        };

        _appUninstallService
            .Setup(x => x.UninstallAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await sut.UninstallAppAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    // --- CheckBatchInstalledAsync ---

    [Fact]
    public async Task CheckBatchInstalledAsync_DelegatesToExternalAppsStatus()
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
            .Setup(x => x.GetExternalAppsInstallationStatusAsync(definitions))
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

    // --- InstallAppAsync: exception handling ---

    [Fact]
    public async Task InstallAppAsync_GenericException_ReturnsFailed()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "ext-app",
            Name = "External App",
            Description = "An external app",
            WinGetPackageId = new[] { "Publisher.ExtApp" }
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Something broke"));

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Something broke");
    }
}
