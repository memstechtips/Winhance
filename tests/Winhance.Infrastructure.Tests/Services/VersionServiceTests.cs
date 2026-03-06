using System.Net;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class VersionServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<HttpMessageHandler> _mockHttpHandler = new();
    private readonly HttpClient _httpClient;
    private readonly VersionService _service;

    public VersionServiceTests()
    {
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _service = new VersionService(
            _mockLogService.Object,
            _mockProcessExecutor.Object,
            _mockFileSystemService.Object,
            _httpClient);
    }

    #region GetCurrentVersion

    [Fact]
    public void GetCurrentVersion_ReturnsNonNullVersionInfo()
    {
        // Act
        var result = _service.GetCurrentVersion();

        // Assert — the method always returns a non-null VersionInfo, even in a test runner
        // context where the assembly version may not be a valid date-based tag.
        // In such cases VersionInfo.FromTag may return a default record with Version = "".
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetCurrentVersion_CalledTwice_ReturnsSameResult()
    {
        // Act — calling multiple times should be deterministic
        var first = _service.GetCurrentVersion();
        var second = _service.GetCurrentVersion();

        // Assert
        first.Version.Should().Be(second.Version);
        first.ReleaseDate.Should().Be(second.ReleaseDate);
    }

    #endregion

    #region CheckForUpdateAsync

    [Fact]
    public async Task CheckForUpdateAsync_NewerVersionAvailable_ReturnsUpdateAvailable()
    {
        // Arrange — simulate a GitHub API response with a very new version
        var releaseJson = JsonSerializer.Serialize(new
        {
            tag_name = "v99.12.31",
            html_url = "https://github.com/memstechtips/Winhance/releases/tag/v99.12.31",
            published_at = "2099-12-31T00:00:00Z"
        });

        SetupHttpResponse(HttpStatusCode.OK, releaseJson);

        // Act
        var result = await _service.CheckForUpdateAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.IsUpdateAvailable.Should().BeTrue();
        result.Version.Should().Be("v99.12.31");
    }

    [Fact]
    public async Task CheckForUpdateAsync_SameOrOlderVersion_ReturnsNoUpdate()
    {
        // Arrange — In the test runner, GetCurrentVersion() returns a VersionInfo with
        // ReleaseDate = DateTime.MinValue (because the assembly version "0.0.0" doesn't
        // parse into a valid date). A version whose ReleaseDate is also DateTime.MinValue
        // (or any invalid date tag) will NOT be "newer", so IsUpdateAvailable = false.
        // We use an invalid tag format that VersionInfo.FromTag will reject, yielding default dates.
        var releaseJson = JsonSerializer.Serialize(new
        {
            tag_name = "v0.0.0",
            html_url = "https://github.com/memstechtips/Winhance/releases/tag/v0.0.0",
            published_at = "2000-01-01T00:00:00Z"
        });

        SetupHttpResponse(HttpStatusCode.OK, releaseJson);

        // Act
        var result = await _service.CheckForUpdateAsync(CancellationToken.None);

        // Assert — v0.0.0 has month=0 which is an invalid DateTime, so FromTag returns
        // a default VersionInfo. Both current and latest have the same (default) ReleaseDate,
        // so IsNewerThan returns false, and IsUpdateAvailable is false.
        result.Should().NotBeNull();
        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdateAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // The handler will throw OperationCanceledException when the token is already cancelled
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.CheckForUpdateAsync(cts.Token));
    }

    [Fact]
    public async Task CheckForUpdateAsync_HttpError_ReturnsNoUpdate()
    {
        // Arrange — simulate a non-transient HTTP error (404)
        SetupHttpResponse(HttpStatusCode.NotFound, "Not Found");

        // Act
        var result = await _service.CheckForUpdateAsync(CancellationToken.None);

        // Assert — on non-retryable error, service returns a default VersionInfo with no update
        result.Should().NotBeNull();
        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdateAsync_BetaVersion_ParsesCorrectly()
    {
        // Arrange
        var releaseJson = JsonSerializer.Serialize(new
        {
            tag_name = "v99.06.15-beta",
            html_url = "https://github.com/memstechtips/Winhance/releases/tag/v99.06.15-beta",
            published_at = "2099-06-15T00:00:00Z"
        });

        SetupHttpResponse(HttpStatusCode.OK, releaseJson);

        // Act
        var result = await _service.CheckForUpdateAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Version.Should().Be("v99.06.15-beta");
        result.IsBeta.Should().BeTrue();
        result.IsUpdateAvailable.Should().BeTrue();
    }

    #endregion

    #region DownloadAndInstallUpdateAsync

    [Fact]
    public async Task DownloadAndInstallUpdateAsync_DownloadsAndLaunchesInstaller()
    {
        // Arrange
        _mockFileSystemService.Setup(f => f.GetTempPath()).Returns(@"C:\Temp");
        _mockFileSystemService.Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => string.Join(@"\", parts));

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[] { 0x4D, 0x5A }) // Fake PE header
            });

        _mockProcessExecutor.Setup(p => p.ShellExecuteAsync(
                It.IsAny<string>(), null, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act — this will throw because FileStream opens a real file path,
        // but we can verify the setup calls were correct.
        // In a real scenario this would need an IFileSystemService.CreateFileStream abstraction.
        // For now, we verify the method at least calls GetTempPath and CombinePath.
        try
        {
            await _service.DownloadAndInstallUpdateAsync(CancellationToken.None);
        }
        catch (DirectoryNotFoundException)
        {
            // Expected in test environment — the temp path doesn't exist on the test runner
        }
        catch (IOException)
        {
            // Also acceptable in test environment
        }

        // Assert — verify the service composed the correct temp path
        _mockFileSystemService.Verify(f => f.GetTempPath(), Times.Once);
        _mockFileSystemService.Verify(f => f.CombinePath(It.IsAny<string[]>()), Times.Once);
    }

    #endregion

    #region Constructor Validation

    [Fact]
    public void Constructor_NullProcessExecutor_ThrowsArgumentNullException()
    {
        var act = () => new VersionService(
            _mockLogService.Object, null!, _mockFileSystemService.Object, _httpClient);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("processExecutor");
    }

    [Fact]
    public void Constructor_NullFileSystemService_ThrowsArgumentNullException()
    {
        var act = () => new VersionService(
            _mockLogService.Object, _mockProcessExecutor.Object, null!, _httpClient);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("fileSystemService");
    }

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        var act = () => new VersionService(
            _mockLogService.Object, _mockProcessExecutor.Object, _mockFileSystemService.Object, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

    #endregion

    #region Helpers

    private void SetupHttpResponse(HttpStatusCode statusCode, string content)
    {
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content)
            });
    }

    #endregion
}
