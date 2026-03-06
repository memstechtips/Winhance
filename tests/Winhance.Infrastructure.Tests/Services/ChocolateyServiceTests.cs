using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class ChocolateyServiceTests
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<ITaskProgressService> _mockProgress = new();
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();
    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly ChocolateyService _sut;

    public ChocolateyServiceTests()
    {
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => string.Format(key, args));

        // By default, choco.exe is NOT found
        _mockFileSystem
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(false);
        _mockFileSystem
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join("\\", parts));

        _sut = new ChocolateyService(
            _mockLog.Object,
            _mockProgress.Object,
            _mockLocalization.Object,
            _mockProcessExecutor.Object,
            _mockFileSystem.Object);
    }

    // ── IsChocolateyInstalledAsync ──

    [Fact]
    public async Task IsChocolateyInstalledAsync_WhenChocoNotFound_ReturnsFalse()
    {
        var result = await _sut.IsChocolateyInstalledAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsChocolateyInstalledAsync_WhenChocoFoundAtStandardPath_ReturnsTrue()
    {
        _mockFileSystem
            .Setup(f => f.FileExists(@"C:\ProgramData\chocolatey\bin\choco.exe"))
            .Returns(true);

        // Need a new instance since the old one already cached _isInstalled
        var sut = new ChocolateyService(
            _mockLog.Object,
            _mockProgress.Object,
            _mockLocalization.Object,
            _mockProcessExecutor.Object,
            _mockFileSystem.Object);

        var result = await sut.IsChocolateyInstalledAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsChocolateyInstalledAsync_CachesResult()
    {
        var first = await _sut.IsChocolateyInstalledAsync();
        var second = await _sut.IsChocolateyInstalledAsync();

        first.Should().Be(second);
        // FileExists should only be called during the first check (caching)
    }

    // ── InstallChocolateyAsync ──

    [Fact]
    public async Task InstallChocolateyAsync_WhenAlreadyInstalled_ReturnsTrueWithoutExecuting()
    {
        _mockFileSystem
            .Setup(f => f.FileExists(@"C:\ProgramData\chocolatey\bin\choco.exe"))
            .Returns(true);

        var sut = new ChocolateyService(
            _mockLog.Object,
            _mockProgress.Object,
            _mockLocalization.Object,
            _mockProcessExecutor.Object,
            _mockFileSystem.Object);

        var result = await sut.InstallChocolateyAsync();

        result.Should().BeTrue();
        _mockProcessExecutor.Verify(
            p => p.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task InstallChocolateyAsync_WhenInstallFails_ReturnsFalse()
    {
        _mockProcessExecutor
            .Setup(p => p.ExecuteAsync("powershell.exe", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 1,
                StandardError = "Install failed"
            });

        var result = await _sut.InstallChocolateyAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task InstallChocolateyAsync_WhenExceptionThrown_ReturnsFalse()
    {
        _mockProcessExecutor
            .Setup(p => p.ExecuteAsync("powershell.exe", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Process error"));

        var result = await _sut.InstallChocolateyAsync();

        result.Should().BeFalse();
        _mockLog.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Failed to install Chocolatey"))), Times.Once);
    }

    // ── InstallPackageAsync ──

    [Fact]
    public async Task InstallPackageAsync_WhenChocoNotFound_ReturnsFalse()
    {
        var result = await _sut.InstallPackageAsync("notepadplusplus");

        result.Should().BeFalse();
        _mockLog.Verify(l => l.LogError(It.Is<string>(s => s.Contains("not found"))), Times.Once);
    }

    [Fact]
    public async Task InstallPackageAsync_SuccessfulInstall_ReturnsTrue()
    {
        _mockFileSystem
            .Setup(f => f.FileExists(@"C:\ProgramData\chocolatey\bin\choco.exe"))
            .Returns(true);
        _mockProcessExecutor
            .Setup(p => p.ExecuteWithStreamingAsync(
                @"C:\ProgramData\chocolatey\bin\choco.exe",
                It.Is<string>(s => s.Contains("install notepadplusplus")),
                It.IsAny<Action<string>?>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0 });

        var sut = new ChocolateyService(
            _mockLog.Object,
            _mockProgress.Object,
            _mockLocalization.Object,
            _mockProcessExecutor.Object,
            _mockFileSystem.Object);

        var result = await sut.InstallPackageAsync("notepadplusplus", "Notepad++");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task InstallPackageAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        _mockFileSystem
            .Setup(f => f.FileExists(@"C:\ProgramData\chocolatey\bin\choco.exe"))
            .Returns(true);
        _mockProcessExecutor
            .Setup(p => p.ExecuteWithStreamingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var sut = new ChocolateyService(
            _mockLog.Object,
            _mockProgress.Object,
            _mockLocalization.Object,
            _mockProcessExecutor.Object,
            _mockFileSystem.Object);

        var act = () => sut.InstallPackageAsync("pkg");

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── UninstallPackageAsync ──

    [Fact]
    public async Task UninstallPackageAsync_WhenChocoNotFound_ReturnsFalse()
    {
        var result = await _sut.UninstallPackageAsync("notepadplusplus");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UninstallPackageAsync_SuccessfulUninstall_ReturnsTrue()
    {
        _mockFileSystem
            .Setup(f => f.FileExists(@"C:\ProgramData\chocolatey\bin\choco.exe"))
            .Returns(true);
        _mockProcessExecutor
            .Setup(p => p.ExecuteWithStreamingAsync(
                @"C:\ProgramData\chocolatey\bin\choco.exe",
                It.Is<string>(s => s.Contains("uninstall notepadplusplus")),
                It.IsAny<Action<string>?>(),
                It.IsAny<Action<string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0 });

        var sut = new ChocolateyService(
            _mockLog.Object,
            _mockProgress.Object,
            _mockLocalization.Object,
            _mockProcessExecutor.Object,
            _mockFileSystem.Object);

        var result = await sut.UninstallPackageAsync("notepadplusplus", "Notepad++");

        result.Should().BeTrue();
    }

    // ── GetInstalledPackageIdsAsync ──

    [Fact]
    public async Task GetInstalledPackageIdsAsync_WhenChocoNotFound_ReturnsEmptySet()
    {
        var result = await _sut.GetInstalledPackageIdsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetInstalledPackageIdsAsync_ParsesChocoListOutput()
    {
        _mockFileSystem
            .Setup(f => f.FileExists(@"C:\ProgramData\chocolatey\bin\choco.exe"))
            .Returns(true);
        _mockProcessExecutor
            .Setup(p => p.ExecuteAsync(
                @"C:\ProgramData\chocolatey\bin\choco.exe",
                "list -r",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = "git|2.43.0\nnotepadplusplus|8.6.2\n7zip|23.01\n"
            });

        var sut = new ChocolateyService(
            _mockLog.Object,
            _mockProgress.Object,
            _mockLocalization.Object,
            _mockProcessExecutor.Object,
            _mockFileSystem.Object);

        var result = await sut.GetInstalledPackageIdsAsync();

        result.Should().HaveCount(3);
        result.Should().Contain("git");
        result.Should().Contain("notepadplusplus");
        result.Should().Contain("7zip");
    }

    [Fact]
    public async Task GetInstalledPackageIdsAsync_IsCaseInsensitive()
    {
        _mockFileSystem
            .Setup(f => f.FileExists(@"C:\ProgramData\chocolatey\bin\choco.exe"))
            .Returns(true);
        _mockProcessExecutor
            .Setup(p => p.ExecuteAsync(
                @"C:\ProgramData\chocolatey\bin\choco.exe",
                "list -r",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = "Git|2.43.0\n"
            });

        var sut = new ChocolateyService(
            _mockLog.Object,
            _mockProgress.Object,
            _mockLocalization.Object,
            _mockProcessExecutor.Object,
            _mockFileSystem.Object);

        var result = await sut.GetInstalledPackageIdsAsync();

        result.Contains("git").Should().BeTrue();
        result.Contains("Git").Should().BeTrue();
        result.Contains("GIT").Should().BeTrue();
    }
}
