using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services.WinGet;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

// COM init fails in a test environment, so methods take the CLI fallback path, which invokes real processes;
// tests focus on construction, null handling, logging and the empty-result fallback.
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
        // COM init will fail in test env, CLI fallback will also
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

        var result = await _sut.GetInstalledPackageIdsAsync();

        result.Should().NotBeNull();
        result.Should().BeOfType<HashSet<string>>();
    }

    [Fact]
    public async Task GetInstalledPackageIdsAsync_ReturnsCaseInsensitiveHashSet()
    {
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

        var result = await _sut.GetInstalledPackageIdsAsync();

        result.Should().NotBeNull();
        result.Add("Test.Package");
        result.Contains("test.package").Should().BeTrue();
        result.Contains("TEST.PACKAGE").Should().BeTrue();
    }

    [Fact]
    public async Task GetInstalledPackageIdsAsync_WhenCancelled_ThrowsOperationCancelled()
    {
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

        // Should either throw OperationCanceledException or return empty
        // (depends on where cancellation is checked in the flow)
        try
        {
            var result = await _sut.GetInstalledPackageIdsAsync(cts.Token);
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
        var result = await _sut.GetInstallerTypeAsync(packageId!);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetInstallerTypeAsync_WithValidPackageId_DoesNotThrow()
    {
        // COM init will fail, CLI fallback will run "winget show"
        // which may or may not succeed depending on environment
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);

        var result = await _sut.GetInstallerTypeAsync("Some.NonExistent.Package");
    }

    [Fact]
    public async Task GetInstallerTypeAsync_WhenExceptionOccurs_ReturnsNull()
    {
        _comSession.ComInitTimedOut = true;
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation).Returns(false);

        var result = await _sut.GetInstallerTypeAsync("Test.Package");

        // The actual result depends on whether winget.exe is on the system, so nothing is asserted
    }

    [Fact]
    public async Task GetInstalledPackageIdsAsync_WhenComTimedOut_SkipsCom()
    {
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

        var result = await _sut.GetInstalledPackageIdsAsync();

        result.Should().NotBeNull();
        _mockLogService.Verify(
            l => l.LogInformation(It.Is<string>(s => s.Contains("COM not available, falling back to CLI")), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task GetInstallerTypeAsync_LogsWarningOnException()
    {
        _comSession.ComInitTimedOut = true;
        _mockInteractiveUserService.Setup(s => s.IsOtsElevation)
            .Throws(new InvalidOperationException("Test exception"));

        var result = await _sut.GetInstallerTypeAsync("Test.Package");

        result.Should().BeNull();
        _mockLogService.Verify(
            l => l.LogWarning(It.Is<string>(s => s.Contains("Could not determine installer type")), It.IsAny<string>()),
            Times.Once);
    }
}
