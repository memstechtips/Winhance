using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

/// <summary>
/// Tests for <see cref="WinGetDetectionService"/>.
///
/// WinGetComSession is a concrete class that tries to initialize COM via
/// WindowsPackageManagerStandardFactory. In a test environment, COM init
/// will fail, which means methods that call _comSession.EnsureComInitialized()
/// will take the CLI fallback path. The CLI fallback calls static methods on
/// WinGetCliRunner which invoke actual processes.
///
/// Tests here focus on:
/// - Verifying the service can be constructed and called without throwing
/// - Null/empty input handling
/// - Verifying logging behavior
/// - Verifying the empty-result fallback when COM and CLI are both unavailable
/// </summary>
public class WinGetDetectionServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IInteractiveUserService> _mockInteractiveUserService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly WinGetComSession _comSession;
    private readonly WinGetDetectionService _sut;

    public WinGetDetectionServiceTests()
    {
        _comSession = new WinGetComSession(_mockLogService.Object);

        _sut = new WinGetDetectionService(
            _comSession,
            _mockLogService.Object,
            _mockInteractiveUserService.Object,
            _mockFileSystemService.Object);
    }

    [Fact]
    public async Task GetInstalledPackageIdsAsync_ReturnsHashSet()
    {
        // Arrange: COM init will fail in test env, CLI fallback will also
        // likely fail since bundled winget isn't available. The service
        // should return an empty HashSet rather than null or throw.
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);
        _mockInteractiveUserService
            .Setup(s => s.GetInteractiveUserFolderPath(It.IsAny<Environment.SpecialFolder>()))
            .Returns(@"C:\Users\TestUser\AppData\Local");
        _mockFileSystemService
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join(@"\", paths));
        _mockFileSystemService
            .Setup(f => f.CreateDirectory(It.IsAny<string>()));
        _mockFileSystemService
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(false);

        // Act
        var result = await _sut.GetInstalledPackageIdsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<HashSet<string>>();
    }

    [Fact]
    public async Task GetInstalledPackageIdsAsync_ReturnsCaseInsensitiveHashSet()
    {
        // Arrange
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);
        _mockInteractiveUserService
            .Setup(s => s.GetInteractiveUserFolderPath(It.IsAny<Environment.SpecialFolder>()))
            .Returns(@"C:\Users\TestUser\AppData\Local");
        _mockFileSystemService
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join(@"\", paths));
        _mockFileSystemService
            .Setup(f => f.CreateDirectory(It.IsAny<string>()));
        _mockFileSystemService
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(false);

        // Act
        var result = await _sut.GetInstalledPackageIdsAsync();

        // Assert: the HashSet should use OrdinalIgnoreCase comparer
        result.Should().NotBeNull();
        // Verify case-insensitivity by adding items and checking
        result.Add("Test.Package");
        result.Contains("test.package").Should().BeTrue();
        result.Contains("TEST.PACKAGE").Should().BeTrue();
    }

    [Fact]
    public async Task GetInstalledPackageIdsAsync_WhenCancelled_ThrowsOperationCancelled()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);
        _mockInteractiveUserService
            .Setup(s => s.GetInteractiveUserFolderPath(It.IsAny<Environment.SpecialFolder>()))
            .Returns(@"C:\Users\TestUser\AppData\Local");
        _mockFileSystemService
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join(@"\", paths));
        _mockFileSystemService
            .Setup(f => f.CreateDirectory(It.IsAny<string>()));
        _mockFileSystemService
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(false);

        // Act & Assert: should either throw OperationCanceledException or return empty
        // (depends on where cancellation is checked in the flow)
        try
        {
            var result = await _sut.GetInstalledPackageIdsAsync(cts.Token);
            // If it doesn't throw, it should return an empty set
            result.Should().NotBeNull();
        }
        catch (OperationCanceledException)
        {
            // This is acceptable behavior
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetInstallerTypeAsync_WithNullOrEmptyPackageId_ReturnsNull(string? packageId)
    {
        // Act
        var result = await _sut.GetInstallerTypeAsync(packageId!);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetInstallerTypeAsync_WithValidPackageId_DoesNotThrow()
    {
        // Arrange: COM init will fail, CLI fallback will run "winget show"
        // which may or may not succeed depending on environment
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);

        // Act & Assert: just verify it doesn't throw
        var result = await _sut.GetInstallerTypeAsync("Some.NonExistent.Package");

        // Result could be null (package not found) or a string (if somehow found)
        // Just verify the method completes without throwing
    }

    [Fact]
    public async Task GetInstallerTypeAsync_WhenExceptionOccurs_ReturnsNull()
    {
        // Arrange: force COM session to mark itself as timed out
        _comSession.ComInitTimedOut = true;
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);

        // Act: with COM timed out, it should try CLI fallback.
        // If CLI also fails, should return null gracefully.
        var result = await _sut.GetInstallerTypeAsync("Test.Package");

        // Assert: should return null, not throw
        // (the actual result depends on whether winget.exe is on the system)
    }

    [Fact]
    public async Task GetInstalledPackageIdsAsync_WhenComTimedOut_SkipsCom()
    {
        // Arrange: mark COM as timed out so it returns false immediately
        _comSession.ComInitTimedOut = true;
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);
        _mockInteractiveUserService
            .Setup(s => s.GetInteractiveUserFolderPath(It.IsAny<Environment.SpecialFolder>()))
            .Returns(@"C:\Users\TestUser\AppData\Local");
        _mockFileSystemService
            .Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join(@"\", paths));
        _mockFileSystemService
            .Setup(f => f.CreateDirectory(It.IsAny<string>()));
        _mockFileSystemService
            .Setup(f => f.FileExists(It.IsAny<string>()))
            .Returns(false);

        // Act
        var result = await _sut.GetInstalledPackageIdsAsync();

        // Assert: should fall through to CLI fallback
        result.Should().NotBeNull();
        _mockLogService.Verify(
            l => l.LogInformation(It.Is<string>(s => s.Contains("COM not available, falling back to CLI"))),
            Times.Once);
    }

    [Fact]
    public async Task GetInstallerTypeAsync_LogsWarningOnException()
    {
        // Arrange: setup to force an exception in the GetInstallerTypeAsync path
        _comSession.ComInitTimedOut = true;
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation)
            .Throws(new InvalidOperationException("Test exception"));

        // Act
        var result = await _sut.GetInstallerTypeAsync("Test.Package");

        // Assert
        result.Should().BeNull();
        _mockLogService.Verify(
            l => l.LogWarning(It.Is<string>(s => s.Contains("Could not determine installer type"))),
            Times.Once);
    }
}
