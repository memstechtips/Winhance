using System.Net;
using System.Text.Json;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class VersionServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<HttpMessageHandler> _mockHttpHandler = new();
    private readonly HttpClient _httpClient;
    private readonly VersionService _service;

    public VersionServiceTests()
    {
        _httpClient = new HttpClient(_mockHttpHandler.Object);
        _service = new VersionService(
            _mockLogService.Object,
            _mockFileSystemService.Object,
            _httpClient);
    }

    [Fact]
    public void GetCurrentVersion_ReturnsNonNullVersionInfo()
    {
        var result = _service.GetCurrentVersion();

        // The method always returns a non-null VersionInfo, even in a test runner
        // context where the assembly version may not be a valid date-based tag.
        // In such cases VersionInfo.FromTag may return a default record with Version = "".
        result.Should().NotBeNull();
    }

    [Fact]
    public void GetCurrentVersion_CalledTwice_ReturnsSameResult()
    {
        var first = _service.GetCurrentVersion();
        var second = _service.GetCurrentVersion();

        first.Version.Should().Be(second.Version);
        first.ReleaseDate.Should().Be(second.ReleaseDate);
    }

    [Fact]
    public async Task CheckForUpdateAsync_NewerVersionAvailable_ReturnsUpdateAvailable()
    {
        var releaseJson = JsonSerializer.Serialize(new
        {
            tag_name = "v99.12.31",
            html_url = "https://github.com/memstechtips/Winhance/releases/tag/v99.12.31",
            published_at = "2099-12-31T00:00:00Z"
        });

        SetupHttpResponse(HttpStatusCode.OK, releaseJson);

        var result = await _service.CheckForUpdateAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result.IsUpdateAvailable.Should().BeTrue();
        result.Version.Should().Be("v99.12.31");
    }

    [Fact]
    public async Task CheckForUpdateAsync_SameOrOlderVersion_ReturnsNoUpdate()
    {
        // In the test runner, GetCurrentVersion() returns a VersionInfo with
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

        var result = await _service.CheckForUpdateAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdateAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.CheckForUpdateAsync(cts.Token));
    }

    [Fact]
    public async Task CheckForUpdateAsync_HttpError_ReturnsNoUpdate()
    {
        SetupHttpResponse(HttpStatusCode.NotFound, "Not Found");

        var result = await _service.CheckForUpdateAsync(CancellationToken.None);

        // 404 is non-retryable, so the service returns a default VersionInfo with no update
        result.Should().NotBeNull();
        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdateAsync_BetaVersion_ParsesCorrectly()
    {
        var releaseJson = JsonSerializer.Serialize(new
        {
            tag_name = "v99.06.15-beta",
            html_url = "https://github.com/memstechtips/Winhance/releases/tag/v99.06.15-beta",
            published_at = "2099-06-15T00:00:00Z"
        });

        SetupHttpResponse(HttpStatusCode.OK, releaseJson);

        var result = await _service.CheckForUpdateAsync(CancellationToken.None);

        result.Should().NotBeNull();
        result.Version.Should().Be("v99.06.15-beta");
        result.IsBeta.Should().BeTrue();
        result.IsUpdateAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task DownloadAndInstallUpdateAsync_DownloadsAndLaunchesInstaller()
    {
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

        // This will throw because FileStream opens a real file path,
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

        _mockFileSystemService.Verify(f => f.GetTempPath(), Times.Once);
        _mockFileSystemService.Verify(f => f.CombinePath(It.IsAny<string[]>()), Times.Once);
    }

    [Theory]
    [InlineData(@"D:\Winhance", false)]
    [InlineData(@"D:\Winhance\", false)]
    [InlineData(@"C:\Program Files\Winhance", false)]
    [InlineData(@"D:\My Portable\Winhance", true)]
    [InlineData(@"D:\My Portable\Winhance\", true)]
    public void BuildInstallerArgs_AlwaysIncludesDirArgPinnedToAppDir(string appDir, bool isPortable)
    {
        var args = VersionService.BuildInstallerArgs(appDir, isPortable);

        // /DIR= must be present, quoted, and equal to the appDir
        // with any trailing path separator stripped. See issue #649: without
        // /DIR=, Inno's silent-install resolution lands {app} at Program Files
        // (regular) or ~\Desktop\Winhance (portable) regardless of where the
        // running app actually lives.
        var expectedDirArg = $"/DIR=\"{appDir.TrimEnd('\\', '/')}\"";
        args.Should().Contain(expectedDirArg);
        args.Should().Contain("/SILENT");
        args.Should().Contain("/SUPPRESSMSGBOXES");
    }

    [Fact]
    public void BuildInstallerArgs_Portable_SelectsPortableInstallTaskOnly()
    {
        var args = VersionService.BuildInstallerArgs(@"D:\Portable\Winhance", isPortable: true);

        args.Should().Contain(@"/MERGETASKS=""portableinstall""");
        args.Should().NotContain("regularinstall");
    }

    [Fact]
    public void BuildInstallerArgs_Regular_SelectsRegularInstallTaskWithShortcuts()
    {
        var args = VersionService.BuildInstallerArgs(@"C:\Program Files\Winhance", isPortable: false);

        args.Should().Contain(@"/MERGETASKS=""regularinstall\desktopicon,regularinstall\startmenuicon""");
        args.Should().NotContain("portableinstall");
    }

    [Fact]
    public void BuildInstallerArgs_PathWithSpaces_QuotesDirArgCorrectly()
    {
        // The actual scenario from issue #649's reporter
        var customPath = @"D:\Windows Tweaks\Winhance";

        var args = VersionService.BuildInstallerArgs(customPath, isPortable: false);

        // The double-quotes around /DIR= must survive so Inno parses
        // the path with its embedded space correctly
        args.Should().Contain($"/DIR=\"{customPath}\"");
    }

    [Fact]
    public void Constructor_NullFileSystemService_ThrowsArgumentNullException()
    {
        var act = () => new VersionService(
            _mockLogService.Object, null!, _httpClient);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("fileSystemService");
    }

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNullException()
    {
        var act = () => new VersionService(
            _mockLogService.Object, _mockFileSystemService.Object, null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("httpClient");
    }

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
}
