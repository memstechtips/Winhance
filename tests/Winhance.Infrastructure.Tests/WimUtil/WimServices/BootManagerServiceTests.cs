using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.WimUtil.Models;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

public class BootManagerServiceTests
{
    private const string Work = @"C:\work";
    private const string BootWim = @"C:\work\sources\boot.wim";
    private const string Ca2023BootManager = @"\Windows\Boot\EFI_EX\bootmgfw_EX.efi";

    private readonly Mock<IWimFileExtractor> _extractor = new();
    private readonly Mock<ISecureBootDatabase> _secureBoot = new();
    private readonly Mock<IFileSystemService> _fileSystem = new();
    private readonly HashSet<string> _inBootWim = new(StringComparer.OrdinalIgnoreCase);

    public BootManagerServiceTests()
    {
        _fileSystem.Setup(fs => fs.CombinePath(It.IsAny<string[]>()))
                   .Returns((string[] parts) => string.Join("\\", parts));
        _fileSystem.Setup(fs => fs.GetTempPath()).Returns(@"C:\temp");
        _fileSystem.Setup(fs => fs.GetFileNameWithoutExtension(It.IsAny<string>()))
                   .Returns((string path) => Path.GetFileNameWithoutExtension(path));
        _fileSystem.Setup(fs => fs.FileExists(BootWim)).Returns(true);
        _extractor.Setup(e => e.Extract(BootWim, 1, It.IsAny<IReadOnlyDictionary<string, string>>()))
                  .Returns((string _, int _, IReadOnlyDictionary<string, string> wanted) =>
                      wanted.Keys.Where(_inBootWim.Contains).ToList());
    }

    [Fact]
    public async Task InspectAsync_NoBootManagerOnTheMedia_OffersNothing()
    {
        var options = await CreateSut().InspectAsync(Work, CancellationToken.None);

        options.Should().Be(new BootManagerOptions(false, BootManager.Pca2011, false));
    }

    [Fact]
    public async Task InspectAsync_2011MediaWhoseBootWimCarries2023_Offers2023And2011StaysTheDefault()
    {
        GivenMediaBootManager("Microsoft Windows Production PCA 2011", 0x70000);
        _inBootWim.Add(Ca2023BootManager);
        _secureBoot.Setup(s => s.RevokesPca2011()).Returns(false);

        var options = await CreateSut().InspectAsync(Work, CancellationToken.None);

        options.Should().Be(new BootManagerOptions(true, BootManager.Pca2011, false));
    }

    [Fact]
    public async Task InspectAsync_ThisPcRevokes2011_Defaults2023()
    {
        GivenMediaBootManager("Microsoft Windows Production PCA 2011", 0x70000);
        _inBootWim.Add(Ca2023BootManager);
        _secureBoot.Setup(s => s.RevokesPca2011()).Returns(true);

        var options = await CreateSut().InspectAsync(Work, CancellationToken.None);

        options.Default.Should().Be(BootManager.Ca2023);
    }

    [Fact]
    public async Task InspectAsync_SecureBootDatabaseUnreadable_Keeps2011()
    {
        GivenMediaBootManager("Microsoft Windows Production PCA 2011", 0x70000);
        _inBootWim.Add(Ca2023BootManager);
        _secureBoot.Setup(s => s.RevokesPca2011()).Throws(new InvalidOperationException("privilege not held"));

        var options = await CreateSut().InspectAsync(Work, CancellationToken.None);

        options.Should().Be(new BootManagerOptions(true, BootManager.Pca2011, false));
    }

    [Fact]
    public async Task InspectAsync_BootWimHasNo2023BootManager_OffersNothing()
    {
        GivenMediaBootManager("Microsoft Windows Production PCA 2011", 0x70000);
        _secureBoot.Setup(s => s.RevokesPca2011()).Returns(true);

        var options = await CreateSut().InspectAsync(Work, CancellationToken.None);

        options.Offers2023.Should().BeFalse();
        options.Default.Should().Be(BootManager.Pca2011);
    }

