using System.IO;
using System.Text;
using FluentAssertions;
using Moq;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.SoftwareApps.Interfaces;
using Winhance.Core.Features.SoftwareApps.Models;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Winhance.Infrastructure.Tests.Helpers;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class AppIconResolverTests : IDisposable
{
    private readonly Mock<IAppxIconSource> _mockIconSource = new();
    private readonly Mock<IRepoIconSource> _mockRepoSource = new();
    private readonly Mock<IIconManifestService> _mockManifest = new();
    private readonly Mock<IBinaryIconSource> _mockBinarySource = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly string _tempCacheDir;
    private readonly AppIconResolver _resolver;

    public AppIconResolverTests()
    {
        _tempCacheDir = Path.Combine(Path.GetTempPath(), "WinhanceTest_" + Path.GetRandomFileName());

        // Pre-seed the schema sentinel at the current version so IconCacheMigration's
        // one-time wipe is a no-op for this test's cache root. Without this a test
        // that pre-writes a cache file (cache-hit / prune cases) could have it wiped
        // by the migration that ResolveBatchAsync runs on first use.
        Directory.CreateDirectory(_tempCacheDir);
        File.WriteAllText(
            Path.Combine(_tempCacheDir, ".schema"),
            IconCacheMigration.CurrentSchemaVersion.ToString());

        _resolver = new AppIconResolver(
            _mockIconSource.Object,
            _mockLog.Object,
            _tempCacheDir,
            _mockRepoSource.Object,
            _mockManifest.Object,
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

    private static string Sha1HexLower(string input)
    {
        using var sha1 = System.Security.Cryptography.SHA1.Create();
        return Convert.ToHexString(sha1.ComputeHash(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    /// <summary>Mirrors AppIconResolver.BuildCacheFileName for path assertions.</summary>
    private static string BuildCacheFileName(string defId, string sourceKey) =>
        $"{defId}.{Sha1HexLower(sourceKey).Substring(0, 8)}.png";

    // ===== Candidate selection / no-op cases =====

    [Fact]
    public async Task ResolveBatchAsync_SkipsDefinitionWithNoRoutableIdentity()
    {
        // A capability-style entry (no AppX names, no IconSources, and an id that
        // isn't external-app-*/windows-app-*) has no icon identity at all.
        var def = Def("capability1", appxName: null);
        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().BeNull();
        _mockIconSource.Verify(
            s => s.GetLogoStreamAsync(It.IsAny<string>(), It.IsAny<Size>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockRepoSource.Verify(
            r => r.GetIconBytesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveBatchAsync_EmptyInput_ReturnsWithoutCallingSource()
    {
        await _resolver.ResolveBatchAsync(Array.Empty<ItemDefinition>());

        _mockIconSource.Verify(
            s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveBatchAsync_OuterException_LogsErrorAndReturnsWithoutThrowing()
    {
        var def = Def("windows-app-x", appxName: "Microsoft.App1");
        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("PackageManager unavailable"));

        var act = async () => await _resolver.ResolveBatchAsync(new[] { def });

        await act.Should().NotThrowAsync();
        def.IconPath.Should().BeNull();
        _mockLog.Verify(l => l.LogError(It.IsAny<string>(), It.IsAny<Exception>()), Times.AtLeastOnce);
    }

    // ===== Layer 1: AppX extraction =====

    [Fact]
    public async Task ResolveBatchAsync_WindowsApp_PackageInInstalledMap_ExtractsViaAppx()
    {
        var def = Def("windows-app-app1", appxName: "Microsoft.App1");
        var fullName = "Microsoft.App1_1.0.0_x64__abc";

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Microsoft.App1"] = fullName });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync(fullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PngBytes("app1"));

        await _resolver.ResolveBatchAsync(new[] { def });

        var expectedPath = Path.Combine(_tempCacheDir, BuildCacheFileName(def.Id, fullName));
        def.IconPath.Should().Be(expectedPath);
        File.Exists(expectedPath).Should().BeTrue();
        File.ReadAllText(expectedPath).Should().Be("PNG-app1");

        // Repo must NOT be consulted when AppX extraction succeeds.
        _mockRepoSource.Verify(
            r => r.GetIconBytesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveBatchAsync_AppxCacheHit_DoesNotCallGetLogoStreamButStampsIconPath()
    {
        var def = Def("windows-app-app1", appxName: "Microsoft.App1");
        var fullName = "Microsoft.App1_1.0.0_x64__abc";
        var cachePath = Path.Combine(_tempCacheDir, BuildCacheFileName(def.Id, fullName));

        Directory.CreateDirectory(_tempCacheDir);
        File.WriteAllBytes(cachePath, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Microsoft.App1"] = fullName });

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().Be(cachePath);
        _mockIconSource.Verify(
            s => s.GetLogoStreamAsync(It.IsAny<string>(), It.IsAny<Size>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveBatchAsync_AppxCacheMiss_PrunesOldVersionFiles()
    {
        var def = Def("windows-app-app1", appxName: "Microsoft.App1");
        var oldFullName = "Microsoft.App1_1.0.0_x64__abc";
        var newFullName = "Microsoft.App1_2.0.0_x64__abc";

        Directory.CreateDirectory(_tempCacheDir);
        var oldPath = Path.Combine(_tempCacheDir, BuildCacheFileName(def.Id, oldFullName));
        File.WriteAllText(oldPath, "old version bytes");

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Microsoft.App1"] = newFullName });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync(newFullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PngBytes("v2"));

        await _resolver.ResolveBatchAsync(new[] { def });

        var newPath = Path.Combine(_tempCacheDir, BuildCacheFileName(def.Id, newFullName));
        File.Exists(newPath).Should().BeTrue();
        File.Exists(oldPath).Should().BeFalse();
        def.IconPath.Should().Be(newPath);
    }

    [Fact]
    public async Task ResolveBatchAsync_PerPackageException_LogsWarningAndContinues()
    {
        var def1 = Def("windows-app-app1", appxName: "Microsoft.App1");
        var def2 = Def("windows-app-app2", appxName: "Microsoft.App2");

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
    public async Task ResolveBatchAsync_MultipleAppxNames_UsesFirstMatchInInstalledMap()
    {
        // Mirrors the Xbox case: a definition declares BOTH a modern and a
        // legacy AppX identity. The installed map only carries one of them.
        var def = new ItemDefinition
        {
            Id = "windows-app-xbox",
            Name = "Xbox",
            Description = "Game library",
            AppxPackageName = new[] { "Microsoft.GamingApp", "Microsoft.XboxApp" },
            IsInstalled = true,
        };
        var legacyFullName = "Microsoft.XboxApp_48.86.5.0_x64__8wekyb3d8bbwe";

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Microsoft.XboxApp"] = legacyFullName });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync(legacyFullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PngBytes("xbox-legacy"));

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().NotBeNull();
        def.IconPath!.Should().Contain("windows-app-xbox.");
        _mockIconSource.Verify(
            s => s.GetLogoStreamAsync(legacyFullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ===== Layer 3: package-icons repo =====

    [Fact]
    public async Task ResolveBatchAsync_ExternalApp_FetchesFromRepo_StampsIconPath()
    {
        // external-app-* with a winget id → repo path icons/external/<winget-id>.png.
        // AppX/binary are never consulted (no AppX name, no binary hint).
        var def = Def("external-app-7zip", winGetId: "7zip.7zip");
        var repoPath = "icons/external/7zip.7zip"; // RepoIconKey lowercases the winget id
        var expectedRepoPath = $"{repoPath}.png";

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());
        _mockManifest.Setup(m => m.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockManifest.Setup(m => m.Sha256For(expectedRepoPath)).Returns("deadbeef");
        _mockRepoSource.Setup(r => r.GetIconBytesAsync(expectedRepoPath, "deadbeef", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("PNG-repo-7zip"));

        await _resolver.ResolveBatchAsync(new[] { def }, applyThemeAdaptation: false);

        var expectedCache = Path.Combine(_tempCacheDir, BuildCacheFileName(def.Id, "repo:deadbeef"));
        def.IconPath.Should().Be(expectedCache);
        File.Exists(expectedCache).Should().BeTrue();
        File.ReadAllText(expectedCache).Should().Be("PNG-repo-7zip");

        _mockIconSource.Verify(
            s => s.GetLogoStreamAsync(It.IsAny<string>(), It.IsAny<Size>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockBinarySource.Verify(
            b => b.GetIconStreamAsync(It.IsAny<string>(), It.IsAny<Size>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockRepoSource.Verify(
            r => r.GetIconBytesAsync(expectedRepoPath, "deadbeef", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveBatchAsync_WindowsApp_NotInstalled_RepoReturnsBytes_StampsIconPath()
    {
        // windows-app-* whose package is NOT in the installed map falls through
        // to the repo, using the AppX-identity candidate path.
        var def = Def("windows-app-calc", appxName: "Microsoft.WindowsCalculator", installed: false);
        var expectedRepoPath = "icons/windows/microsoft.windowscalculator.png";

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>()); // not installed
        _mockManifest.Setup(m => m.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockManifest.Setup(m => m.Sha256For(expectedRepoPath)).Returns((string?)null);
        _mockRepoSource.Setup(r => r.GetIconBytesAsync(expectedRepoPath, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("PNG-repo-calc"));

        await _resolver.ResolveBatchAsync(new[] { def }, applyThemeAdaptation: true);

        // No sha known → cache key is "repo:" + path.
        var expectedCache = Path.Combine(_tempCacheDir, BuildCacheFileName(def.Id, "repo:" + expectedRepoPath));
        def.IconPath.Should().Be(expectedCache);
        File.ReadAllText(expectedCache).Should().Be("PNG-repo-calc");

        _mockIconSource.Verify(
            s => s.GetLogoStreamAsync(It.IsAny<string>(), It.IsAny<Size>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockRepoSource.Verify(
            r => r.GetIconBytesAsync(expectedRepoPath, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveBatchAsync_WindowsApp_Installed_DoesNotCallRepo()
    {
        // windows-app-* present in the installed map resolves via AppX; the repo
        // must NOT be touched.
        var def = Def("windows-app-calc", appxName: "Microsoft.WindowsCalculator");
        var fullName = "Microsoft.WindowsCalculator_1.0.0_x64__abc";

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Microsoft.WindowsCalculator"] = fullName });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync(fullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PngBytes("calc-appx"));

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().Be(Path.Combine(_tempCacheDir, BuildCacheFileName(def.Id, fullName)));
        _mockRepoSource.Verify(
            r => r.GetIconBytesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveBatchAsync_WindowsApp_NotInstalled_RepoReturnsNull_LeavesIconPathNull()
    {
        var def = Def("windows-app-calc", appxName: "Microsoft.WindowsCalculator", installed: false);
        var expectedRepoPath = "icons/windows/microsoft.windowscalculator.png";

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());
        _mockManifest.Setup(m => m.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockManifest.Setup(m => m.Sha256For(It.IsAny<string>())).Returns((string?)null);
        _mockRepoSource.Setup(r => r.GetIconBytesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().BeNull();
        _mockRepoSource.Verify(
            r => r.GetIconBytesAsync(expectedRepoPath, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveBatchAsync_RepoCacheHit_DoesNotCallRepoSource()
    {
        var def = Def("external-app-7zip", winGetId: "7zip.7zip");
        var expectedRepoPath = "icons/external/7zip.7zip.png";

        Directory.CreateDirectory(_tempCacheDir);
        var cachePath = Path.Combine(_tempCacheDir, BuildCacheFileName(def.Id, "repo:cafef00d"));
        File.WriteAllBytes(cachePath, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());
        _mockManifest.Setup(m => m.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockManifest.Setup(m => m.Sha256For(expectedRepoPath)).Returns("cafef00d");

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().Be(cachePath);
        _mockRepoSource.Verify(
            r => r.GetIconBytesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ===== Layer 2: explicit local-path IconSources =====

    [Fact]
    public async Task ResolveBatchAsync_IconSourcesLocalPath_ReadsFromDisk()
    {
        Directory.CreateDirectory(_tempCacheDir);
        var localIconPath = Path.Combine(_tempCacheDir, "fake-onedrive.ico");
        File.WriteAllText(localIconPath, "ICO-bytes");

        var def = Def("external-app-onedrive") with
        {
            IconSources = new[] { localIconPath },
        };

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().NotBeNull();
        Path.GetFileName(def.IconPath!).Should().StartWith($"{def.Id}.");
        File.ReadAllText(def.IconPath!).Should().Be("ICO-bytes");

        // A local IconSources hit means the repo is never consulted for this def.
        _mockRepoSource.Verify(
            r => r.GetIconBytesAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ResolveBatchAsync_IconSourcesExePath_RoutesThroughBinarySource()
    {
        // A .exe file on disk shouldn't be read as raw bytes — it should be handed
        // to IBinaryIconSource, and the extracted PNG cached under <def.Id>.<hash>.png.
        Directory.CreateDirectory(_tempCacheDir);
        var fakeExe = Path.Combine(_tempCacheDir, "fake-explorer.exe");
        File.WriteAllText(fakeExe, "this is not a real exe, but File.Exists returns true");

        var def = Def("external-app-msiexec-runtime") with { IconSources = new[] { fakeExe } };

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());
        _mockBinarySource.Setup(b => b.GetIconStreamAsync(fakeExe, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PngBytes("from-exe"));

        await _resolver.ResolveBatchAsync(new[] { def });

        def.IconPath.Should().NotBeNull();
        Path.GetFileName(def.IconPath!).Should().StartWith($"{def.Id}.");
        File.ReadAllText(def.IconPath!).Should().Be("PNG-from-exe");
        _mockBinarySource.Verify(
            b => b.GetIconStreamAsync(fakeExe, It.IsAny<Size>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveBatchAsync_IconSourcesUrlEntry_IsIgnored_FallsThroughToRepo()
    {
        // URL IconSources entries are no longer fetched (live fetch machinery
        // removed). A def whose only IconSources entry is a URL falls through to
        // the repo layer based on its external-app-* id.
        var def = Def("external-app-7zip", winGetId: "7zip.7zip") with
        {
            IconSources = new[] { "https://example.invalid/icon.png" },
        };
        var expectedRepoPath = "icons/external/7zip.7zip.png";

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>());
        _mockManifest.Setup(m => m.LoadAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockManifest.Setup(m => m.Sha256For(expectedRepoPath)).Returns((string?)null);
        _mockRepoSource.Setup(r => r.GetIconBytesAsync(expectedRepoPath, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("PNG-repo-fallback"));

        await _resolver.ResolveBatchAsync(new[] { def }, applyThemeAdaptation: false);

        def.IconPath.Should().NotBeNull();
        File.ReadAllText(def.IconPath!).Should().Be("PNG-repo-fallback");
        _mockRepoSource.Verify(
            r => r.GetIconBytesAsync(expectedRepoPath, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ===== Light/dark variant generation (theme adaptation) =====

    [Fact]
    public async Task ResolveBatchAsync_WhiteAppxIcon_WritesLightVariantSibling()
    {
        var def = Def("windows-app-white", appxName: "Vendor.WhiteApp");
        var fullName = "Vendor.WhiteApp_1.0.0_x64__abc";
        var whitePngBytes = await PngTestHelper.MakeSolidPngAsync(16, 16, 0xFF, 0xFF, 0xFF);

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Vendor.WhiteApp"] = fullName });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync(fullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(whitePngBytes));

        await _resolver.ResolveBatchAsync(new[] { def }, applyThemeAdaptation: true);

        def.IconPath.Should().NotBeNull();
        var lightPath = Path.ChangeExtension(def.IconPath!, null) + ".light.png";
        File.Exists(lightPath).Should().BeTrue("a monochrome-white primary must produce a .light.png sibling");

        (await SampleFirstPixelAsync(lightPath)).Should().Be(((byte)0x1F, (byte)0x1F, (byte)0x1F, (byte)0xFF));
    }

    [Fact]
    public async Task ResolveBatchAsync_DarkGreyAppxIcon_WritesBothLightAndDarkVariants()
    {
        var def = Def("windows-app-gamebar", appxName: "Vendor.XboxGameBarLike");
        var fullName = "Vendor.XboxGameBarLike_1.0.0_x64__abc";
        var greyPngBytes = await PngTestHelper.MakeSolidPngAsync(16, 16, 0x33, 0x33, 0x33);

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Vendor.XboxGameBarLike"] = fullName });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync(fullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(greyPngBytes));

        await _resolver.ResolveBatchAsync(new[] { def }, applyThemeAdaptation: true);

        def.IconPath.Should().NotBeNull();
        var stem = Path.ChangeExtension(def.IconPath!, null);
        File.Exists(stem + ".light.png").Should().BeTrue("mono-dark icons need a light-mode variant");
        File.Exists(stem + ".dark.png").Should().BeTrue("mono-dark icons need a dark-mode variant");

        (await SampleFirstPixelAsync(stem + ".light.png")).Should().Be(((byte)0x1F, (byte)0x1F, (byte)0x1F, (byte)0xFF));
        (await SampleFirstPixelAsync(stem + ".dark.png")).Should().Be(((byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF));
    }

    [Fact]
    public async Task ResolveBatchAsync_IconWithUniformBackplate_TrimsBackplateBeforeCaching()
    {
        var def = Def("windows-app-stickynotes", appxName: "Vendor.StickyNotesLike");
        var fullName = "Vendor.StickyNotesLike_1.0.0_x64__abc";
        var input = await PngTestHelper.MakePngAsync(20, 20, (x, y) =>
        {
            bool isYellowShape = x >= 8 && x < 16 && y >= 8 && y < 12;
            return isYellowShape
                ? ((byte)0x00, (byte)0xE0, (byte)0xE0, (byte)0xFF)   // BGRA: vivid yellow
                : ((byte)0x40, (byte)0x40, (byte)0x40, (byte)0xFF);  // dark-grey backplate
        });

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Vendor.StickyNotesLike"] = fullName });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync(fullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(input));

        await _resolver.ResolveBatchAsync(new[] { def }, applyThemeAdaptation: true);

        def.IconPath.Should().NotBeNull();
        var decoder = await DecodeAsync(def.IconPath!);
        decoder.PixelWidth.Should().Be(8, "backplate trim should crop to the inner yellow rectangle's width");
        decoder.PixelHeight.Should().Be(4, "backplate trim should crop to the inner yellow rectangle's height");
    }

    [Fact]
    public async Task ResolveBatchAsync_TransparentBorderedIcon_StillTrimsByAlphaUnchanged()
    {
        var def = Def("windows-app-transparent", appxName: "Vendor.TransparentBorder");
        var fullName = "Vendor.TransparentBorder_1.0.0_x64__abc";
        var input = await PngTestHelper.MakePngAsync(20, 20, (x, y) =>
        {
            bool isOpaqueWhite = x >= 6 && x < 14 && y >= 6 && y < 14;
            return isOpaqueWhite
                ? ((byte)0xFF, (byte)0xFF, (byte)0xFF, (byte)0xFF)
                : ((byte)0x00, (byte)0x00, (byte)0x00, (byte)0x00);  // fully transparent
        });

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Vendor.TransparentBorder"] = fullName });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync(fullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(input));

        await _resolver.ResolveBatchAsync(new[] { def }, applyThemeAdaptation: true);

        def.IconPath.Should().NotBeNull();
        var decoder = await DecodeAsync(def.IconPath!);
        decoder.PixelWidth.Should().Be(8);
        decoder.PixelHeight.Should().Be(8);
    }

    [Fact]
    public async Task ResolveBatchAsync_ThemeAdaptationDisabled_DoesNotWriteVariants()
    {
        // External Apps path: applyThemeAdaptation=false. A monochrome-white icon
        // that WOULD normally produce a .light.png must not.
        var def = Def("windows-app-whitelogo", appxName: "Vendor.WhiteLogo");
        var fullName = "Vendor.WhiteLogo_1.0.0_x64__abc";
        var whitePngBytes = await PngTestHelper.MakeSolidPngAsync(16, 16, 0xFF, 0xFF, 0xFF);

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Vendor.WhiteLogo"] = fullName });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync(fullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(whitePngBytes));

        await _resolver.ResolveBatchAsync(new[] { def }, applyThemeAdaptation: false);

        def.IconPath.Should().NotBeNull();
        var stem = Path.ChangeExtension(def.IconPath!, null);
        File.Exists(stem + ".light.png").Should().BeFalse("theme adaptation off → no .light.png");
        File.Exists(stem + ".dark.png").Should().BeFalse("theme adaptation off → no .dark.png");
    }

    [Fact]
    public async Task ResolveBatchAsync_ThemeAdaptationDisabled_DoesNotCropBackplate()
    {
        var def = Def("windows-app-framed", appxName: "Vendor.FramedLogo");
        var fullName = "Vendor.FramedLogo_1.0.0_x64__abc";
        var input = await PngTestHelper.MakePngAsync(20, 20, (x, y) =>
        {
            bool isYellowShape = x >= 8 && x < 16 && y >= 8 && y < 12;
            return isYellowShape
                ? ((byte)0x00, (byte)0xE0, (byte)0xE0, (byte)0xFF)
                : ((byte)0x40, (byte)0x40, (byte)0x40, (byte)0xFF);
        });

        _mockIconSource.Setup(s => s.GetInstalledPackageMapAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> { ["Vendor.FramedLogo"] = fullName });
        _mockIconSource.Setup(s => s.GetLogoStreamAsync(fullName, It.IsAny<Size>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(input));

        await _resolver.ResolveBatchAsync(new[] { def }, applyThemeAdaptation: false);

        def.IconPath.Should().NotBeNull();
        var decoder = await DecodeAsync(def.IconPath!);
        decoder.PixelWidth.Should().Be(20, "theme adaptation off → backplate is not cropped");
        decoder.PixelHeight.Should().Be(20, "theme adaptation off → backplate is not cropped");
    }

    // ===== Helpers =====

    private static async Task<BitmapDecoder> DecodeAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(bytes.AsBuffer());
        stream.Seek(0);
        return await BitmapDecoder.CreateAsync(stream);
    }

    private static async Task<(byte R, byte G, byte B, byte A)> SampleFirstPixelAsync(string path)
    {
        var bytes = await File.ReadAllBytesAsync(path);
        using var stream = new InMemoryRandomAccessStream();
        await stream.WriteAsync(bytes.AsBuffer());
        stream.Seek(0);
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var sw = await decoder.GetSoftwareBitmapAsync(
            BitmapPixelFormat.Bgra8, BitmapAlphaMode.Straight);
        var buffer = new Windows.Storage.Streams.Buffer((uint)(sw.PixelWidth * sw.PixelHeight * 4));
        sw.CopyToBuffer(buffer);
        var pixels = buffer.ToArray();
        return (pixels[2], pixels[1], pixels[0], pixels[3]);
    }
}
