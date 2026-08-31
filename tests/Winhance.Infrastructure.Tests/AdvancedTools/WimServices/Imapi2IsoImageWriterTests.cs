using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools.WimServices;

public class Imapi2IsoImageWriterTests
{
    private const string BiosBootImage = @"C:\work\boot\etfsboot.com";
    private const string UefiBootImage = @"C:\work\efi\microsoft\boot\efisys.bin";

    private readonly Mock<IFileSystemService> _fileSystem = new();
    private readonly Mock<ILocalizationService> _localization = new();

    public Imapi2IsoImageWriterTests()
    {
        _fileSystem.Setup(fs => fs.CombinePath(It.IsAny<string[]>()))
                   .Returns((string[] parts) => string.Join("\\", parts));
        _fileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _localization.Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
                     .Returns((string key, object[] args) => key);
    }

    [Fact]
    public void Write_Always_RequestsUdfOnlyAtRevision102()
    {
        var image = BuildImage();

        BuildWriter(image.Object).Write(@"C:\work", @"C:\out.iso", null, CancellationToken.None);

        image.VerifySet(i => i.FileSystemsToCreate = IsoFileSystems.Udf);
        image.VerifySet(i => i.UdfRevision = 0x102);
    }

    [Fact]
    public void Write_Always_ClearsTheMediaBlockLimitBeforeAddingTheTree()
    {
        var order = new List<string>();
        var image = BuildImage();
        image.SetupSet(i => i.FreeMediaBlocks = 0).Callback(() => order.Add("FreeMediaBlocks"));
        image.Setup(i => i.AddTree(It.IsAny<string>(), It.IsAny<bool>())).Callback(() => order.Add("AddTree"));

        BuildWriter(image.Object).Write(@"C:\work", @"C:\out.iso", null, CancellationToken.None);

        order.Should().ContainInOrder("FreeMediaBlocks", "AddTree");
    }

    [Fact]
    public void Write_Always_AddsTheTreeWithoutTheBaseDirectory()
    {
        var image = BuildImage();

        BuildWriter(image.Object).Write(@"C:\work", @"C:\out.iso", null, CancellationToken.None);

        image.Verify(i => i.AddTree(@"C:\work", false), Times.Once);
    }

    [Fact]
    public void Write_Always_AssignsTwoBootEntriesWithDistinctPlatformIds()
    {
        IReadOnlyList<BootEntry> assigned = Array.Empty<BootEntry>();
        var image = BuildImage();
        image.Setup(i => i.SetBootImageOptions(It.IsAny<IReadOnlyList<BootEntry>>()))
             .Callback<IReadOnlyList<BootEntry>>(entries => assigned = entries);

        BuildWriter(image.Object).Write(@"C:\work", @"C:\out.iso", null, CancellationToken.None);

        assigned.Should().HaveCount(2);
        assigned.Select(e => e.Platform).Should().Equal(BootPlatform.BiosX86, BootPlatform.Uefi);
        assigned.Select(e => e.BootImagePath).Should().Equal(BiosBootImage, UefiBootImage);
    }

    [Fact]
    public void Write_BootArrayReadsBackShort_Throws()
    {
        var image = new Mock<IFileSystemImageWrapper>();
        image.SetupGet(i => i.BootImageEntryCount).Returns(1);

        Action act = () => BuildWriter(image.Object)
            .Write(@"C:\work", @"C:\out.iso", null, CancellationToken.None);

        act.Should().Throw<InvalidOperationException>().WithMessage("*2*");
    }

    [Fact]
    public void Write_Always_NeverStagesFiles()
    {
        var image = BuildImage();

        BuildWriter(image.Object).Write(@"C:\work", @"C:\out.iso", null, CancellationToken.None);

        image.VerifySet(i => i.StageFiles = false);
    }

    [Fact]
    public void Write_BiosBootFileMissing_ThrowsFileNotFound()
    {
        var fileSystem = new Mock<IFileSystemService>();
        fileSystem.Setup(fs => fs.CombinePath(It.IsAny<string[]>()))
                  .Returns((string[] parts) => string.Join("\\", parts));
        fileSystem.Setup(fs => fs.FileExists(It.Is<string>(p => p.EndsWith("etfsboot.com")))).Returns(false);
        fileSystem.Setup(fs => fs.FileExists(It.Is<string>(p => p.EndsWith("efisys.bin")))).Returns(true);

        Action act = () => BuildWriter(Mock.Of<IFileSystemImageWrapper>(), fileSystem.Object)
            .Write(@"C:\work", @"C:\out.iso", null, CancellationToken.None);

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Write_UefiBootFileMissing_ThrowsFileNotFound()
    {
        var fileSystem = new Mock<IFileSystemService>();
        fileSystem.Setup(fs => fs.CombinePath(It.IsAny<string[]>()))
                  .Returns((string[] parts) => string.Join("\\", parts));
        fileSystem.Setup(fs => fs.FileExists(It.Is<string>(p => p.EndsWith("etfsboot.com")))).Returns(true);
        fileSystem.Setup(fs => fs.FileExists(It.Is<string>(p => p.EndsWith("efisys.bin")))).Returns(false);

        Action act = () => BuildWriter(Mock.Of<IFileSystemImageWrapper>(), fileSystem.Object)
            .Write(@"C:\work", @"C:\out.iso", null, CancellationToken.None);

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Write_Always_WritesTheResultImageToTheRequestedPath()
    {
        var result = new Mock<IIsoResultImage>();
        var image = BuildImage();
        image.Setup(i => i.CreateResultImage()).Returns(result.Object);

        BuildWriter(image.Object).Write(@"C:\work", @"C:\out.iso", null, CancellationToken.None);

        result.Verify(r => r.WriteTo(@"C:\out.iso", It.IsAny<Action<long, long>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IFileSystemImageWrapper> BuildImage()
    {
        var image = new Mock<IFileSystemImageWrapper>();
        image.SetupGet(i => i.BootImageEntryCount).Returns(2);
        image.Setup(i => i.CreateResultImage()).Returns(Mock.Of<IIsoResultImage>());
        return image;
    }

    private Imapi2IsoImageWriter BuildWriter(IFileSystemImageWrapper image, IFileSystemService? fileSystem = null)
    {
        return new Imapi2IsoImageWriter(
            () => image,
            fileSystem ?? _fileSystem.Object,
            _localization.Object,
            Mock.Of<ILogService>());
    }
}
