using System.IO;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Infrastructure.Features.SoftwareApps.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class IconCacheMigrationTests : IDisposable
{
    private readonly Mock<ILogService> _mockLog = new();
    private readonly IconCacheMigration _sut;
    private readonly string _root;

    public IconCacheMigrationTests()
    {
        _sut = new IconCacheMigration(_mockLog.Object);
        _root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void EnsureSchema_NoSentinel_DeletesExistingFilesAndWritesSentinel()
    {
        // Arrange: a stale cache file, no .schema present
        var oldFile = Path.Combine(_root, "old.png");
        File.WriteAllText(oldFile, "stale");

        // Act
        _sut.EnsureSchema(_root, 2);

        // Assert: old file removed, sentinel written with current version
        File.Exists(oldFile).Should().BeFalse("stale cache file must be deleted when no sentinel is present");
        var sentinelPath = Path.Combine(_root, ".schema");
        File.Exists(sentinelPath).Should().BeTrue(".schema sentinel must be written");
        File.ReadAllText(sentinelPath).Should().Be("2");
    }

    [Fact]
    public void EnsureSchema_SentinelAlreadyCurrent_KeepsExistingFiles()
    {
        // Arrange: sentinel already at version 2, a cached file that must survive
        File.WriteAllText(Path.Combine(_root, ".schema"), "2");
        var keepFile = Path.Combine(_root, "keep.png");
        File.WriteAllText(keepFile, "cached icon");

        // Act
        _sut.EnsureSchema(_root, 2);

        // Assert: file is untouched
        File.Exists(keepFile).Should().BeTrue("cached file must not be deleted when schema is already current");
        File.ReadAllText(keepFile).Should().Be("cached icon");
    }

    [Fact]
    public void EnsureSchema_SentinelOlder_WipesFilesAndUpdatesSentinel()
    {
        // Arrange: sentinel at version 1 (older), a stale cache file
        File.WriteAllText(Path.Combine(_root, ".schema"), "1");
        var oldFile = Path.Combine(_root, "old.png");
        File.WriteAllText(oldFile, "stale v1 icon");

        // Act
        _sut.EnsureSchema(_root, 2);

        // Assert: old file removed, sentinel bumped to 2
        File.Exists(oldFile).Should().BeFalse("stale cache file must be deleted when schema is outdated");
        var sentinelPath = Path.Combine(_root, ".schema");
        File.ReadAllText(sentinelPath).Should().Be("2");
    }
}