    [Fact]
    public async Task InspectAsync_MediaAlreadyStartsFrom2023_OffersNothingAndNeverOpensBootWim()
    {
        GivenMediaBootManager("Windows UEFI CA 2023", 0x70000);

        var options = await CreateSut().InspectAsync(Work, CancellationToken.None);

        options.Offers2023.Should().BeFalse();
        _extractor.Verify(e => e.Extract(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Never);
    }

    [Fact]
    public async Task InspectAsync_ProbingBootWim_LeavesTheStagedReplacementsAlone()
    {
        GivenMediaBootManager("Microsoft Windows Production PCA 2011", 0x70000);
        _inBootWim.Add(Ca2023BootManager);
        _fileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        await CreateSut().InspectAsync(Work, CancellationToken.None);

        _fileSystem.Verify(fs => fs.DeleteDirectory(@"C:\temp\Winhance\BootManager2023", It.IsAny<bool>()), Times.Never);
        _extractor.Verify(e => e.Extract(
            BootWim,
            1,
            It.Is<IReadOnlyDictionary<string, string>>(wanted => wanted[Ca2023BootManager] == @"C:\temp\Winhance\BootManagerProbe\bootmgfw_EX.efi")));
    }

    [Theory]
    [InlineData(0x50000u, true)]
    [InlineData(0x70000u, false)]
    [InlineData(null, false)]
    public async Task InspectAsync_SecurityVersionBelowTheNewest_IsOutdated(uint? securityVersion, bool outdated)
    {
        GivenMediaBootManager("Microsoft Windows Production PCA 2011", securityVersion);

        var options = await CreateSut().InspectAsync(Work, CancellationToken.None);

        options.IsOutdated.Should().Be(outdated);
    }

    [Fact]
    public async Task PrepareAsync_2011_ReplacesNothing()
    {
        var replacements = await CreateSut().PrepareAsync(Work, BootManager.Pca2011, CancellationToken.None);

        replacements.Files.Should().BeEmpty();
        _extractor.Verify(e => e.Extract(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Never);
    }

    [Fact]
    public async Task PrepareAsync_2023_MapsEachFileBootWimHasToItsPathOnTheMedia()
    {
        _fileSystem.Setup(fs => fs.FileExists(@"C:\work\efi\boot\bootx64.efi")).Returns(true);
        _fileSystem.Setup(fs => fs.FileExists(@"C:\work\efi\microsoft\boot\efisys.bin")).Returns(true);
        _fileSystem.Setup(fs => fs.DirectoryExists(@"C:\work\efi\microsoft\boot\fonts")).Returns(true);
        _fileSystem.Setup(fs => fs.GetFiles(@"C:\work\efi\microsoft\boot\fonts", "*.ttf", SearchOption.TopDirectoryOnly))
                   .Returns([@"C:\work\efi\microsoft\boot\fonts\wgl4_boot.ttf", @"C:\work\efi\microsoft\boot\fonts\malgun_console.ttf"]);
        _inBootWim.UnionWith(
        [
            Ca2023BootManager,
            @"\Windows\Boot\DVD_EX\EFI\en-US\efisys_EX.bin",
            @"\Windows\Boot\Fonts_EX\wgl4_boot_EX.ttf",
        ]);

        var replacements = await CreateSut().PrepareAsync(Work, BootManager.Ca2023, CancellationToken.None);

        replacements.Files.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            [@"efi\boot\bootx64.efi"] = @"C:\temp\Winhance\BootManager2023\efi\boot\bootx64.efi",
            [@"efi\microsoft\boot\efisys.bin"] = @"C:\temp\Winhance\BootManager2023\efi\microsoft\boot\efisys.bin",
            [@"efi\microsoft\boot\fonts\wgl4_boot.ttf"] = @"C:\temp\Winhance\BootManager2023\efi\microsoft\boot\fonts\wgl4_boot.ttf",
        });
    }

    [Fact]
    public async Task PrepareAsync_BootWimHasNo2023BootManager_Throws()
    {
        _fileSystem.Setup(fs => fs.FileExists(@"C:\work\efi\boot\bootx64.efi")).Returns(true);

        Func<Task> act = () => CreateSut().PrepareAsync(Work, BootManager.Ca2023, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private void GivenMediaBootManager(string signer, uint? securityVersion)
    {
        _fileSystem.Setup(fs => fs.FileExists(@"C:\work\efi\boot\bootx64.efi")).Returns(true);
        _fileSystem.Setup(fs => fs.ReadAllBytesAsync(@"C:\work\efi\boot\bootx64.efi", It.IsAny<CancellationToken>()))
                   .ReturnsAsync(FakeBootManager.Build(signer, securityVersion));
    }

    private BootManagerService CreateSut() =>
        new(_extractor.Object, _secureBoot.Object, _fileSystem.Object, Mock.Of<ILogService>());
}
