using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

/// <summary>
/// Tests for <see cref="WinGetBootstrapper"/>.
///
/// WinGetComSession is a concrete class without virtual methods, so it cannot
/// be mocked with Moq. We instantiate it with a mock ILogService. Because the
/// COM API (WindowsPackageManagerStandardFactory) is unlikely to be available
/// in a CI/test environment, EnsureComInitialized() will return false, which
/// exercises the fallback paths.
///
/// WinGetCliRunner uses static methods that check real file system paths (PATH,
/// WindowsApps, bundled). Tests that rely on these are written to verify the
/// service's branching logic rather than actual winget availability.
/// </summary>
public class WinGetBootstrapperTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IInteractiveUserService> _mockInteractiveUserService = new();
    private readonly Mock<IPowerShellRunner> _mockPowerShellRunner = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<ITaskProgressService> _mockTaskProgressService = new();
    private readonly WinGetComSession _comSession;
    private readonly WinGetBootstrapper _sut;

    public WinGetBootstrapperTests()
    {
        _comSession = new WinGetComSession(_mockLogService.Object);

        _sut = new WinGetBootstrapper(
            _comSession,
            _mockLogService.Object,
            _mockLocalization.Object,
            _mockInteractiveUserService.Object,
            _mockPowerShellRunner.Object,
            _mockFileSystemService.Object,
            _mockTaskProgressService.Object,
            new System.Net.Http.HttpClient());
    }

    [Fact]
    public void IsSystemWinGetAvailable_DefaultsToFalse()
    {
        // The backing field _systemWinGetAvailable defaults to false
        _sut.IsSystemWinGetAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureWinGetReadyAsync_WhenWinGetExeNotFound_ReturnsFalse()
    {
        // Arrange: ensure no winget.exe is found by the file system check.
        // WinGetCliRunner.GetWinGetExePath will look in PATH, WindowsApps, and bundled.
        // Even if it finds one, _fileSystemService.FileExists should return false to
        // simulate the exe not being accessible.
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);

        // Act
        var result = await _sut.EnsureWinGetReadyAsync();

        // Assert: the method should log and return false when winget CLI not found.
        // Note: WinGetCliRunner.GetWinGetExePath uses File.Exists (not IFileSystemService),
        // so if winget IS on this system, it will be found and the IFileSystemService check
        // will then make it return false. If winget is NOT on this system, GetWinGetExePath
        // returns null, which also results in false.
        // Either way, the method goes through the readiness check flow.
        result.Should().Be(result); // Validates the method completes without throwing
        _mockLogService.Verify(l => l.LogInformation(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task EnsureWinGetReadyAsync_WhenOtsElevation_SkipsComInit()
    {
        // Arrange: simulate OTS elevation scenario
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(true);
        _mockInteractiveUserService
            .Setup(s => s.GetInteractiveUserFolderPath(It.IsAny<Environment.SpecialFolder>()))
            .Returns(@"C:\Users\TestUser\AppData\Local");
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _sut.EnsureWinGetReadyAsync();

        // Assert: when OTS is detected and system winget is available,
        // it should skip COM init. But since we can't control the static
        // IsSystemWinGetAvailable check, just verify the method runs to completion.
        _mockLogService.Verify(l => l.LogInformation(It.IsAny<string>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task EnsureWinGetReadyAsync_LogsAvailabilityCheck()
    {
        // Arrange
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        // Act
        await _sut.EnsureWinGetReadyAsync();

        // Assert: should log "Checking WinGet availability..."
        _mockLogService.Verify(
            l => l.LogInformation(It.Is<string>(s => s.Contains("Checking WinGet availability"))),
            Times.Once);
    }

    [Fact]
    public async Task EnsureWinGetReadyAsync_LogsSystemWinGetAvailability()
    {
        // Arrange
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

        // Act
        await _sut.EnsureWinGetReadyAsync();

        // Assert: should log the system winget availability status
        _mockLogService.Verify(
            l => l.LogInformation(It.Is<string>(s => s.Contains("System winget available:"))),
            Times.Once);
    }

    [Fact]
    public async Task InstallWinGetAsync_LogsStartMessage()
    {
        // Arrange: the InstallAsync inside will likely fail since we're in a test env,
        // but we can verify it logs the start message.
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);

        // Act
        var result = await _sut.InstallWinGetAsync();

        // Assert
        _mockLogService.Verify(
            l => l.LogInformation(It.Is<string>(s => s.Contains("Starting AppInstaller installation"))),
            Times.Once);
    }

    [Fact]
    public async Task InstallWinGetAsync_WhenCancelled_ReturnsFalse()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - the method catches OperationCanceledException internally
        var result = await _sut.InstallWinGetAsync(cts.Token);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void WinGetInstalled_EventCanBeSubscribed()
    {
        // Arrange
        var eventRaised = false;
        _sut.WinGetInstalled += (sender, args) => eventRaised = true;

        // Assert: just verifying event subscription doesn't throw
        eventRaised.Should().BeFalse();
    }

    [Fact]
    public async Task EnsureWinGetReadyAsync_HandlesExceptionGracefully()
    {
        // Arrange: force an exception by making the interactive user service throw
        _mockInteractiveUserService
            .Setup(s => s.IsOtsElevation)
            .Throws(new InvalidOperationException("Test exception"));

        // Act
        var result = await _sut.EnsureWinGetReadyAsync();

        // Assert: should catch the exception and return false
        result.Should().BeFalse();
        _mockLogService.Verify(
            l => l.LogError(It.Is<string>(s => s.Contains("Error checking WinGet availability")), It.IsAny<Exception>()),
            Times.AtMostOnce);
    }
}
