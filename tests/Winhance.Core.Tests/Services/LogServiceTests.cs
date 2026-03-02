using System.IO;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
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
        var mockService = new Mock<IInteractiveUserService>();

        var action = () => service.SetInteractiveUserService(mockService.Object);

        action.Should().NotThrow();
    }

    [Fact]
    public void SetSystemInfoProvider_DoesNotThrow()
    {
        using var service = new LogService();
        var mockProvider = new Mock<ISystemInfoProvider>();

        var action = () => service.SetSystemInfoProvider(mockProvider.Object);

        action.Should().NotThrow();
    }

    [Fact]
    public void StartLog_WithSystemInfoProvider_WritesAllDiagnosticFields()
    {
        var service = new LogService();
        var mockProvider = new Mock<ISystemInfoProvider>();
        mockProvider.Setup(p => p.Collect()).Returns(new SystemInfo
        {
            AppVersion = "26.02.20",
            OperatingSystem = "Windows 11 Pro 25H2 (Build 26100.4061)",
            Architecture = "x64",
            DeviceType = "Laptop",
            Cpu = "AMD Ryzen 7 7840HS (16 cores)",
            Ram = "32 GB",
            Gpu = "NVIDIA GeForce RTX 4060 (Dedicated)",
            DotNetRuntime = ".NET 10.0.0",
            Elevation = "Admin (OTS)",
            FirmwareType = "UEFI",
            SecureBoot = "Enabled",
            Tpm = "2.0",
            DomainJoined = "No"
        });
        service.SetSystemInfoProvider(mockProvider.Object);
        service.StartLog();
        var logPath = service.GetLogPath();
        service.Dispose(); // Release file lock before reading

        var logContent = File.ReadAllText(logPath);
        logContent.Should().Contain("Winhance 26.02.20 Log Started");
        logContent.Should().Contain("OS:            Windows 11 Pro 25H2 (Build 26100.4061)");
        logContent.Should().Contain("Architecture:  x64");
        logContent.Should().Contain("Device Type:   Laptop");
        logContent.Should().Contain("CPU:           AMD Ryzen 7 7840HS (16 cores)");
        logContent.Should().Contain("RAM:           32 GB");
        logContent.Should().Contain("GPU:           NVIDIA GeForce RTX 4060 (Dedicated)");
        logContent.Should().Contain(".NET Runtime:  .NET 10.0.0");
        logContent.Should().Contain("Elevation:     Admin (OTS)");
        logContent.Should().Contain("Firmware:      UEFI");
        logContent.Should().Contain("Secure Boot:   Enabled");
        logContent.Should().Contain("TPM:           2.0");
        logContent.Should().Contain("Domain Joined: No");
        logContent.Should().Contain("=====================================");
    }

    [Fact]
    public void StartLog_WithoutSystemInfoProvider_WritesFallbackMessage()
    {
        var service = new LogService();
        service.StartLog();
        var logPath = service.GetLogPath();
        service.Dispose(); // Release file lock before reading

        var logContent = File.ReadAllText(logPath);
        logContent.Should().Contain("Winhance Log Started");
        logContent.Should().Contain("System info unavailable (provider not configured)");
    }

    [Fact]
    public void StartLog_DoesNotContainUserOrMachineInfo()
    {
        var service = new LogService();
        var mockProvider = new Mock<ISystemInfoProvider>();
        mockProvider.Setup(p => p.Collect()).Returns(new SystemInfo());
        service.SetSystemInfoProvider(mockProvider.Object);
        service.StartLog();
        var logPath = service.GetLogPath();
        service.Dispose(); // Release file lock before reading

        var logContent = File.ReadAllText(logPath);
        logContent.Should().NotContain("User:");
        logContent.Should().NotContain("Elevated User:");
        logContent.Should().NotContain("Machine:");
        logContent.Should().NotContain("Timestamp:");
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
