using System.Text;
using FluentAssertions;
using Winhance.Infrastructure.Features.Common.Services;
using Winhance.IntegrationTests.Fixtures;
using Xunit;

namespace Winhance.IntegrationTests.FileSystem;

[Trait("Category", "Integration")]
public class FileSystemServiceTests : IClassFixture<TempDirectoryFixture>
{
    private readonly TempDirectoryFixture _fixture;
    private readonly FileSystemService _service;

    public FileSystemServiceTests(TempDirectoryFixture fixture)
    {
        _fixture = fixture;
        _service = new FileSystemService();
    }

    private string TempFile(string name) => Path.Combine(_fixture.TempPath, name);

    [Fact]
    public void CreateDirectory_DirectoryExists_ReturnsTrue()
    {
        // Arrange
        var dirPath = Path.Combine(_fixture.TempPath, "NewDir");

        // Act
        _service.CreateDirectory(dirPath);

        // Assert
        _service.DirectoryExists(dirPath).Should().BeTrue();
    }

    [Fact]
    public async Task WriteAndReadText_RoundTrips()
    {
        // Arrange
        var filePath = TempFile("text_roundtrip.txt");
        var content = "Hello, Winhance integration tests!\nLine 2.";

        // Act
        await _service.WriteAllTextAsync(filePath, content);
        var readBack = await _service.ReadAllTextAsync(filePath);

        // Assert
        readBack.Should().Be(content);
    }

    [Fact]
    public async Task WriteAndReadBytes_RoundTrips()
    {
        // Arrange
        var filePath = TempFile("bytes_roundtrip.bin");
        var content = new byte[] { 0x00, 0x01, 0x42, 0xFF, 0xDE, 0xAD };

        // Act
        await _service.WriteAllBytesAsync(filePath, content);
        var readBack = await _service.ReadAllBytesAsync(filePath);

        // Assert
        readBack.Should().BeEquivalentTo(content);
    }

    [Fact]
    public async Task WriteAndReadText_WithEncoding_PreservesContent()
    {
        // Arrange
        var filePath = TempFile("utf8_test.txt");
        var content = "Unicode test: \u00e9\u00e8\u00ea \u00fc\u00f6\u00e4 \u2603 \u2764";

        // Act
        await _service.WriteAllTextAsync(filePath, content);
        var readBack = _service.ReadAllText(filePath, Encoding.UTF8);

        // Assert
        readBack.Should().Be(content);
    }

    [Fact]
    public void GetFiles_WithPattern_ReturnsMatching()
    {
        // Arrange
        var subDir = Path.Combine(_fixture.TempPath, "PatternTest");
        _service.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "file1.txt"), "a");
        File.WriteAllText(Path.Combine(subDir, "file2.txt"), "b");
        File.WriteAllText(Path.Combine(subDir, "data.json"), "{}");

        // Act
        var txtFiles = _service.GetFiles(subDir, "*.txt");
        var jsonFiles = _service.GetFiles(subDir, "*.json");

        // Assert
        txtFiles.Should().HaveCount(2);
        jsonFiles.Should().HaveCount(1);
    }

    [Fact]
    public void DeleteFile_FileNoLongerExists()
    {
        // Arrange
        var filePath = TempFile("to_delete.txt");
        File.WriteAllText(filePath, "temporary");
        _service.FileExists(filePath).Should().BeTrue();

        // Act
        _service.DeleteFile(filePath);

        // Assert
        _service.FileExists(filePath).Should().BeFalse();
    }

    [Fact]
    public void CopyFile_BothExist()
    {
        // Arrange
        var source = TempFile("copy_source.txt");
        var dest = TempFile("copy_dest.txt");
        File.WriteAllText(source, "original content");

        // Act
        _service.CopyFile(source, dest);

        // Assert
        _service.FileExists(source).Should().BeTrue("source should still exist after copy");
        _service.FileExists(dest).Should().BeTrue("destination should exist after copy");
        File.ReadAllText(dest).Should().Be("original content");
    }

    [Fact]
    public void MoveFile_OnlyDestExists()
    {
        // Arrange
        var source = TempFile("move_source.txt");
        var dest = TempFile("move_dest.txt");
        File.WriteAllText(source, "moved content");

        // Act
        _service.MoveFile(source, dest);

        // Assert
        _service.FileExists(source).Should().BeFalse("source should not exist after move");
        _service.FileExists(dest).Should().BeTrue("destination should exist after move");
        File.ReadAllText(dest).Should().Be("moved content");
    }

    [Fact]
    public void GetFileSize_ReturnsCorrectSize()
    {
        // Arrange
        var filePath = TempFile("size_test.txt");
        var content = "12345"; // 5 ASCII bytes
        File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // Act
        var size = _service.GetFileSize(filePath);

        // Assert
        size.Should().Be(5);
    }

    [Fact]
    public void GetFileName_ExtractsName()
    {
        // Act & Assert
        _service.GetFileName(@"C:\Users\Test\Documents\config.json").Should().Be("config.json");
        _service.GetFileNameWithoutExtension(@"C:\path\to\file.txt").Should().Be("file");
        _service.GetExtension(@"C:\path\to\file.winhance").Should().Be(".winhance");
    }

    [Fact]
    public void GetDirectories_ReturnsCreatedDirs()
    {
        // Arrange
        var baseDir = Path.Combine(_fixture.TempPath, "DirListTest");
        _service.CreateDirectory(baseDir);
        _service.CreateDirectory(Path.Combine(baseDir, "SubA"));
        _service.CreateDirectory(Path.Combine(baseDir, "SubB"));
        _service.CreateDirectory(Path.Combine(baseDir, "SubC"));

        // Act
        var dirs = _service.GetDirectories(baseDir);

        // Assert
        dirs.Should().HaveCount(3);
        dirs.Select(Path.GetFileName).Should().Contain("SubA");
        dirs.Select(Path.GetFileName).Should().Contain("SubB");
        dirs.Select(Path.GetFileName).Should().Contain("SubC");
    }
}
