using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.AdvancedTools.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.AdvancedTools.WimServices;

public class WimCustomizationServiceTests
{
    private readonly Mock<IFileSystemService> _mockFileSystem = new();
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<HttpMessageHandler> _mockHttpHandler = new();
    private readonly HttpClient _httpClient;
    private readonly Mock<ILocalizationService> _mockLocalization = new();
    private readonly Mock<IDriverCategorizer> _mockDriverCategorizer = new();
    private readonly Mock<IDismProcessRunner> _mockDismRunner = new();
    private readonly WimCustomizationService _service;

    public WimCustomizationServiceTests()
    {
        _httpClient = new HttpClient(_mockHttpHandler.Object);

        _mockFileSystem
            .Setup(fs => fs.CombinePath(It.IsAny<string[]>()))
            .Returns((string[] paths) => string.Join("\\", paths));

        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>(), It.IsAny<object[]>()))
            .Returns((string key, object[] args) => key);
        _mockLocalization
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _service = new WimCustomizationService(
            _mockFileSystem.Object,
            _mockLogService.Object,
            _httpClient,
            _mockLocalization.Object,
            _mockDriverCategorizer.Object,
            _mockDismRunner.Object);
    }

    #region AddXmlToImageAsync

    [Fact]
    public async Task AddXmlToImageAsync_XmlFileNotFound_ReturnsFalse()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _service.AddXmlToImageAsync(@"C:\missing.xml", @"C:\work");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddXmlToImageAsync_WorkingDirectoryNotFound_ReturnsFalse()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _service.AddXmlToImageAsync(@"C:\answer.xml", @"C:\missing_dir");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddXmlToImageAsync_ValidInputs_CopiesFileAndReturnsTrue()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("<xml>content</xml>");
        _mockFileSystem.Setup(fs => fs.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.AddXmlToImageAsync(@"C:\answer.xml", @"C:\work");

        // Assert
        result.Should().BeTrue();
        _mockFileSystem.Verify(
            fs => fs.WriteAllTextAsync(
                It.Is<string>(p => p.Contains("autounattend.xml")),
                "<xml>content</xml>",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AddXmlToImageAsync_WriteThrows_ReturnsFalse()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.ReadAllTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk error"));

        // Act
        var result = await _service.AddXmlToImageAsync(@"C:\answer.xml", @"C:\work");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region AddDriversAsync

    [Fact]
    public async Task AddDriversAsync_DriverSourcePathDoesNotExist_ReturnsFalse()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.Is<string>(p => p == @"C:\drivers")))
            .Returns(false);

        // Act
        var result = await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddDriversAsync_NoDriversCopied_ReturnsFalse()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockDriverCategorizer.Setup(dc => dc.CategorizeAndCopyDrivers(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(0);

        // Act
        var result = await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task AddDriversAsync_DriversSuccessfullyCopied_ReturnsTrue()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockDriverCategorizer.Setup(dc => dc.CategorizeAndCopyDrivers(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(5);
        _mockFileSystem.Setup(fs => fs.CreateDirectory(It.IsAny<string>()));
        _mockFileSystem.Setup(fs => fs.WriteAllText(It.IsAny<string>(), It.IsAny<string>()));

        // Act
        var result = await _service.AddDriversAsync(@"C:\work", @"C:\drivers");

        // Assert
        result.Should().BeTrue();
    }

    #endregion
}
