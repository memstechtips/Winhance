using FluentAssertions;
using Moq;
using Winhance.Core.Features.AdvancedTools.Interfaces;
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
    private readonly Mock<IOscdimgToolManager> _mockOscdimgManager = new();
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
            _mockOscdimgManager.Object);
    }

    #region ValidateIsoFileAsync

    [Fact]
    public async Task ValidateIsoFileAsync_FileDoesNotExist_ReturnsFalse()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _service.ValidateIsoFileAsync(@"C:\test.iso");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateIsoFileAsync_WrongExtension_ReturnsFalse()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetExtension(It.IsAny<string>())).Returns(".txt");

        // Act
        var result = await _service.ValidateIsoFileAsync(@"C:\test.txt");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateIsoFileAsync_FileTooSmall_ReturnsFalse()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetExtension(It.IsAny<string>())).Returns(".iso");
        _mockFileSystem.Setup(fs => fs.GetFileSize(It.IsAny<string>())).Returns(512); // < 1MB

        // Act
        var result = await _service.ValidateIsoFileAsync(@"C:\test.iso");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateIsoFileAsync_ValidIsoFile_ReturnsTrue()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetExtension(It.IsAny<string>())).Returns(".iso");
        _mockFileSystem.Setup(fs => fs.GetFileSize(It.IsAny<string>())).Returns(5_000_000_000L);

        // Act
        var result = await _service.ValidateIsoFileAsync(@"C:\test.iso");

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region CleanupWorkingDirectoryAsync

    [Fact]
    public async Task CleanupWorkingDirectoryAsync_DirectoryDoesNotExist_ReturnsTrue()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(false);

        // Act
        var result = await _service.CleanupWorkingDirectoryAsync(@"C:\work");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CleanupWorkingDirectoryAsync_DirectoryExists_DeletesAndReturnsTrue()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);

        // Act
        var result = await _service.CleanupWorkingDirectoryAsync(@"C:\work");

        // Assert
        result.Should().BeTrue();
        _mockFileSystem.Verify(
            fs => fs.DeleteDirectory(@"C:\work", true),
            Times.Once);
    }

    [Fact]
    public async Task CleanupWorkingDirectoryAsync_DeleteThrows_ReturnsFalse()
    {
        // Arrange
        _mockFileSystem.Setup(fs => fs.DirectoryExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.DeleteDirectory(It.IsAny<string>(), It.IsAny<bool>()))
            .Throws(new UnauthorizedAccessException("Access denied"));

        // Act
        var result = await _service.CleanupWorkingDirectoryAsync(@"C:\work");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CreateIsoAsync

    [Fact]
    public async Task CreateIsoAsync_OscdimgNotAvailable_ReturnsFalse()
    {
        // Arrange
        _mockOscdimgManager.Setup(m => m.GetOscdimgPath()).Returns(string.Empty);
        _mockOscdimgManager.Setup(m => m.IsOscdimgAvailableAsync()).ReturnsAsync(false);

        // Act
        var result = await _service.CreateIsoAsync(@"C:\work", @"C:\output.iso");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CreateIsoAsync_BootFileNotFound_ReturnsFalse()
    {
        // Arrange
        _mockOscdimgManager.Setup(m => m.GetOscdimgPath()).Returns(@"C:\tools\oscdimg.exe");
        _mockOscdimgManager.Setup(m => m.IsOscdimgAvailableAsync()).ReturnsAsync(true);
        _mockFileSystem.Setup(fs => fs.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.IO.SearchOption>()))
            .Returns(Array.Empty<string>());
        _mockDismRunner.Setup(d => d.CheckDiskSpaceAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystem.Setup(fs => fs.GetDirectoryName(It.IsAny<string>())).Returns(@"C:\output");

        // Act
        var result = await _service.CreateIsoAsync(@"C:\work", @"C:\output\output.iso");

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
