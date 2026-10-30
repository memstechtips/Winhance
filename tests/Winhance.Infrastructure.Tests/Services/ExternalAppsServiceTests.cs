using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Catalogs;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ExternalAppsServiceTests
{
    private static readonly ItemDefinition WinGetApp = new()
    {
        Id = "ext-app",
        Name = "External App",
        Description = "An external app",
        WinGetPackageId = ["Publisher.ExtApp"]
    };

    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IWinGetPackageInstaller> _winGetPackageInstaller = new();
    private readonly Mock<IWinGetDetectionService> _winGetDetectionService = new();
    private readonly Mock<IWinGetBootstrapper> _winGetBootstrapper = new();
    private readonly Mock<IAppStatusDiscoveryService> _appStatusDiscoveryService = new();
    private readonly Mock<IExternalAppUninstallService> _externalAppUninstallService = new();
    private readonly Mock<IDirectDownloadService> _directDownloadService = new();
    private readonly Mock<ITaskProgressService> _taskProgressService = new();
    private readonly Mock<IChocolateyService> _chocolateyService = new();
    private readonly Mock<IInteractiveUserService> _interactiveUserService = new();
    private readonly Mock<IFileSystemService> _fileSystemService = new();
    private readonly Mock<IProcessExecutor> _processExecutor = new();
    private readonly Mock<IChangeHistoryService> _changeHistoryService = new();
    private readonly Mock<IInstallOptionsService> _installOptions = new();

    private static readonly InstallOptions MachineScope = new() { Scope = InstallScope.Machine };

    private static readonly InstallOptions AllOptions = new()
    {
        Scope = InstallScope.User,
        Architecture = InstallArchitecture.X86,
        Interactive = true,
    };

    public ExternalAppsServiceTests()
    {
        _installOptions.Setup(x => x.EffectiveFor(It.IsAny<string>())).Returns(InstallOptions.Default);
    }

    private ExternalAppsService CreateSut() => new(
        _logService.Object,
        _winGetPackageInstaller.Object,
        _winGetDetectionService.Object,
        _winGetBootstrapper.Object,
        _appStatusDiscoveryService.Object,
        _externalAppUninstallService.Object,
        _directDownloadService.Object,
        _taskProgressService.Object,
        _chocolateyService.Object,
        _interactiveUserService.Object,
        _fileSystemService.Object,
        _processExecutor.Object,
        _changeHistoryService.Object,
        _installOptions.Object);

    [Fact]
    public void DomainName_ReturnsExternalApps()
    {
        var sut = CreateSut();

        sut.DomainName.Should().Be("ExternalApps");
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
    public async Task GetAppsAsync_AllItemsHaveIds()
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
                "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
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
                "9NBLGGH4NNS1", "msstore", "Store App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
    }

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

    [Fact]
    public async Task InstallAppAsync_NoPackageIds_NoDownloadUrl_ReturnsFailed()
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
        result.ErrorMessage.Should().Contain("Installation failed");
    }

    [Fact]
    public async Task InstallAppAsync_NoPackageIds_WithDownloadUrl_FallsBackToDirectDownload()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "download-only-app",
            Name = "Download Only App",
            Description = "Has only a download URL",
            ExternalApp = new ExternalAppMetadata
            {
                DownloadUrl = "https://example.com/installer.exe"
            }
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
    public async Task InstallAppAsync_WithBothWinGetAndMsStore_TriesWinGetFirst()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "dual-source-app",
            Name = "Dual Source App",
            Description = "Has both WinGet and MsStore IDs",
            WinGetPackageId = new[] { "Publisher.DualApp" },
            MsStoreId = "9NBLGGH12345"
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.DualApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.DualApp", "winget", "Dual Source App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            "Publisher.DualApp", "winget", "Dual Source App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            "9NBLGGH12345", "msstore", It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InstallAppAsync_WinGetFailsWithBothSources_FallsToMsStore()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "dual-source-app",
            Name = "Dual Source App",
            Description = "Has both WinGet and MsStore IDs",
            WinGetPackageId = new[] { "Publisher.DualApp" },
            MsStoreId = "9NBLGGH12345"
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.DualApp", "winget", "Dual Source App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.PackageNotFound, "Not found"));

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "9NBLGGH12345", "msstore", "Dual Source App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            "9NBLGGH12345", "msstore", "Dual Source App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_WinGetFailsWithChocolateyPackage_FallsBackToChocolatey()
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
                "Publisher.ChocoApp", "winget", "Choco App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.HashMismatchOrInstallError, "Hash mismatch"));

        _chocolateyService
            .Setup(x => x.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _chocolateyService
            .Setup(x => x.InstallPackageAsync("chocoapp", "Choco App", It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
        _chocolateyService.Verify(x => x.InstallPackageAsync("chocoapp", "Choco App", It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_ChocoOnlyApp_InstallsViaChocolatey()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "choco-only-app",
            Name = "Choco Only App",
            Description = "App with only a choco package ID",
            ChocoPackageId = "chocoonlyapp"
        };

        _chocolateyService
            .Setup(x => x.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _chocolateyService
            .Setup(x => x.InstallPackageAsync("chocoonlyapp", "Choco Only App", It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
        _chocolateyService.Verify(x => x.InstallPackageAsync("chocoonlyapp", "Choco Only App", It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_ChocoOnlyApp_InstallsChocolateyFirst()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "choco-only-app",
            Name = "Choco Only App",
            Description = "App with only a choco package ID",
            ChocoPackageId = "chocoonlyapp"
        };

        _chocolateyService
            .Setup(x => x.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _chocolateyService
            .Setup(x => x.InstallChocolateyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _chocolateyService
            .Setup(x => x.InstallPackageAsync("chocoonlyapp", "Choco Only App", It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
        _chocolateyService.Verify(x => x.InstallChocolateyAsync(It.IsAny<CancellationToken>()), Times.Once);
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
                "Publisher.ChocoApp", "winget", "Choco App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.DownloadError, "Download error"));

        _chocolateyService
            .Setup(x => x.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _chocolateyService
            .Setup(x => x.InstallChocolateyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _chocolateyService
            .Setup(x => x.InstallPackageAsync("chocoapp", "Choco App", It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
        _chocolateyService.Verify(x => x.InstallChocolateyAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_ChocolateyInstallFails_NoDownloadUrl_ReturnsFailed()
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
                "Publisher.ChocoApp", "winget", "Choco App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.Other, "WinGet failed"));

        _chocolateyService
            .Setup(x => x.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _chocolateyService
            .Setup(x => x.InstallChocolateyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task InstallAppAsync_ChocolateyInstallFails_WithDownloadUrl_FallsBackToDirectDownload()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "choco-app",
            Name = "Choco App",
            Description = "App with choco and download fallback",
            WinGetPackageId = new[] { "Publisher.ChocoApp" },
            ChocoPackageId = "chocoapp",
            ExternalApp = new ExternalAppMetadata
            {
                DownloadUrl = "https://example.com/chocoapp.exe"
            }
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.ChocoApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ChocoApp", "winget", "Choco App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.Other, "WinGet failed"));

        _chocolateyService
            .Setup(x => x.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _chocolateyService
            .Setup(x => x.InstallChocolateyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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
    public async Task InstallAppAsync_WinGetFails_FallsBackToDirectDownload()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "fallback-app",
            Name = "Fallback App",
            Description = "App with direct download fallback",
            WinGetPackageId = new[] { "Publisher.FallbackApp" },
            ExternalApp = new ExternalAppMetadata
            {
                DownloadUrl = "https://example.com/fallback.exe"
            }
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.FallbackApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.FallbackApp", "winget", "Fallback App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.PackageNotFound, "Not found"));

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
    public async Task InstallAppAsync_AllSourcesFail_NoDownloadUrl_ReturnsFailed()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "all-fail-app",
            Name = "All Fail App",
            Description = "App without download fallback",
            WinGetPackageId = new[] { "Publisher.AllFailApp" }
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.AllFailApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.AllFailApp", "winget", "All Fail App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.Other, "Failed"));

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
        _directDownloadService.Verify(x => x.DownloadAndInstallAsync(
            It.IsAny<ItemDefinition>(), It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InstallAppAsync_DirectDownloadFallbackFails_ReturnsFailed()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "fallback-fail-app",
            Name = "Fallback Fail App",
            Description = "App where direct download also fails",
            WinGetPackageId = new[] { "Publisher.FallbackFailApp" },
            ExternalApp = new ExternalAppMetadata
            {
                DownloadUrl = "https://example.com/failapp.exe"
            }
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.FallbackFailApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.FallbackFailApp", "winget", "Fallback Fail App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.Other, "WinGet failed"));

        _directDownloadService
            .Setup(x => x.DownloadAndInstallAsync(
                item, It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task InstallAppAsync_DirectDownloadFallbackThrows_ReturnsFailed()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "fallback-throw-app",
            Name = "Fallback Throw App",
            Description = "App where direct download throws",
            WinGetPackageId = new[] { "Publisher.FallbackThrowApp" },
            ExternalApp = new ExternalAppMetadata
            {
                DownloadUrl = "https://example.com/throwapp.exe"
            }
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.FallbackThrowApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.FallbackThrowApp", "winget", "Fallback Throw App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.Other, "WinGet failed"));

        _directDownloadService
            .Setup(x => x.DownloadAndInstallAsync(
                item, It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Network error"));

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task InstallAppAsync_WinGetAndChocoFail_FallsBackToDirectDownload()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "full-fallback-app",
            Name = "Full Fallback App",
            Description = "App where WinGet and Choco fail but download works",
            WinGetPackageId = new[] { "Publisher.FullFallbackApp" },
            ChocoPackageId = "fullfallbackapp",
            ExternalApp = new ExternalAppMetadata
            {
                DownloadUrl = "https://example.com/fullfallback.exe"
            }
        };

        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.FullFallbackApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");

        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.FullFallbackApp", "winget", "Full Fallback App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.Other, "WinGet failed"));

        _chocolateyService
            .Setup(x => x.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _chocolateyService
            .Setup(x => x.InstallPackageAsync("fullfallbackapp", "Full Fallback App", It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

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

    [Fact]
    public async Task InstallAppOnNewPcAsync_MachineWideSucceeds_SkipsTheDefaultScope()
    {
        var sut = CreateSut();
        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.ExtApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");
        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), MachineScope, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        var result = await sut.InstallAppOnNewPcAsync(WinGetApp);

        result.Success.Should().BeTrue();
        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), InstallOptions.Default, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InstallAppOnNewPcAsync_MachineWideFails_TriesTheDefaultScopeForTheSameId()
    {
        var sut = CreateSut();
        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.ExtApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");
        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), MachineScope, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.Other, "No machine-scope installer"));
        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), InstallOptions.Default, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        var result = await sut.InstallAppOnNewPcAsync(WinGetApp);

        result.Success.Should().BeTrue();
        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), InstallOptions.Default, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallAppOnNewPcAsync_PortableInstaller_SkipsTheMachineWideAttempt()
    {
        var sut = CreateSut();
        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.ExtApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("portable");
        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), InstallOptions.Default, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        var result = await sut.InstallAppOnNewPcAsync(WinGetApp);

        result.Success.Should().BeTrue();
        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), MachineScope, It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InstallAppAsync_DefaultOptions_NeverAsksForAScope()
    {
        var sut = CreateSut();
        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.ExtApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");
        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        await sut.InstallAppAsync(WinGetApp);

        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.Is<InstallOptions>(o => o.Scope != InstallScope.Default), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InstallAppOnNewPcAsync_AnswerFileChoseOnlyMe_InstallsForTheUser()
    {
        var sut = CreateSut();
        var own = new InstallOptions { Scope = InstallScope.User, Architecture = InstallArchitecture.X86 };
        _installOptions.Setup(x => x.EffectiveFor("ext-app")).Returns(own);
        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.ExtApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");
        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), own, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        var result = await sut.InstallAppOnNewPcAsync(WinGetApp);

        result.Success.Should().BeTrue();
        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), own, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_AppHasItsOwnOptions_InstallsWithThem()
    {
        var sut = CreateSut();
        var own = new InstallOptions { Architecture = InstallArchitecture.Arm64, Interactive = true };
        _installOptions.Setup(x => x.EffectiveFor("ext-app")).Returns(own);
        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.ExtApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");
        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), own, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        var result = await sut.InstallAppAsync(WinGetApp);

        result.Success.Should().BeTrue();
        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), own, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_StoreSource_NeverGetsTheOptions()
    {
        var sut = CreateSut();
        var item = new ItemDefinition { Id = "store-app", Name = "Store App", Description = "Store only", MsStoreId = "9NBLGGH4NNS1" };
        _installOptions.Setup(x => x.EffectiveFor("store-app")).Returns(AllOptions);
        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("9NBLGGH4NNS1", It.IsAny<CancellationToken>()))
            .ReturnsAsync("msstore");
        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "9NBLGGH4NNS1", "msstore", "Store App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        await sut.InstallAppAsync(item);

        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            "9NBLGGH4NNS1", "msstore", "Store App", It.IsAny<string?>(), InstallOptions.Default, It.IsAny<CancellationToken>()),
            Times.Once);
        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_PortablePackage_DropsTheScopeAndKeepsTheRest()
    {
        var sut = CreateSut();
        var withoutScope = AllOptions with { Scope = InstallScope.Default };
        _installOptions.Setup(x => x.EffectiveFor("ext-app")).Returns(AllOptions);
        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.ExtApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("portable");
        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        await sut.InstallAppAsync(WinGetApp);

        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), withoutScope, It.IsAny<CancellationToken>()),
            Times.Once);
        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<InstallOptions>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_ChosenScopeFails_RetriesOnceWithTheDefaultScope()
    {
        var sut = CreateSut();
        var withoutScope = AllOptions with { Scope = InstallScope.Default };
        _installOptions.Setup(x => x.EffectiveFor("ext-app")).Returns(AllOptions);
        _winGetDetectionService
            .Setup(x => x.GetInstallerTypeAsync("Publisher.ExtApp", It.IsAny<CancellationToken>()))
            .ReturnsAsync("exe");
        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), AllOptions, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Failed(InstallFailureReason.Other, "No user-scope installer"));
        _winGetPackageInstaller
            .Setup(x => x.InstallPackageAsync(
                "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), withoutScope, It.IsAny<CancellationToken>()))
            .ReturnsAsync(PackageInstallResult.Succeeded());

        var result = await sut.InstallAppAsync(WinGetApp);

        result.Success.Should().BeTrue();
        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), AllOptions, It.IsAny<CancellationToken>()),
            Times.Once);
        _winGetPackageInstaller.Verify(x => x.InstallPackageAsync(
            "Publisher.ExtApp", "winget", "External App", It.IsAny<string?>(), withoutScope, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InstallAppAsync_Chocolatey_GetsTheOptions()
    {
        var sut = CreateSut();
        var item = new ItemDefinition { Id = "choco-only-app", Name = "Choco Only App", Description = "Choco only", ChocoPackageId = "chocoonlyapp" };
        _installOptions.Setup(x => x.EffectiveFor("choco-only-app")).Returns(AllOptions);
        _chocolateyService
            .Setup(x => x.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _chocolateyService
            .Setup(x => x.InstallPackageAsync("chocoonlyapp", "Choco Only App", AllOptions, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await sut.InstallAppAsync(item);

        result.Success.Should().BeTrue();
    }

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

        _externalAppUninstallService
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
        _externalAppUninstallService.Verify(
            x => x.UninstallAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UninstallAppAsync_Success_LogsAppRemoved()
    {
        var sut = CreateSut();
        var item = new ItemDefinition
        {
            Id = "ext-app",
            Name = "External App",
            Description = "An external app"
        };

        _externalAppUninstallService
            .Setup(x => x.UninstallAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        _interactiveUserService
            .Setup(x => x.GetInteractiveUserFolderPath(Environment.SpecialFolder.Programs))
            .Returns(@"C:\Users\Test\AppData\Roaming\Microsoft\Windows\Start Menu\Programs");
        _fileSystemService
            .Setup(x => x.CombinePath(It.IsAny<string[]>()))
            .Returns(@"C:\Users\Test\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\External App");
        _fileSystemService
            .Setup(x => x.DirectoryExists(It.IsAny<string>()))
            .Returns(false);

        await sut.UninstallAppAsync(item);

        _changeHistoryService.Verify(
            x => x.LogAppChange(It.IsAny<string>(), AppChangeKind.Removed), Times.Once);
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

        _externalAppUninstallService
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

        _externalAppUninstallService
            .Setup(x => x.UninstallAsync(item, It.IsAny<IProgress<TaskProgressDetail>?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await sut.UninstallAppAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

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

    [Fact]
    public void InvalidateStatusCache_DelegatesToDiscoveryService()
    {
        var sut = CreateSut();

        sut.InvalidateStatusCache();

        _appStatusDiscoveryService.Verify(x => x.InvalidateCache(), Times.Once);
    }

    [Fact]
    public async Task SearchPackagesAsync_LeavesOutWingetAppsTheCatalogAlreadyLists()
    {
        var sut = CreateSut();
        var catalogPackageId = ExternalAppDefinitions.GetExternalApps().Items
            .First(item => item.WinGetPackageId is { Length: > 0 }).WinGetPackageId![0];
        _winGetDetectionService
            .Setup(x => x.SearchPackagesAsync("player", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new WinGetPackageMatch(catalogPackageId.ToUpperInvariant(), "Already curated"),
                new WinGetPackageMatch("Contoso.Player", "Contoso Player"),
            });

        var result = (await sut.SearchPackagesAsync("player")).Items;

        result.Should().ContainSingle();
        result[0].Id.Should().Be("winget-Contoso.Player");
        result[0].Name.Should().Be("Contoso Player");
        result[0].WinGetPackageId.Should().Equal("Contoso.Player");
    }

    [Fact]
    public async Task SearchPackagesAsync_AddsOnlyTheChocolateyPackagesWingetAndTheCatalogLack()
    {
        var sut = CreateSut();
        var catalogChocoId = ExternalAppDefinitions.GetExternalApps().Items.First(item => item.ChocoPackageId is not null).ChocoPackageId!;
        _chocolateyService.Setup(x => x.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _chocolateyService.Setup(x => x.SearchPackagesAsync("note", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { "fabrikamnotesplus", "Contoso-Notes", catalogChocoId, "contoso-diary" });
        _winGetDetectionService.Setup(x => x.SearchPackagesAsync("note", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new WinGetPackageMatch("Fabrikam.Pad", "Fabrikam Notes+"), new WinGetPackageMatch("Contoso.ContosoNotes", "Contoso Writer") });

        var result = await sut.SearchPackagesAsync("note");

        result.SearchedChocolatey.Should().BeTrue();
        result.Items.Select(i => i.Id).Should().Equal("winget-Fabrikam.Pad", "winget-Contoso.ContosoNotes", "choco-contoso-diary");
        result.Items[2].ChocoPackageId.Should().Be("contoso-diary");
    }

    [Fact]
    public async Task SearchPackagesAsync_LeavesOutPackagesWhoseIdASavedFileCouldNotCarry()
    {
        var sut = CreateSut();
        _chocolateyService.Setup(x => x.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _chocolateyService.Setup(x => x.SearchPackagesAsync("tools", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ChocoIdsWithOneUnsaveable);
        _winGetDetectionService.Setup(x => x.SearchPackagesAsync("tools", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new WinGetPackageMatch("Contoso.Black&White", "Black & White"), new WinGetPackageMatch("Contoso.Tools", "Contoso Tools") });

        var result = await sut.SearchPackagesAsync("tools");

        result.Items.Select(i => i.Id).Should().Equal("winget-Contoso.Tools", "choco-fabrikam-kit");
        result.Items.Should().OnlyContain(i => PackageSearchItem.FromId(i.Id) != null);
    }

    [Fact]
    public async Task SearchPackagesAsync_SkipsChocolateyWhenItIsNotInstalled()
    {
        var sut = CreateSut();
        _winGetDetectionService.Setup(x => x.SearchPackagesAsync("note", It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<WinGetPackageMatch>());

        var result = await sut.SearchPackagesAsync("note");

        result.SearchedChocolatey.Should().BeFalse();
        _chocolateyService.Verify(x => x.SearchPackagesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static readonly string[] ChocoIdsWithOneUnsaveable = ["fabrikam tools", "fabrikam-kit"];
    private static readonly string[] MissingFromWinGet = ["gone.app", "Gone.Too", "Gone.Choco", "Gone.Store", "Gone.Search"];

    [Fact]
    public async Task GetUnavailableAppIdsAsync_NamesOnlyAppsWithNoSourceLeft()
    {
        var sut = CreateSut();
        ItemDefinition App(string id, string[] winget, string? choco = null, string? store = null) => new()
        {
            Id = id, Name = id, Description = id, WinGetPackageId = winget, ChocoPackageId = choco, MsStoreId = store,
        };
        var items = new[]
        {
            App("gone", ["Gone.App"]),
            App("one-of-two-gone", ["Gone.Too", "Still.Here"]),
            App("has-choco", ["Gone.Choco"], choco: "gone-choco"),
            App("has-store", ["Gone.Store"], store: "9ABC"),
            App("winget-Gone.Search", ["Gone.Search"]),
        };
        _winGetDetectionService
            .Setup(x => x.FindMissingPackageIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(MissingFromWinGet);

        var result = await sut.GetUnavailableAppIdsAsync(items);

        result.Should().Equal("gone");
    }

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
