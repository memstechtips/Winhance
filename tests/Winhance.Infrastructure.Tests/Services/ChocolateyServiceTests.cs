using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.SoftwareApps.Models;
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
    private readonly Mock<IUserPreferencesService> _mockPrefs = new();
    private readonly Mock<IDialogService> _mockDialog = new();
    private readonly ChocolateyService _sut;

    public ChocolateyServiceTests()
    {
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => string.Format(key, args));

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
            _mockFileSystem.Object,
            _mockPrefs.Object,
            _mockDialog.Object);
    }

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
            _mockFileSystem.Object,
            _mockPrefs.Object,
            _mockDialog.Object);

        var result = await sut.IsChocolateyInstalledAsync();

        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsChocolateyInstalledAsync_CachesResult()
    {
        var first = await _sut.IsChocolateyInstalledAsync();
        var second = await _sut.IsChocolateyInstalledAsync();

        first.Should().Be(second);
    }

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
            _mockFileSystem.Object,
            _mockPrefs.Object,
            _mockDialog.Object);

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
        _mockLog.Verify(l => l.LogError(It.Is<string>(s => s.Contains("Failed to install Chocolatey")), It.IsAny<Exception?>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task InstallPackageAsync_WhenChocoNotFound_ReturnsFalse()
    {
        var result = await _sut.InstallPackageAsync("notepadplusplus", null, InstallOptions.Default, CancellationToken.None);

        result.Should().BeFalse();
        _mockLog.Verify(l => l.LogError(It.Is<string>(s => s.Contains("not found")), It.IsAny<Exception?>(), It.IsAny<string>()), Times.Once);
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
            _mockFileSystem.Object,
            _mockPrefs.Object,
            _mockDialog.Object);

        var result = await sut.InstallPackageAsync("notepadplusplus", "Notepad++", InstallOptions.Default, CancellationToken.None);

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
            _mockFileSystem.Object,
            _mockPrefs.Object,
            _mockDialog.Object);

        var act = () => sut.InstallPackageAsync("pkg", null, InstallOptions.Default, CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task InstallPackageAsync_KilledByCancellation_ThrowsInsteadOfReportingFailure()
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
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = -1 });
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var sut = new ChocolateyService(
            _mockLog.Object,
            _mockProgress.Object,
            _mockLocalization.Object,
            _mockProcessExecutor.Object,
            _mockFileSystem.Object,
            _mockPrefs.Object,
            _mockDialog.Object);

        var act = () => sut.InstallPackageAsync("pkg", null, InstallOptions.Default, cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task InstallPackageAsync_ChecksumFailureWithSavedPreference_RetriesWithFlagWithoutAsking()
    {
        SetupChecksumFailureThenSuccess();
        _mockPrefs.Setup(p => p.GetPreferenceAsync(UserPreferenceKeys.ChocolateyIgnoreChecksums, false)).ReturnsAsync(true);

        var result = await _sut.InstallPackageAsync("vlc", "VLC", InstallOptions.Default, CancellationToken.None);

        result.Should().BeTrue();
        VerifyInstallRuns(withFlag: true, Times.Once());
        _mockDialog.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
    }

    [Fact]
    public async Task InstallPackageAsync_ChecksumFailureConfirmedWithCheckbox_RetriesAndSavesPreference()
    {
        SetupChecksumFailureThenSuccess();
        _mockDialog
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true, CheckboxChecked = true });

        var result = await _sut.InstallPackageAsync("vlc", "VLC", InstallOptions.Default, CancellationToken.None);

        result.Should().BeTrue();
        VerifyInstallRuns(withFlag: true, Times.Once());
        _mockPrefs.Verify(p => p.SetPreferenceAsync(UserPreferenceKeys.ChocolateyIgnoreChecksums, true), Times.Once);
    }

    [Fact]
    public async Task InstallPackageAsync_ChecksumFailureConfirmedWithoutCheckbox_RetriesWithoutSavingPreference()
    {
        SetupChecksumFailureThenSuccess();
        _mockDialog
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = true });

        var result = await _sut.InstallPackageAsync("vlc", "VLC", InstallOptions.Default, CancellationToken.None);

        result.Should().BeTrue();
        _mockPrefs.Verify(p => p.SetPreferenceAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task InstallPackageAsync_ChecksumFailureDeclined_DoesNotRetry()
    {
        SetupChecksumFailureThenSuccess();
        _mockDialog
            .Setup(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()))
            .ReturnsAsync(new ConfirmationResponse { Confirmed = false, CheckboxChecked = true });

        var result = await _sut.InstallPackageAsync("vlc", "VLC", InstallOptions.Default, CancellationToken.None);

        result.Should().BeFalse();
        VerifyInstallRuns(withFlag: true, Times.Never());
        _mockPrefs.Verify(p => p.SetPreferenceAsync(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task InstallPackageAsync_ChecksumFailureWithNoDialogService_DoesNotRetry()
    {
        SetupChecksumFailureThenSuccess();
        var sut = new ChocolateyService(
            _mockLog.Object,
            _mockProgress.Object,
            _mockLocalization.Object,
            _mockProcessExecutor.Object,
            _mockFileSystem.Object,
            _mockPrefs.Object);

        var result = await sut.InstallPackageAsync("vlc", "VLC", InstallOptions.Default, CancellationToken.None);

        result.Should().BeFalse();
        VerifyInstallRuns(withFlag: true, Times.Never());
    }

    [Fact]
    public async Task InstallPackageAsync_NonChecksumFailure_DoesNotAskOrRetry()
    {
        _mockFileSystem.Setup(f => f.FileExists(ChocoPath)).Returns(true);
        _mockProcessExecutor
            .Setup(p => p.ExecuteWithStreamingAsync(ChocoPath, It.IsAny<string>(), It.IsAny<Action<string>?>(), It.IsAny<Action<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = 1, StandardError = "Unable to resolve dependency" });

        var result = await _sut.InstallPackageAsync("vlc", "VLC", InstallOptions.Default, CancellationToken.None);

        result.Should().BeFalse();
        _mockDialog.Verify(d => d.ShowConfirmationAsync(It.IsAny<ConfirmationRequest>()), Times.Never);
        VerifyInstallRuns(withFlag: false, Times.Once());
    }

    [Fact]
    public void InstallArguments_DefaultOptions_AddNoOptionFlags()
    {
        ChocolateyService.InstallArguments("vlc", InstallOptions.Default)
            .Should().Be("install vlc -y --no-progress");
    }

    [Fact]
    public void InstallArguments_X86_ForcesThe32BitInstaller()
    {
        ChocolateyService.InstallArguments("vlc", InstallOptions.Default with { Architecture = InstallArchitecture.X86 })
            .Should().EndWith(" --forcex86");
    }

    [Fact]
    public void InstallArguments_Interactive_ShowsTheInstaller()
    {
        ChocolateyService.InstallArguments("vlc", InstallOptions.Default with { Interactive = true })
            .Should().EndWith(" --not-silent");
    }

    [Theory]
    [InlineData(InstallArchitecture.X64, InstallScope.Default)]
    [InlineData(InstallArchitecture.Arm64, InstallScope.Default)]
    [InlineData(InstallArchitecture.Default, InstallScope.User)]
    [InlineData(InstallArchitecture.Default, InstallScope.Machine)]
    public void InstallArguments_OptionsChocolateyCannotHonour_AddNothing(InstallArchitecture architecture, InstallScope scope)
    {
        ChocolateyService.InstallArguments("vlc", new InstallOptions { Architecture = architecture, Scope = scope })
            .Should().Be(ChocolateyService.InstallArguments("vlc", InstallOptions.Default));
    }

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
            _mockFileSystem.Object,
            _mockPrefs.Object,
            _mockDialog.Object);

        var result = await sut.UninstallPackageAsync("notepadplusplus", "Notepad++");

        result.Should().BeTrue();
    }

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
            _mockFileSystem.Object,
            _mockPrefs.Object,
            _mockDialog.Object);

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
            _mockFileSystem.Object,
            _mockPrefs.Object,
            _mockDialog.Object);

        var result = await sut.GetInstalledPackageIdsAsync();

        result.Contains("git").Should().BeTrue();
        result.Contains("Git").Should().BeTrue();
        result.Contains("GIT").Should().BeTrue();
    }

    [Fact]
    public void ParseIds_RepeatedAndJunkLines_ReturnsEachIdOnce()
    {
        ChocolateyService.ParseIds("vlc|3.0.21\r\nvlc.portable|3.0.21\r\nVLC|3.0.20\r\n\r\nnot a package line\r\n")
            .Should().Equal("vlc", "vlc.portable");
    }

    [Fact]
    public async Task GetOutdatedPackagesAsync_WithoutChocolatey_HasNothingOutdated()
    {
        var outdated = await _sut.GetOutdatedPackagesAsync();

        outdated.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task GetOutdatedPackagesAsync_WhenTheCheckFails_ReturnsNull()
    {
        _mockFileSystem
            .Setup(f => f.FileExists(@"C:\ProgramData\chocolatey\bin\choco.exe"))
            .Returns(true);
        _mockProcessExecutor
            .Setup(p => p.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("choco is broken"));

        var outdated = await _sut.GetOutdatedPackagesAsync();

        outdated.Should().BeNull();
    }

    [Fact]
    public void ParseOutdated_PinnedAndNoiseLines_ReturnsNewerVersionsOfUnpinnedPackages()
    {
        var outdated = ChocolateyService.ParseOutdated(
            "vlc|3.0.20|3.0.21|false\r\ngit|2.44.0|2.46.0|true\r\nChocolatey detected noise\r\n7zip|23.1|24.9|false\r\n");

        outdated.Should().HaveCount(2);
        outdated["VLC"].Should().Be("3.0.21");
        outdated["7zip"].Should().Be("24.9");
    }

    [Fact]
    public void ParseOutdated_FeedVersionNotNewer_IsSkipped()
    {
        var outdated = ChocolateyService.ParseOutdated("vlc|3.0.21|3.0.20|false\r\nchrome|130.0.1|130.0.1|false\r\ngit|2.44.0|2.46.0|false\r\n");

        outdated.Keys.Should().Equal("git");
    }

    [Theory]
    [InlineData("notepad++", "notepad++")]
    [InlineData("vlc\" & del C:\\", "vlc  del C")]
    [InlineData("  7-zip  ", "7-zip")]
    public void SearchTerm_UnsafeCharacters_KeepsOnlyThoseAPackageNameCanHold(string query, string expected) =>
        ChocolateyService.SearchTerm(query).Should().Be(expected);

    private const string ChocoPath = @"C:\ProgramData\chocolatey\bin\choco.exe";

    // The first run (no flag) prints a checksum error and fails; a run with the flag succeeds.
    private void SetupChecksumFailureThenSuccess()
    {
        _mockFileSystem.Setup(f => f.FileExists(ChocoPath)).Returns(true);
        _mockProcessExecutor
            .Setup(p => p.ExecuteWithStreamingAsync(ChocoPath, It.Is<string>(a => !a.Contains("--ignore-checksums")), It.IsAny<Action<string>?>(), It.IsAny<Action<string>?>(), It.IsAny<CancellationToken>()))
            .Returns((string _, string _, Action<string>? onOutput, Action<string>? _, CancellationToken _) =>
            {
                onOutput?.Invoke("ERROR: Checksum for 'vlc.exe' did not meet 'abc' for checksum type 'sha256'.");
                return Task.FromResult(new ProcessExecutionResult { ExitCode = 1 });
            });
        _mockProcessExecutor
            .Setup(p => p.ExecuteWithStreamingAsync(ChocoPath, It.Is<string>(a => a.Contains("--ignore-checksums")), It.IsAny<Action<string>?>(), It.IsAny<Action<string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0 });
    }

    private void VerifyInstallRuns(bool withFlag, Times times) =>
        _mockProcessExecutor.Verify(
            p => p.ExecuteWithStreamingAsync(ChocoPath, It.Is<string>(a => a.Contains("--ignore-checksums") == withFlag), It.IsAny<Action<string>?>(), It.IsAny<Action<string>?>(), It.IsAny<CancellationToken>()),
            times);
}
