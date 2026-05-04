using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using FluentAssertions;
using Moq;
using Moq.Protected;
using Windows.Foundation;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class AppIconResolverTests : IDisposable
{
    private readonly Mock<IAppxIconSource> _mockIconSource = new();
    private readonly Mock<IStoreIconSource> _mockStoreSource = new();
    private readonly Mock<IBinaryIconSource> _mockBinarySource = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly string _tempCacheDir;
    private readonly AppIconResolver _resolver;

    public AppIconResolverTests()
    {
        _tempCacheDir = Path.Combine(Path.GetTempPath(), "WinhanceTest_" + Path.GetRandomFileName());
        _resolver = new AppIconResolver(
            _mockIconSource.Object,
            _mockLog.Object,
            _tempCacheDir,
            _mockStoreSource.Object,
            _mockBinarySource.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempCacheDir))
            Directory.Delete(_tempCacheDir, recursive: true);
    }

    private static ItemDefinition Def(
        string id,
        string? appxName = null,
        bool installed = true,
        string? msStoreId = null,
        string? winGetId = null,
        string? binaryHint = null) => new()
    {
        Id = id,
        Name = $"App {id}",
        Description = $"Description for {id}",
        AppxPackageName = appxName != null ? new[] { appxName } : null,
        IsInstalled = installed,
        MsStoreId = msStoreId,
        WinGetPackageId = winGetId != null ? new[] { winGetId } : null,
        InstalledBinaryHint = binaryHint,
    };

    private static MemoryStream PngBytes(string label) => new(Encoding.UTF8.GetBytes("PNG-" + label));

    [Fact]
    public async Task ResolveBatchAsync_SkipsDefinitionWithNoAppxPackageName()
    {
        var def = Def("capability1", appxName: null);
        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().BeNull();
        _mockIconSource.Verify(
            s => s.GetLogoStreamAsync(It.IsAny<string>(), It.IsAny<Size>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveBatchAsync_SkipsDefinitionWhenPackageNotInInstalledMap()
    {
        // The IsInstalled flag is no longer the gate — what matters is whether the
        // AppxIconSource (which now spans current-user/all-users/provisioned)
        // surfaces the package in its map. A package not present in any of those
        // scopes simply has no AppX-side icon to extract.
        var def = Def("app1", appxName: "Microsoft.App1", installed: false);
        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().BeNull();
        _mockIconSource.Verify(
            s => s.GetLogoStreamAsync(It.IsAny<string>(), It.IsAny<Size>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveBatchAsync_AttemptsExtraction_EvenWhenIsInstalledFalse_IfPackageIsInMap()
    {
        // Provisioned-but-not-user-installed packages show up in the map (Layer 2/3
        // of AppxIconSource enumeration). The resolver must still attempt extraction
        // for them — otherwise icons for OS apps the user removed would never resolve.
        var def = Def("app1", appxName: "Microsoft.App1", installed: false);
        var fullName = "Microsoft.App1_1.0.0_x64__abc";
        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Microsoft.App1"] = fullName });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync(fullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PngBytes("provisioned"));

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().Be(Path.Combine(_tempCacheDir, fullName + ".96-trim2.png"));
        _mockIconSource.Verify(
            s => s.GetLogoStreamAsync(fullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveBatchAsync_CacheHit_DoesNotCallGetLogoStreamButStampsIconPath()
    {
        var def = Def("app1", appxName: "Microsoft.App1");
        var fullName = "Microsoft.App1_1.0.0_x64__abc";
        var cachePath = Path.Combine(_tempCacheDir, fullName + ".96-trim2.png");

        Directory.CreateDirectory(_tempCacheDir);
        File.WriteAllBytes(cachePath, new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG header bytes

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Microsoft.App1"] = fullName });

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().Be(cachePath);
        _mockIconSource.Verify(
            s => s.GetLogoStreamAsync(It.IsAny<string>(), It.IsAny<Size>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveBatchAsync_CacheMiss_ExtractsAndWritesAndStampsIconPath()
    {
        var def = Def("app1", appxName: "Microsoft.App1");
        var fullName = "Microsoft.App1_1.0.0_x64__abc";

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Microsoft.App1"] = fullName });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync(fullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PngBytes("app1"));

        await _resolver.ResolveBatchAsync(new[] { def });

        var expectedPath = Path.Combine(_tempCacheDir, fullName + ".96-trim2.png");
        def.IconPath.Should().Be(expectedPath);
        File.Exists(expectedPath).Should().BeTrue();
        File.ReadAllText(expectedPath).Should().Be("PNG-app1");
    }

    [Fact]
    public async Task ResolveBatchAsync_CacheMiss_PrunesOldVersionFiles()
    {
        var def = Def("app1", appxName: "Microsoft.App1");
        var oldFullName = "Microsoft.App1_1.0.0_x64__abc";
        var newFullName = "Microsoft.App1_2.0.0_x64__abc";

        Directory.CreateDirectory(_tempCacheDir);
        var oldPath = Path.Combine(_tempCacheDir, oldFullName + ".png");
        File.WriteAllText(oldPath, "old version bytes");

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Microsoft.App1"] = newFullName });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync(newFullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PngBytes("v2"));

        await _resolver.ResolveBatchAsync(new[] { def });

        // Prune sweeps any "<package>_*.png" file (covers both old and new
        // suffix formats) — verifies legacy cache files get cleaned up too.
        var newPath = Path.Combine(_tempCacheDir, newFullName + ".96-trim2.png");
        File.Exists(newPath).Should().BeTrue();
        File.Exists(oldPath).Should().BeFalse();
        def.IconPath.Should().Be(newPath);
    }

    [Fact]
    public async Task ResolveBatchAsync_PerPackageException_LogsWarningAndContinues()
    {
        var def1 = Def("app1", appxName: "Microsoft.App1");
        var def2 = Def("app2", appxName: "Microsoft.App2");

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["Microsoft.App1"] = "Microsoft.App1_1.0.0_x64__abc",
                ["Microsoft.App2"] = "Microsoft.App2_1.0.0_x64__abc",
            });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync("Microsoft.App1_1.0.0_x64__abc", It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        _mockIconSource.Setup(s => s.GetLogoStreamAsync("Microsoft.App2_1.0.0_x64__abc", It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PngBytes("app2"));

        await _resolver.ResolveBatchAsync(new[] { def1, def2 });

        def1.IconPath.Should().BeNull();
        def2.IconPath.Should().NotBeNull();
        _mockLog.Verify(l => l.LogWarning(It.Is<string>(s => s.Contains("app1") || s.Contains("App1"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ResolveBatchAsync_OuterException_LogsErrorAndReturnsWithoutThrowing()
    {
        var def = Def("app1", appxName: "Microsoft.App1");
        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("PackageManager unavailable"));

        var act = async () => await _resolver.ResolveBatchAsync(new[] { def });

        await act.Should().NotThrowAsync();
        def.IconPath.Should().BeNull();
        _mockLog.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ResolveBatchAsync_EmptyInput_ReturnsWithoutCallingSource()
    {
        await _resolver.ResolveBatchAsync(Array.Empty<ItemDefinition>());

        _mockIconSource.Verify(
            s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // --- Store CDN fallback ---

    [Fact]
    public async Task ResolveBatchAsync_StoreFallback_WhenAppxLayerYieldsNothing_FetchesAndStamps()
    {
        var def = Def("app1", appxName: "Microsoft.App1", msStoreId: "9NBLGGH42THS");
        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>()); // AppX layer empty
        _mockStoreSource.Setup(s => s.GetIconStreamAsync("9NBLGGH42THS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PngBytes("store"));

        await _resolver.ResolveBatchAsync(new[] { def });

        var expectedPath = Path.Combine(_tempCacheDir, "MsStore_9NBLGGH42THS.96-trim2.png");
        def.IconPath.Should().Be(expectedPath);
        File.Exists(expectedPath).Should().BeTrue();
        File.ReadAllText(expectedPath).Should().Be("PNG-store");
    }

    [Fact]
    public async Task ResolveBatchAsync_StoreFallback_NotCalled_WhenAppxLayerSucceeds()
    {
        var def = Def("app1", appxName: "Microsoft.App1", msStoreId: "9NBLGGH42THS");
        var fullName = "Microsoft.App1_1.0.0_x64__abc";
        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Microsoft.App1"] = fullName });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync(fullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PngBytes("appx"));

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().Be(Path.Combine(_tempCacheDir, fullName + ".96-trim2.png"));
        _mockStoreSource.Verify(
            s => s.GetIconStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveBatchAsync_StoreFallback_NotCalled_WhenDefHasNoMsStoreId()
    {
        var def = Def("cap1", appxName: null, msStoreId: null); // capability-style entry
        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().BeNull();
        _mockStoreSource.Verify(
            s => s.GetIconStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveBatchAsync_StoreFallback_CacheHit_DoesNotCallStoreSource()
    {
        var def = Def("app1", appxName: "Microsoft.App1", msStoreId: "9NBLGGH42THS");
        var cachePath = Path.Combine(_tempCacheDir, "MsStore_9NBLGGH42THS.96-trim2.png");
        Directory.CreateDirectory(_tempCacheDir);
        File.WriteAllBytes(cachePath, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>()); // no AppX coverage

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().Be(cachePath);
        _mockStoreSource.Verify(
            s => s.GetIconStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // --- Layer 1b: binary icon source ---

    [Fact]
    public async Task ResolveBatchAsync_ResolvesViaBinaryLayer_WhenInstalledBinaryHintSet()
    {
        var def = Def("ext-1", binaryHint: "C:\\PowerToys\\PowerToys.exe");
        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());
        _mockBinarySource.Setup(b => b.GetIconStreamAsync(
                "C:\\PowerToys\\PowerToys.exe",
                It.IsAny<Size>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PngBytes("binary"));

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().NotBeNull();
        def.IconPath.Should().StartWith(_tempCacheDir);
        Path.GetFileName(def.IconPath!).Should().StartWith("Bin_");
    }

    // --- Layer 2 preference: AppX > Store > IconSources ---

    [Fact]
    public async Task ResolveBatchAsync_PrefersAppxOverEverything_WhenInstalled()
    {
        var def = Def("ext-5",
            appxName: "Microsoft.PowerToys",
            msStoreId: "XP89DCGQ3K6VLD",
            binaryHint: "C:\\PowerToys\\PowerToys.exe");

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["Microsoft.PowerToys"] = "Microsoft.PowerToys_0.87.0_x64__abc"
            });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync(
                "Microsoft.PowerToys_0.87.0_x64__abc",
                It.IsAny<Size>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PngBytes("appx"));

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().NotBeNull();
        Path.GetFileName(def.IconPath!).Should().NotStartWith("Bin_")
            .And.NotStartWith("MsStore_")
            .And.NotStartWith("Src_");
        _mockBinarySource.Verify(b => b.GetIconStreamAsync(It.IsAny<string>(), It.IsAny<Size>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockStoreSource.Verify(s => s.GetIconStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // --- Layer 2b: per-entry IconSources (URLs and local file paths) ---

    private AppIconResolver BuildResolverWithHttpClient(HttpClient httpClient) => new(
        _mockIconSource.Object,
        _mockLog.Object,
        _tempCacheDir,
        _mockStoreSource.Object,
        _mockBinarySource.Object,
        httpClient);

    private static Mock<HttpMessageHandler> SetupHandler(HttpStatusCode status, byte[]? body = null)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(status)
            {
                Content = new ByteArrayContent(body ?? new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
            });
        return handler;
    }

    [Fact]
    public async Task ResolveBatchAsync_IconSourcesUrl_FetchesAndStamps_WhenLocalAndStoreEmpty()
    {
        // No AppX, no Store — IconSources is the only available path.
        var def = Def("ext-srcs-1") with
        {
            IconSources = new[] { "https://example.invalid/icon.png" },
        };

        var handler = SetupHandler(HttpStatusCode.OK, Encoding.UTF8.GetBytes("PNG-from-url"));
        using var client = new HttpClient(handler.Object);
        var resolver = BuildResolverWithHttpClient(client);

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        await resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().NotBeNull();
        Path.GetFileName(def.IconPath!).Should().StartWith("Src_");
        File.ReadAllText(def.IconPath!).Should().Be("PNG-from-url");
    }

    [Fact]
    public async Task ResolveBatchAsync_IconSourcesLocalPath_ReadsFromDisk_NoHttpCall()
    {
        // Write a "local" file in the temp dir to act as the on-disk icon (e.g. OneDrive.ico).
        Directory.CreateDirectory(_tempCacheDir);
        var localIconPath = Path.Combine(_tempCacheDir, "fake-onedrive.ico");
        File.WriteAllText(localIconPath, "ICO-bytes");

        var def = Def("ext-srcs-2") with
        {
            IconSources = new[] { localIconPath },   // Bare path, no http(s):// prefix.
        };

        // Strict handler — fails the test if any HTTP call is made.
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        using var client = new HttpClient(handler.Object);
        var resolver = BuildResolverWithHttpClient(client);

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        await resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().NotBeNull();
        Path.GetFileName(def.IconPath!).Should().StartWith("Src_");
        File.ReadAllText(def.IconPath!).Should().Be("ICO-bytes");
    }

    [Fact]
    public async Task ResolveBatchAsync_IconSourcesArray_TriesEachInOrder_FirstHitWins()
    {
        // First source (local) doesn't exist → fall through to second source (URL) → wins.
        var missingPath = Path.Combine(_tempCacheDir, "does-not-exist.ico");
        var def = Def("ext-srcs-3") with
        {
            IconSources = new[]
            {
                missingPath,                                   // Layer 2b try 1: local file, missing
                "https://example.invalid/fallback.png",        // Layer 2b try 2: URL, succeeds
            },
        };

        var handler = SetupHandler(HttpStatusCode.OK, Encoding.UTF8.GetBytes("PNG-fallback"));
        using var client = new HttpClient(handler.Object);
        var resolver = BuildResolverWithHttpClient(client);

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        await resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().NotBeNull();
        Path.GetFileName(def.IconPath!).Should().StartWith("Src_");
        File.ReadAllText(def.IconPath!).Should().Be("PNG-fallback");
    }

    [Fact]
    public async Task ResolveBatchAsync_StoreCdn_PreferredOver_IconSources()
    {
        // Both MsStoreId and IconSources present — Store CDN wins per Marco's
        // chosen layering (Layer 2a runs before Layer 2b).
        var def = Def("ext-srcs-4", msStoreId: "9NBLGGH42THS") with
        {
            IconSources = new[] { "https://example.invalid/should-not-be-used.png" },
        };

        // Strict handler — would fail if IconSources URL were fetched.
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        using var client = new HttpClient(handler.Object);
        var resolver = BuildResolverWithHttpClient(client);

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());
        _mockStoreSource.Setup(s => s.GetIconStreamAsync("9NBLGGH42THS", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PngBytes("store-wins"));

        await resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().NotBeNull();
        Path.GetFileName(def.IconPath!).Should().StartWith("MsStore_");
    }

    [Fact]
    public async Task ResolveBatchAsync_AppxSucceeds_IconSourcesNotConsulted()
    {
        // AppX (Layer 1a) fires first; once IconPath is set, Layer 2 — including
        // IconSources — never runs for this entry.
        var def = Def("ext-srcs-5", appxName: "Microsoft.App") with
        {
            IconSources = new[] { "https://example.invalid/should-not-be-used.png" },
        };

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        using var client = new HttpClient(handler.Object);
        var resolver = BuildResolverWithHttpClient(client);

        var fullName = "Microsoft.App_1.0_x64__abc";
        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Microsoft.App"] = fullName });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync(fullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PngBytes("appx-wins"));

        await resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().NotBeNull();
        Path.GetFileName(def.IconPath!).Should().NotStartWith("Src_");
    }

    [Fact]
    public async Task ResolveBatchAsync_IconSourcesOnlyEntry_BecomesCandidate_WithoutOtherIdentifiers()
    {
        // An entry with ONLY IconSources (no AppX, MsStoreId, InstalledBinaryHint)
        // should still be picked up as a candidate and resolved via Layer 2b.
        var def = new ItemDefinition
        {
            Id = "srcs-only",
            Name = "IconSources-only entry",
            Description = "no other identifiers",
            IconSources = new[] { "https://example.invalid/only.png" },
        };

        var handler = SetupHandler(HttpStatusCode.OK, Encoding.UTF8.GetBytes("PNG-only"));
        using var client = new HttpClient(handler.Object);
        var resolver = BuildResolverWithHttpClient(client);

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        await resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().NotBeNull();
        Path.GetFileName(def.IconPath!).Should().StartWith("Src_");
    }

    // --- Layer 2b: data: URIs ---

    private static string Sha1HexLower(string input)
    {
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        return Convert.ToHexString(sha1.ComputeHash(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    [Fact]
    public async Task ResolveBatchAsync_IconSourcesDataUri_DecodesAndStamps_NoHttpCall()
    {
        // Embedded base64 PNG payload. Strict HTTP handler asserts the resolver
        // never reaches for the network when the source is a data: URI.
        var payload = Encoding.UTF8.GetBytes("PNG-from-data-uri");
        var dataUri = "data:image/png;base64," + Convert.ToBase64String(payload);

        var def = Def("ext-srcs-data") with { IconSources = new[] { dataUri } };

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        using var client = new HttpClient(handler.Object);
        var resolver = BuildResolverWithHttpClient(client);

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        await resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().NotBeNull();
        Path.GetFileName(def.IconPath!).Should().StartWith("Src_");
        File.ReadAllText(def.IconPath!).Should().Be("PNG-from-data-uri");
    }

    [Fact]
    public async Task ResolveBatchAsync_IconSourcesDataUri_CacheHit_DoesNotRedecode()
    {
        // Pre-warm the cache at the exact path the resolver will compute. A second
        // resolve call should short-circuit on the cache hit and leave the file untouched.
        var payload = Encoding.UTF8.GetBytes("PNG-real-payload");
        var dataUri = "data:image/png;base64," + Convert.ToBase64String(payload);

        Directory.CreateDirectory(_tempCacheDir);
        var cachePath = Path.Combine(_tempCacheDir, "Src_" + Sha1HexLower(dataUri) + ".96-trim2.png");
        File.WriteAllText(cachePath, "pre-cached-bytes");

        var def = Def("ext-srcs-data-cached") with { IconSources = new[] { dataUri } };

        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        using var client = new HttpClient(handler.Object);
        var resolver = BuildResolverWithHttpClient(client);

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        await resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().Be(cachePath);
        File.ReadAllText(cachePath).Should().Be("pre-cached-bytes");
    }

    [Fact]
    public async Task ResolveBatchAsync_IconSourcesDataUri_NoBase64Marker_FallsThrough()
    {
        // data: URI without ;base64 (e.g. URL-encoded text payload) is rejected;
        // the resolver should continue to the next source.
        var def = Def("ext-srcs-data-nobase64") with
        {
            IconSources = new[]
            {
                "data:image/png,not-base64-encoded",
                "https://example.invalid/fallback.png",
            },
        };

        var handler = SetupHandler(HttpStatusCode.OK, Encoding.UTF8.GetBytes("PNG-fallback"));
        using var client = new HttpClient(handler.Object);
        var resolver = BuildResolverWithHttpClient(client);

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        await resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().NotBeNull();
        File.ReadAllText(def.IconPath!).Should().Be("PNG-fallback");
    }

    [Fact]
    public async Task ResolveBatchAsync_IconSourcesDataUri_InvalidBase64_FallsThrough()
    {
        // Malformed base64 payload: decoder throws FormatException → resolver
        // treats it as a miss and tries the next source.
        var def = Def("ext-srcs-data-badbase64") with
        {
            IconSources = new[]
            {
                "data:image/png;base64,!!!not valid base64!!!",
                "https://example.invalid/fallback.png",
            },
        };

        var handler = SetupHandler(HttpStatusCode.OK, Encoding.UTF8.GetBytes("PNG-fallback"));
        using var client = new HttpClient(handler.Object);
        var resolver = BuildResolverWithHttpClient(client);

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        await resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().NotBeNull();
        File.ReadAllText(def.IconPath!).Should().Be("PNG-fallback");
    }
}
