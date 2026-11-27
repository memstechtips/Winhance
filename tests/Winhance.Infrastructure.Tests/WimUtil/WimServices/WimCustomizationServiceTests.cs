using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Winhance.Infrastructure.Features.Autounattend;
using Winhance.Infrastructure.Features.Autounattend.Helpers;
using Winhance.Infrastructure.Features.Common.Services;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.WimUtil.Interfaces;
using Winhance.Infrastructure.Features.WimUtil.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.WimUtil.WimServices;

public class WimCustomizationServiceTests
{
    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IDriverCategorizer> _mockDriverCategorizer = new();
    private readonly Mock<IDismProcessRunner> _mockDismRunner = new();
    private readonly Mock<IDriverInstallStepWriter> _mockDriverInstallStep = new();
    private readonly Mock<IUpdateInstallStepWriter> _mockUpdateInstallStep = new();
    private readonly WimCustomizationService _service;

    public WimCustomizationServiceTests()
    {
        _mockFileSystem
            .Setup(fs => fs.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));

        _mockFileSystem.Setup(fs => fs.OpenRead(It.IsAny<string>()))
            .Returns(() => new MemoryStream(Encoding.UTF8.GetBytes("<unattend/>")));
        _mockFileSystem.Setup(fs => fs.ReadAllBytesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("<unattend/>"));

        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => key);
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _service = new WimCustomizationService(
            _mockFileSystem.Object,
            _mockLogService.Object,
            _mockLocalization.Object,
            _mockDriverCategorizer.Object,
            _mockDismRunner.Object,
            _mockDriverInstallStep.Object,
            _mockUpdateInstallStep.Object);
    }

    [Fact]
    public async Task AddXmlToImageAsync_XmlFileNotFound_Fails()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        var result = await _service.AddXmlToImageAsync(@"C:\missing.xml", @"C:\work");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain(@"C:\missing.xml");
    }

    [Fact]
    public async Task AddXmlToImageAsync_WorkingDirectoryNotFound_Fails()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);

        var result = await _service.AddXmlToImageAsync(@"C:\answer.xml", @"C:\missing_dir");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain(@"C:\missing_dir");
    }

    [Fact]
    public async Task AddXmlToImageAsync_ValidInputs_CopiesFileAndSucceeds()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        var result = await _service.AddXmlToImageAsync(@"C:\answer.xml", @"C:\work");

        result.Success.Should().BeTrue();
        _mockFileSystem.Verify(fs => fs.CopyFile(@"C:\answer.xml", It.IsAny<string>(), false), Times.Once);
        _mockFileSystem.Verify(fs => fs.MoveFileReplacing(It.IsAny<string>(), @"C:\work\autounattend.xml"), Times.Once);
    }

    [Fact]
    public async Task AddXmlToImageAsync_SelectedFileIsAlreadyTheDestination_SucceedsWithoutCopying()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        var result = await _service.AddXmlToImageAsync(@"C:\WIM\autounattend.xml", @"C:\WIM");

        result.Success.Should().BeTrue();
        _mockFileSystem.Verify(fs => fs.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        _mockDriverInstallStep.Verify(d => d.EnsureAsync(@"C:\WIM\autounattend.xml", @"C:\WIM", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddXmlToImageAsync_DestinationExists_ClearsItsAttributesBeforeCopying()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        var calls = new List<string>();
        _mockFileSystem.Setup(fs => fs.SetFileAttributes(@"C:\WIM\autounattend.xml", FileAttributes.Normal))
            .Callback(() => calls.Add("attributes"));
        _mockFileSystem.Setup(fs => fs.MoveFileReplacing(It.IsAny<string>(), @"C:\WIM\autounattend.xml"))
            .Callback(() => calls.Add("copy"));

        var result = await _service.AddXmlToImageAsync(@"C:\answer.xml", @"C:\WIM");

        result.Success.Should().BeTrue();
        calls.Should().Equal("attributes", "copy");
    }

    [Fact]
    public async Task AddXmlToImageAsync_CopyThrows_FailsWithTheExceptionMessage()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.CopyFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
            .Throws(new IOException("Disk error"));

        var result = await _service.AddXmlToImageAsync(@"C:\answer.xml", @"C:\work");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Disk error");
        result.Exception.Should().BeOfType<IOException>();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AddXmlToImageAsync_ImportsSidecarsOrPreservesExistingXmlWhenMissing(bool samePath, bool missing)
    {
        var root = Directory.CreateTempSubdirectory();
        try
        {
            string source = Path.Combine(root.FullName, "source");
            string target = samePath ? source : Path.Combine(root.FullName, "target");
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(target);
            string sourceXml = Path.Combine(source, "autounattend.xml");
            string targetXml = Path.Combine(target, "autounattend.xml");
            const string picture = @"C:\Wallpapers\Winhance\beach.jpg";
            byte[] bytes = [1, 2, 3];
            XNamespace x = AutounattendDocument.WinhanceExtensions;
            var doc = new XDocument(new XElement("unattend", new XElement(x + "Extensions",
                new XElement(x + "Generator", "Winhance"), MediaPayloads.Entry(picture, bytes, true))));
            doc.Save(sourceXml);
            if (!samePath)
                File.WriteAllText(targetXml, "<previous/>");
            var previous = File.ReadAllBytes(targetXml);
            if (!missing)
            {
                string sourcePicture = MediaPayloads.MediaPath(source, picture)!;
                Directory.CreateDirectory(Path.GetDirectoryName(sourcePicture)!);
                File.WriteAllBytes(sourcePicture, bytes);
            }

            var service = new WimCustomizationService(new FileSystemService(_mockLogService.Object), _mockLogService.Object,
                _mockLocalization.Object, _mockDriverCategorizer.Object, _mockDismRunner.Object, _mockDriverInstallStep.Object, _mockUpdateInstallStep.Object);
            var result = await service.AddXmlToImageAsync(sourceXml, target);

            result.Success.Should().Be(!missing);
            File.ReadAllBytes(targetXml).Should().Equal(missing ? previous : File.ReadAllBytes(sourceXml));
            if (!missing)
                File.ReadAllBytes(MediaPayloads.MediaPath(target, picture)!).Should().Equal(bytes);
            Directory.GetDirectories(target, ".winhance-media-*").Should().BeEmpty();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task AddDriversAsync_Extract_ExportsStraightIntoTheOemFolder()
    {
        _mockDismRunner
            .Setup(d => d.RunProcessWithProgressAsync("dism.exe", It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, ""));
        _mockDriverCategorizer
            .Setup(d => d.MoveStorageDrivers(@"C:\work\sources\$OEM$\$$\Drivers", @"C:\work\$WinpeDriver$"))
            .Returns(5);

        var result = await _service.AddDriversAsync(@"C:\work");

        result.Should().BeTrue();
        _mockFileSystem.Verify(fs => fs.CreateDirectory(@"C:\work\sources\$OEM$\$$\Drivers"), Times.Once);
        _mockDismRunner.Verify(d => d.RunProcessWithProgressAsync(
            "dism.exe",
            It.Is<string>(a => a.Contains("/Export-Driver") && a.Contains(@"C:\work\sources\$OEM$\$$\Drivers")),
            It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockDriverCategorizer.Verify(d => d.CategorizeAndCopyDrivers(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AddDriversAsync_Extract_DismFailure_KeepsWhatLanded()
    {
        _mockDismRunner
            .Setup(d => d.RunProcessWithProgressAsync("dism.exe", It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((87, "error"));
        _mockDriverCategorizer
            .Setup(d => d.MoveStorageDrivers(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(3);

        var result = await _service.AddDriversAsync(@"C:\work");

        result.Should().BeTrue();
        _mockLogService.Verify(l => l.LogWarning(It.Is<string>(m => m.Contains("87")), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task AddDriversAsync_Extract_NothingExported_ReturnsFalse()
    {
        _mockDismRunner
            .Setup(d => d.RunProcessWithProgressAsync("dism.exe", It.IsAny<string>(), It.IsAny<IProgress<TaskProgressDetail>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((0, ""));
        _mockDriverCategorizer
            .Setup(d => d.MoveStorageDrivers(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(0);

        var result = await _service.AddDriversAsync(@"C:\work");

        result.Should().BeFalse();
        _mockDriverInstallStep.Verify(w => w.EnsureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddDriversAsync_DriverSourcePathDoesNotExist_ReturnsFalse()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.Is<string>(p => p == @"C:\drivers")))
            .Returns(false);

        var result = await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddDriversAsync_NoDriversCopied_ReturnsFalse()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockDriverCategorizer.Setup(dc => dc.CategorizeAndCopyDrivers(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(0);

        var result = await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddDriversAsync_DriversSuccessfullyCopied_ReturnsTrue()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockDriverCategorizer.Setup(dc => dc.CategorizeAndCopyDrivers(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(5);

        var result = await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AddDriversAsync_StorageDriversGoToTheMediaRootNotTheSourcesFolder()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockDriverCategorizer
            .Setup(d => d.CategorizeAndCopyDrivers(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(1);

        await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        _mockDriverCategorizer.Verify(d => d.CategorizeAndCopyDrivers(
            It.IsAny<string>(),
            @"C:\work\$WinpeDriver$",
            It.IsAny<string>(),
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task AddDriversAsync_OnSuccess_EnsuresTheDriverInstallStep()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockDriverCategorizer
            .Setup(d => d.CategorizeAndCopyDrivers(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(2);

        await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        _mockDriverInstallStep.Verify(w => w.EnsureAsync(@"C:\work", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddDriversAsync_NothingCopied_DoesNotTouchTheXml()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockDriverCategorizer
            .Setup(d => d.CategorizeAndCopyDrivers(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(0);

        await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        _mockDriverInstallStep.Verify(w => w.EnsureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AddDriversAsync_EnsureFailure_DoesNotFailTheOperation()
    {
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockDriverCategorizer
            .Setup(d => d.CategorizeAndCopyDrivers(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(3);
        _mockDriverInstallStep
            .Setup(w => w.EnsureAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("locked"));

        var result = await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task AddXmlToImageAsync_EnsureFailure_StillSucceeds()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockDriverInstallStep
            .Setup(w => w.EnsureAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("locked"));

        var result = await _service.AddXmlToImageAsync(@"C:\answer.xml", @"C:\work");

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task AddXmlToImageAsync_EnsuresTheDriverInstallStepBeforePublication()
    {
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        var result = await _service.AddXmlToImageAsync(@"C:\answer.xml", @"C:\work");

        result.Success.Should().BeTrue();
        _mockDriverInstallStep.Verify(w => w.EnsureAsync(It.IsAny<string>(), @"C:\work", It.IsAny<CancellationToken>()), Times.Once);
    }
}
