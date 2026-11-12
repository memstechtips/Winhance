using System.Net;
using System.Security.Cryptography;
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
    public async Task DownloadAndInstallUpdateAsync_MatchingDigest_KeepsVerifiedInstaller()
    {
        using var temp = new TempDirectory();
        SetupTempPath(temp.Path);
        var installer = new byte[] { 0x4D, 0x5A, 1, 2, 3 };
        SetupDownload(ReleaseJson("sha256:" + Sha256Of(installer)), installer);

        await _service.DownloadAndInstallUpdateAsync(CancellationToken.None);

        File.ReadAllBytes(InstallerPath(temp)).Should().Equal(installer);
        _mockHttpHandler.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.AbsoluteUri == DownloadUrl),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task DownloadAndInstallUpdateAsync_WrongDigest_DeletesFileAndThrows()
    {
        using var temp = new TempDirectory();
        SetupTempPath(temp.Path);
        SetupDownload(ReleaseJson("sha256:" + Sha256Of(new byte[] { 9, 9, 9 })), new byte[] { 0x4D, 0x5A, 1 });

        var act = () => _service.DownloadAndInstallUpdateAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*did not match the checksum*");
        File.Exists(InstallerPath(temp)).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("md5:abc")]
    public async Task DownloadAndInstallUpdateAsync_MissingOrUnsupportedDigest_ThrowsWithoutDownloading(string? digest)
    {
        using var temp = new TempDirectory();
        SetupTempPath(temp.Path);
        SetupDownload(ReleaseJson(digest), new byte[] { 0x4D, 0x5A });

        var act = () => _service.DownloadAndInstallUpdateAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*checksum*");
        File.Exists(InstallerPath(temp)).Should().BeFalse();
    }

    [Fact]
    public async Task DownloadAndInstallUpdateAsync_NoInstallerAsset_Throws()
    {
        using var temp = new TempDirectory();
        SetupTempPath(temp.Path);
        SetupDownload(JsonSerializer.Serialize(new { tag_name = "v1", assets = Array.Empty<object>() }), new byte[] { 1 });

        var act = () => _service.DownloadAndInstallUpdateAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void OpenVerifiedInstaller_MatchingHash_ReturnsStreamThatBlocksWritesAndDeletes()
    {
        using var temp = new TempDirectory();
        string path = System.IO.Path.Combine(temp.Path, "Winhance.Installer.exe");
        var installer = new byte[] { 1, 2, 3, 4 };
        File.WriteAllBytes(path, installer);

        using var held = VersionService.OpenVerifiedInstaller(path, Sha256Of(installer));

        var write = () => File.WriteAllBytes(path, new byte[] { 9 });
        var delete = () => File.Delete(path);
        write.Should().Throw<IOException>();
        delete.Should().Throw<IOException>();
    }

    [Fact]
    public void OpenVerifiedInstaller_FileChangedAfterDownload_ThrowsAndReleasesFile()
    {
        using var temp = new TempDirectory();
        string path = System.IO.Path.Combine(temp.Path, "Winhance.Installer.exe");
        File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
        string expected = Sha256Of(new byte[] { 1, 2, 3, 4 });
        File.WriteAllBytes(path, new byte[] { 6, 6, 6 });

        var act = () => VersionService.OpenVerifiedInstaller(path, expected);

        act.Should().Throw<InvalidOperationException>();
        File.Delete(path);
        File.Exists(path).Should().BeFalse();
    }

    [Fact]
    public void LaunchInstallerAndRestart_NothingDownloaded_Throws()
    {
        var act = () => _service.LaunchInstallerAndRestart();

        act.Should().Throw<InvalidOperationException>();
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

    private const string DownloadUrl = "https://github.com/memstechtips/Winhance/releases/download/v26.06.12/Winhance.Installer.exe";

    private static string Sha256Of(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string InstallerPath(TempDirectory temp) => System.IO.Path.Combine(temp.Path, "Winhance.Installer.exe");

    private static string ReleaseJson(string? digest) => JsonSerializer.Serialize(new
    {
        tag_name = "v26.06.12",
        assets = new object[]
        {
            new { name = "Other.zip", digest = "sha256:00", browser_download_url = "https://example.invalid/Other.zip" },
            new { name = "Winhance.Installer.exe", digest, browser_download_url = DownloadUrl },
        },
    });

    private void SetupTempPath(string tempPath)
    {
        _mockFileSystemService.Setup(f => f.GetTempPath()).Returns(tempPath);
        _mockFileSystemService.Setup(f => f.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] parts) => System.IO.Path.Combine(parts));
    }

    private void SetupDownload(string releaseJson, byte[] installerBytes)
    {
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.Host == "api.github.com"),
                ItExpr.IsAny<CancellationToken>())
            .Returns(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(releaseJson) }));
        _mockHttpHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.AbsoluteUri == DownloadUrl),
                ItExpr.IsAny<CancellationToken>())
            .Returns(() => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(installerBytes) }));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "winhance-vs-" + Guid.NewGuid().ToString("N"));

        public TempDirectory() => Directory.CreateDirectory(Path);

        public void Dispose() => Directory.Delete(Path, recursive: true);
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
