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

public class ExternalAppUninstallServiceTests
{
    private readonly Mock<IWinGetPackageInstaller> _winGetPackageInstaller = new();
    private readonly Mock<IChocolateyService> _chocolateyService = new();
    private readonly Mock<ILogService> _logService = new();
    private readonly Mock<IWindowsRegistryService> _registry = new();
    private readonly Mock<ITaskProgressService> _taskProgressService = new();
    private readonly Mock<IProcessExecutor> _processExecutor = new();
    private readonly Mock<IStagedAppInstaller> _stagedAppInstaller = new();
    private readonly Mock<ILocalizationService> _localization = new();

    public ExternalAppUninstallServiceTests()
    {
        _localization.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns<string, object[]>((key, args) => $"<{key}:{string.Join("|", args)}>");
    }

    private ExternalAppUninstallService CreateSut() => new(
        _winGetPackageInstaller.Object,
        _chocolateyService.Object,
        _logService.Object,
        _registry.Object,
        _taskProgressService.Object,
        _processExecutor.Object,
        _stagedAppInstaller.Object,
        _localization.Object);

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

    private void GivenUninstallKeys(string root, params (string KeyName, string? DisplayName, string? UninstallString)[] keys)
    {
        _registry.Setup(r => r.GetSubKeyNames(root)).Returns(keys.Select(k => k.KeyName).ToArray());
        foreach (var (keyName, displayName, uninstallString) in keys)
        {
            _registry.Setup(r => r.GetValue($@"{root}\{keyName}", "DisplayName")).Returns(displayName);
            _registry.Setup(r => r.GetValue($@"{root}\{keyName}", "UninstallString")).Returns(uninstallString);
        }
    }

