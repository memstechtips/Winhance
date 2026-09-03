using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools.WimServices;

public class IsoServiceTests
{
    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();
    private readonly Mock<IDismProcessRunner> _mockDismRunner = new();
    private readonly Mock<IIsoImageReader> _mockIsoImageReader = new();
    private readonly Mock<IIsoImageWriter> _mockIsoImageWriter = new();
    private readonly Mock<IMediaCopier> _mockMediaCopier = new();
    private readonly IsoService _service;

    public IsoServiceTests()
    {
        _mockFileSystem
            .Setup(fs => fs.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));

        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => key);
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _service = new IsoService(
            _mockFileSystem.Object,
            _mockLogService.Object,
            _mockLocalization.Object,
            _mockProcessExecutor.Object,
            _mockDismRunner.Object,
            _mockIsoImageReader.Object,
            _mockIsoImageWriter.Object,
            _mockMediaCopier.Object);
    }

    [Fact]
    public async Task ValidateIsoFileAsync_FileDoesNotExist_ReturnsFalse()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        var result = await _service.ValidateIsoFileAsync(@"C:\test.iso");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateIsoFileAsync_WrongExtension_ReturnsFalse()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetExtension(It.IsAny<string>())).Returns(".txt");

        var result = await _service.ValidateIsoFileAsync(@"C:\test.txt");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateIsoFileAsync_FileTooSmall_ReturnsFalse()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetExtension(It.IsAny<string>())).Returns(".iso");
        _mockFileSystem.Setup(fs => fs.GetFileSize(It.IsAny<string>())).Returns(512); // < 1MB

        var result = await _service.ValidateIsoFileAsync(@"C:\test.iso");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateIsoFileAsync_ValidIsoFile_ReturnsTrue()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetExtension(It.IsAny<string>())).Returns(".iso");
        _mockFileSystem.Setup(fs => fs.GetFileSize(It.IsAny<string>())).Returns(5_000_000_000L);

        var result = await _service.ValidateIsoFileAsync(@"C:\test.iso");

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task CreateIsoAsync_EmptyOrNullWorkingDirectory_ReturnsFalse(string? workingDirectory)
    {
        var result = await _service.CreateIsoAsync(workingDirectory!, @"C:\output.iso");

        result.Should().BeFalse();
        _mockIsoImageWriter.Verify(
            w => w.Write(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateIsoAsync_WriterThrows_SurfacesTheReason()
    {
        GivenEnoughRoomToWrite();
        _mockIsoImageWriter
            .Setup(w => w.Write(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .Throws(new FileNotFoundException(@"Boot file not found: C:\work\boot\etfsboot.com"));

        Func<Task> act = () => _service.CreateIsoAsync(@"C:\work", @"C:\output\output.iso");

        await act.Should().ThrowAsync<FileNotFoundException>().WithMessage("*etfsboot.com*");
    }

    [Fact]
    public async Task CreateIsoAsync_WriterProducedNoFile_Throws()
    {
        GivenEnoughRoomToWrite();

        Func<Task> act = () => _service.CreateIsoAsync(@"C:\work", @"C:\output\output.iso");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*output.iso*");
        _mockIsoImageWriter.Verify(
            w => w.Write(@"C:\work", @"C:\output\output.iso", It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExtractIsoAsync_Always_CopiesFromTheAttachedVolumeAndDetachesIt()
    {
        var attachment = GivenAnExtractableIso();

        var result = await _service.ExtractIsoAsync(@"C:\src.iso", @"C:\work");

        result.Should().BeTrue();
        _mockMediaCopier.Verify(c => c.CopyTree(@"\\?\Volume{1111}\", @"C:\work", null, null,
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Once);
        attachment.Verify(a => a.Dispose(), Times.Once);
    }

    [Fact]
    public async Task ExtractIsoAsync_CopyThrows_DetachesAndReturnsFalse()
    {
        var attachment = GivenAnExtractableIso();
        _mockMediaCopier
            .Setup(c => c.CopyTree(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Func<string, bool>>(), It.IsAny<long?>(),
                It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .Throws(new IOException("The device is not ready."));

        var result = await _service.ExtractIsoAsync(@"C:\src.iso", @"C:\work");

        result.Should().BeFalse();
        attachment.Verify(a => a.Dispose(), Times.Once);
    }

    [Fact]
    public async Task ExtractIsoAsync_MediaHasNoSourcesFolder_ReturnsFalse()
    {
        GivenAnExtractableIso();
        _mockFileSystem
            .Setup(fs => fs.GetDirectories(@"C:\work", "*", System.IO.SearchOption.TopDirectoryOnly))
            .Returns([@"C:\work\boot", @"C:\work\efi"]);

        var result = await _service.ExtractIsoAsync(@"C:\src.iso", @"C:\work");

        result.Should().BeFalse();
    }

    private Mock<IIsoAttachment> GivenAnExtractableIso()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(@"C:\src.iso")).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetExtension(@"C:\src.iso")).Returns(".iso");
        _mockFileSystem.Setup(fs => fs.GetFileSize(@"C:\src.iso")).Returns(5_000_000_000L);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(@"C:\work")).Returns(false);
        _mockFileSystem
            .Setup(fs => fs.GetDirectories(@"C:\work", "*", System.IO.SearchOption.TopDirectoryOnly))
            .Returns([@"C:\work\sources", @"C:\work\boot"]);
        _mockFileSystem.Setup(fs => fs.GetFileName(It.IsAny<string>())).Returns((string p) => Path.GetFileName(p));
        _mockDismRunner.Setup(d => d.CheckDiskSpaceAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var attachment = new Mock<IIsoAttachment>();
        attachment.SetupGet(a => a.RootPath).Returns(@"\\?\Volume{1111}\");
        _mockIsoImageReader.Setup(r => r.Attach(@"C:\src.iso")).Returns(attachment.Object);
        return attachment;
    }

    private void GivenEnoughRoomToWrite()
    {
        _mockFileSystem.Setup(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.IO.SearchOption>()))
            .Returns(Array.Empty<string>());
        _mockDismRunner.Setup(d => d.CheckDiskSpaceAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystem.Setup(fs => fs.GetDirectoryName(It.IsAny<string>())).Returns(@"C:\output");
    }
}
