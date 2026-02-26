using System.IO;
using FluentAssertions;
using Winhance.Core.Features.Common.Services;
using Xunit;

namespace Winhance.Core.Tests.Services;

public class LogServiceTests
{
    [Fact]
    public void GetLogPath_ReturnsNonEmptyPath()
    {
        using var service = new LogService();

        var path = service.GetLogPath();

        path.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GetLogPath_ContainsWinhanceDirectory()
    {
        using var service = new LogService();

        var path = service.GetLogPath();

        path.Should().Contain("Winhance");
        path.Should().Contain("Logs");
        path.Should().EndWith(".log");
    }

    [Fact]
    public void Log_WithDifferentLevels_DoesNotThrow()
    {
        using var service = new LogService();

        // Without calling StartLog(), these should still not throw
        // (they just won't write to the file)
        var action = () =>
        {
            service.Log(Winhance.Core.Features.Common.Enums.LogLevel.Info, "info message");
            service.Log(Winhance.Core.Features.Common.Enums.LogLevel.Warning, "warning message");
            service.Log(Winhance.Core.Features.Common.Enums.LogLevel.Error, "error message");
            service.Log(Winhance.Core.Features.Common.Enums.LogLevel.Debug, "debug message");
            service.Log(Winhance.Core.Features.Common.Enums.LogLevel.Success, "success message");
        };

        action.Should().NotThrow();
    }

    [Fact]
    public void Log_WithException_DoesNotThrow()
    {
        using var service = new LogService();

        var ex = new InvalidOperationException("test error");
        var action = () => service.Log(
            Winhance.Core.Features.Common.Enums.LogLevel.Error, "error", ex);

        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        var service = new LogService();

        var action = () =>
        {
            service.Dispose();
            service.Dispose();
        };

        action.Should().NotThrow();
    }

    [Fact]
    public void SetInteractiveUserService_DoesNotThrow()
    {
        using var service = new LogService();
        var mockService = new Moq.Mock<Winhance.Core.Features.Common.Interfaces.IInteractiveUserService>();

        var action = () => service.SetInteractiveUserService(mockService.Object);

        action.Should().NotThrow();
    }

    // ── CleanupOldLogs (BP-7) ──

    [Fact]
    public void CleanupOldLogs_DeletesFilesOlderThanMaxAge()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"WinhanceLogTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Create an "old" log file with a creation time well in the past
            var oldFile = Path.Combine(tempDir, "Winhance_Log_20200101_000000.log");
            File.WriteAllText(oldFile, "old");
            File.SetCreationTimeUtc(oldFile, DateTime.UtcNow.AddDays(-60));

            // Create a "recent" log file
            var recentFile = Path.Combine(tempDir, "Winhance_Log_20260226_120000.log");
            File.WriteAllText(recentFile, "recent");

            LogService.CleanupOldLogs(tempDir, maxAgeDays: 30, maxFiles: 100);

            File.Exists(oldFile).Should().BeFalse("file older than maxAgeDays should be deleted");
            File.Exists(recentFile).Should().BeTrue("recent file should be kept");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CleanupOldLogs_CapsToMaxFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"WinhanceLogTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            // Create 5 files, all recent
            for (int i = 0; i < 5; i++)
            {
                var file = Path.Combine(tempDir, $"Winhance_Log_20260226_{i:D6}.log");
                File.WriteAllText(file, $"log {i}");
                File.SetCreationTimeUtc(file, DateTime.UtcNow.AddMinutes(-5 + i));
            }

            LogService.CleanupOldLogs(tempDir, maxAgeDays: 30, maxFiles: 3);

            var remaining = Directory.GetFiles(tempDir, "Winhance_Log_*.log");
            remaining.Should().HaveCount(3, "should cap to maxFiles");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void CleanupOldLogs_NonExistentDirectory_DoesNotThrow()
    {
        var action = () => LogService.CleanupOldLogs(
            Path.Combine(Path.GetTempPath(), "NonExistentDir_" + Guid.NewGuid().ToString("N")));

        action.Should().NotThrow();
    }

    [Fact]
    public void CleanupOldLogs_EmptyDirectory_DoesNotThrow()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"WinhanceLogTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var action = () => LogService.CleanupOldLogs(tempDir);
            action.Should().NotThrow();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
