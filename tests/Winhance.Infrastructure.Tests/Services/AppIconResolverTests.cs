using System.IO;
using System.Text;
using FluentAssertions;
using Moq;
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
            _mockStoreSource.Object);
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
        string? msStoreId = null) => new()
    {
        Id = id,
        Name = $"App {id}",
        Description = $"Description for {id}",
        AppxPackageName = appxName != null ? new[] { appxName } : null,
        IsInstalled = installed,
        MsStoreId = msStoreId,
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
}