    [Fact]
    public async Task UninstallAsync_RegistryDetected_RunsTheUninstallStringOfTheMatchingKey()
    {
        GivenUninstallKeys(UninstallRegistryReader.User,
            ("Other", "Other App", @"""C:\Other\remove.exe"""),
            ("Contoso", "Contoso Tool 2.1", @"""C:\Contoso\remove.exe"" /x"));
        var item = new ItemDefinition
        {
            Id = "contoso", Name = "Contoso", Description = "",
            DetectedVia = DetectionSource.Registry, RegistryDisplayName = "Contoso Tool {version}",
        };

        var result = await CreateSut().UninstallAsync(item);

        result.Success.Should().BeTrue();
        _processExecutor.Verify(
            p => p.ShellExecuteAsync(@"C:\Contoso\remove.exe", "/x", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UninstallAsync_MatchingKeyIsAHiddenSystemComponent_StillUsesItsUninstallString()
    {
        GivenUninstallKeys(UninstallRegistryReader.Machine, ("ContosoKey", null, @"C:\Contoso\remove.exe"));
        _registry.Setup(r => r.GetValue($@"{UninstallRegistryReader.Machine}\ContosoKey", "SystemComponent")).Returns(1);
        var item = new ItemDefinition
        {
            Id = "contoso", Name = "Contoso", Description = "",
            DetectedVia = DetectionSource.Registry, RegistrySubKeyName = "ContosoKey",
        };

        var result = await CreateSut().UninstallAsync(item);

        result.Success.Should().BeTrue();
        _processExecutor.Verify(
            p => p.ShellExecuteAsync(@"C:\Contoso\remove.exe", It.IsAny<string>(), true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UninstallAsync_FirstMatchingKeyHasNoUninstallString_UsesTheNextMatch()
    {
        GivenUninstallKeys(UninstallRegistryReader.Machine, ("Contoso", "Contoso Tool", null));
        GivenUninstallKeys(UninstallRegistryReader.Machine32, ("Contoso32", "Contoso Tool", @"C:\Contoso32\remove.exe"));
        var item = new ItemDefinition
        {
            Id = "contoso", Name = "Contoso Tool", Description = "", DetectedVia = DetectionSource.Registry,
        };

        var result = await CreateSut().UninstallAsync(item);

        result.Success.Should().BeTrue();
        _processExecutor.Verify(
            p => p.ShellExecuteAsync(@"C:\Contoso32\remove.exe", It.IsAny<string>(), true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UninstallAsync_PortableWinhanceLaidOut_IsRemovedByItAndNeverGoesToWinGet()
    {
        var item = new ItemDefinition { Id = "tool", Name = "Tool", Description = "", WinGetPackageId = ["Example.Tool"] };
        _stagedAppInstaller.Setup(s => s.UninstallPortableAsync(item, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OperationResult<bool>.Succeeded(true));

        var result = await CreateSut().UninstallAsync(item);

        result.Success.Should().BeTrue();
        _winGetPackageInstaller.Verify(
            w => w.UninstallPackageAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UninstallAsync_PortableWinGetInstalled_StillGoesThroughWinGet()
    {
        var item = new ItemDefinition { Id = "tool", Name = "Tool", Description = "", WinGetPackageId = ["Example.Tool"] };
        _winGetPackageInstaller
            .Setup(w => w.UninstallPackageAsync("Example.Tool", "winget", "Tool", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateSut().UninstallAsync(item);

        result.Success.Should().BeTrue();
        _winGetPackageInstaller.Verify(
            w => w.UninstallPackageAsync("Example.Tool", "winget", "Tool", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private ItemDefinition GivenBraveInstalledPerMachineAndPerUser()
    {
        GivenUninstallKeys(UninstallRegistryReader.Machine, ("BraveSoftware Brave-Browser", "Brave",
            @"""C:\Program Files\BraveSoftware\Brave-Browser\Application\157.1.99.57\Installer\setup.exe"" --uninstall --system-level"));
        GivenUninstallKeys(UninstallRegistryReader.User, ("BraveSoftware Brave-Browser", "Brave",
            @"""C:\Users\u\AppData\Local\BraveSoftware\Brave-Browser\Application\157.1.99.57\Installer\setup.exe"" --uninstall"));
        var item = new ItemDefinition
        {
            Id = "brave", Name = "Brave", Description = "", WinGetPackageId = ["Brave.Brave"], DetectedVia = DetectionSource.WinGet,
        };
        _winGetPackageInstaller
            .Setup(w => w.UninstallPackageAsync("Brave.Brave", "winget", "Brave", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return item;
    }

    [Fact]
    public async Task UninstallAsync_AppInstalledPerMachineAndPerUser_RunsBothUninstallers()
    {
        var item = GivenBraveInstalledPerMachineAndPerUser();

        await CreateSut().UninstallAsync(item);

        _processExecutor.Verify(
            p => p.ShellExecuteAsync(It.Is<string>(f => f.StartsWith(@"C:\Program Files\")), It.IsAny<string>(), true, It.IsAny<CancellationToken>()),
            Times.Once);
        _processExecutor.Verify(
            p => p.ShellExecuteAsync(It.Is<string>(f => f.StartsWith(@"C:\Users\u\")), It.IsAny<string>(), true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // Chromium's setup.exe ignores /VERYSILENT and asks the user to confirm, then opens the vendor's page.
    [Fact]
    public async Task UninstallAsync_ChromiumUninstallString_GetsChromiumsSilentSwitch()
    {
        var item = GivenBraveInstalledPerMachineAndPerUser();

        await CreateSut().UninstallAsync(item);

        _processExecutor.Verify(
            p => p.ShellExecuteAsync(It.IsAny<string>(), "--uninstall --system-level --force-uninstall", true, It.IsAny<CancellationToken>()),
            Times.Once);
        _processExecutor.Verify(
            p => p.ShellExecuteAsync(It.IsAny<string>(), "--uninstall --force-uninstall", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // winget runs each scope's registered string as written, so Brave would ask once per scope and open its survey page.
    [Fact]
    public async Task UninstallAsync_WinGetAppWithAChromiumUninstaller_NeverGoesToWinGet()
    {
        var item = GivenBraveInstalledPerMachineAndPerUser();

        var result = await CreateSut().UninstallAsync(item);

        result.Success.Should().BeTrue();
        _winGetPackageInstaller.Verify(
            w => w.UninstallPackageAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UninstallAsync_SquirrelUpdateExeUninstall_GetsNoChromiumSwitch()
    {
        GivenUninstallKeys(UninstallRegistryReader.User, ("Contoso", "Contoso", @"""C:\Users\u\AppData\Local\Contoso\Update.exe"" --uninstall -s"));
        var item = new ItemDefinition { Id = "contoso", Name = "Contoso", Description = "", DetectedVia = DetectionSource.Registry };

        await CreateSut().UninstallAsync(item);

        _processExecutor.Verify(
            p => p.ShellExecuteAsync(It.IsAny<string>(), "--uninstall -s", true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // A 0 % report reads as a failed run and keeps the bar up after the task ends.
    [Fact]
    public async Task UninstallAsync_AppThatIsNotInstalled_EndsOnNotFoundWithoutAFailedProgressReport()
    {
        var item = new ItemDefinition { Id = "7zip", Name = "7-Zip", Description = "", WinGetPackageId = ["7zip.7zip"], ChocoPackageId = "7zip" };
        var statuses = new List<string?>();
        _taskProgressService.Setup(t => t.UpdateDetailedProgress(It.IsAny<TaskProgressDetail>()))
            .Callback<TaskProgressDetail>(d => statuses.Add(d.StatusText));

        var result = await CreateSut().UninstallAsync(item);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("<Progress_Uninstall_NotFound:7-Zip>");
        statuses.Should().EndWith("<Progress_Uninstall_NotFound:7-Zip>");
        _taskProgressService.Verify(t => t.UpdateProgress(0, It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task UninstallAsync_RegistryFallback_LeavesAnotherAppWhoseNameAlsoMatches()
    {
        GivenUninstallKeys(UninstallRegistryReader.Machine,
            ("Contoso", "Contoso", @"""C:\Contoso\remove.exe"""),
            ("ContosoBeta", "Contoso Beta", @"""C:\ContosoBeta\remove.exe"""));
        var item = new ItemDefinition { Id = "contoso", Name = "Contoso", Description = "", DetectedVia = DetectionSource.Registry };

        await CreateSut().UninstallAsync(item);

        _processExecutor.Verify(
            p => p.ShellExecuteAsync(@"C:\ContosoBeta\remove.exe", It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UninstallAsync_WinGetFallbackExitsButLeavesTheEntry_ReportsFailure()
    {
        GivenUninstallKeys(UninstallRegistryReader.User, ("Example.Tool_src", "Tool", "winget uninstall --product-code Example.Tool_src"));
        var item = new ItemDefinition
        {
            Id = "tool", Name = "Tool", Description = "", DetectedVia = DetectionSource.Registry, RegistryDisplayName = "Tool",
        };

        var result = await CreateSut().UninstallAsync(item);

        result.Success.Should().BeFalse();
    }
}
