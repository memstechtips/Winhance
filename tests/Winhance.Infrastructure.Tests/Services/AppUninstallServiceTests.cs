using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Enums;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class AppUninstallServiceTests
{
    private readonly Mock<IWinGetPackageInstaller> _winGetPackageInstaller = new();
    private readonly Mock<IChocolateyService> _chocolateyService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IInteractiveUserService> _interactiveUserService = new();
    private readonly Mock<ITaskProgressService> _taskProgressService = new();
    private readonly Mock<IProcessExecutor> _processExecutor = new();

    private AppUninstallService CreateSut() => new(
        _winGetPackageInstaller.Object,
        _chocolateyService.Object,
        _logService.Object,
        _interactiveUserService.Object,
        _taskProgressService.Object,
        _processExecutor.Object);

    // --- UninstallAsync: FileSystem method ---

    [Fact]
    public async Task UninstallAsync_FileSystemDetectedWithExistingDirectory_ReturnsSuccess()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"WinhanceTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var item = new ItemDefinition
            {
                Id = "fs-app",
                Name = "FileSystem App",
                Description = "App detected via filesystem",
                DetectedVia = DetectionSource.FileSystem,
                DetectionPaths = new[] { tempDir }
            };

            var sut = CreateSut();
            var result = await sut.UninstallAsync(item);

            result.Success.Should().BeTrue();
            Directory.Exists(tempDir).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task UninstallAsync_FileSystemDetectedWithNonExistentPath_ReturnsFailed()
    {
        var item = new ItemDefinition
        {
            Id = "fs-app",
            Name = "FileSystem App",
            Description = "App detected via filesystem",
            DetectedVia = DetectionSource.FileSystem,
            DetectionPaths = new[] { @"C:\NonExistent\Path\That\Does\Not\Exist_" + Guid.NewGuid() }
        };

        var sut = CreateSut();
        var result = await sut.UninstallAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No files found to remove");
    }

    [Fact]
    public async Task UninstallAsync_FileSystemDetectedWithNoDetectionPaths_ReturnsFailed()
    {
        var item = new ItemDefinition
        {
            Id = "fs-app",
            Name = "FileSystem App",
            Description = "App detected via filesystem",
            DetectedVia = DetectionSource.FileSystem,
            DetectionPaths = Array.Empty<string>()
        };

        var sut = CreateSut();
        var result = await sut.UninstallAsync(item);

        result.Success.Should().BeFalse();
        // Empty DetectionPaths means FileSystem guard clause is not matched,
        // so it falls through to "No uninstall method available"
        result.ErrorMessage.Should().Contain("No uninstall method available");
    }

    [Fact]
    public async Task UninstallAsync_FileSystemDetectedWithExistingFile_DeletesFileAndReturnsSuccess()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"WinhanceTest_{Guid.NewGuid():N}.txt");
        File.WriteAllText(tempFile, "test");

        try
        {
            var item = new ItemDefinition
            {
                Id = "fs-app",
                Name = "FileSystem App",
                Description = "App detected via filesystem",
                DetectedVia = DetectionSource.FileSystem,
                DetectionPaths = new[] { tempFile }
            };

            var sut = CreateSut();
            var result = await sut.UninstallAsync(item);

            result.Success.Should().BeTrue();
            File.Exists(tempFile).Should().BeFalse();
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task UninstallAsync_FileSystemCancellation_ReturnsCancelled()
    {
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var item = new ItemDefinition
        {
            Id = "fs-app",
            Name = "FileSystem App",
            Description = "App detected via filesystem",
            DetectedVia = DetectionSource.FileSystem,
            DetectionPaths = new[] { Path.GetTempPath() }
        };

        var sut = CreateSut();
        var result = await sut.UninstallAsync(item, cancellationToken: cts.Token);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    // --- UninstallAsync: WinGet method ---

    [Fact]
    public async Task UninstallAsync_WinGetDetected_UsesWinGetUninstall()
    {
        var item = new ItemDefinition
        {
            Id = "winget-app",
            Name = "WinGet App",
            Description = "App detected via WinGet",
            DetectedVia = DetectionSource.WinGet,
            WinGetPackageId = new[] { "Publisher.App" }
        };

        _winGetPackageInstaller
            .Setup(w => w.UninstallPackageAsync("Publisher.App", "winget", "WinGet App", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = CreateSut();
        var result = await sut.UninstallAsync(item);

        result.Success.Should().BeTrue();
        _winGetPackageInstaller.Verify(
            w => w.UninstallPackageAsync("Publisher.App", "winget", "WinGet App", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // --- UninstallAsync: Chocolatey method ---

    [Fact]
    public async Task UninstallAsync_ChocolateyDetected_UsesChocolateyUninstall()
    {
        var item = new ItemDefinition
        {
            Id = "choco-app",
            Name = "Choco App",
            Description = "App detected via Chocolatey",
            DetectedVia = DetectionSource.Chocolatey,
            ChocoPackageId = "chocoapp"
        };

        _chocolateyService
            .Setup(c => c.IsChocolateyInstalledAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _chocolateyService
            .Setup(c => c.UninstallPackageAsync("chocoapp", "Choco App", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = CreateSut();
        var result = await sut.UninstallAsync(item);

        result.Success.Should().BeTrue();
        _chocolateyService.Verify(
            c => c.UninstallPackageAsync("chocoapp", "Choco App", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // --- UninstallAsync: No method available ---

    [Fact]
    public async Task UninstallAsync_NoUninstallMethodAvailable_ReturnsFailed()
    {
        var item = new ItemDefinition
        {
            Id = "no-method-app",
            Name = "No Method App",
            Description = "App with no uninstall method"
        };

        var sut = CreateSut();
        var result = await sut.UninstallAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No uninstall method available");
    }
}
