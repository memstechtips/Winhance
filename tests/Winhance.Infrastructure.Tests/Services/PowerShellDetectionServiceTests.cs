using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Utilities;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class PowerShellDetectionServiceTests
{
    private readonly Mock<ILogService> _log = new();
    private PowerShellDetectionService NewService() => new(_log.Object);

    private static SettingDefinition Setting(string id, string detectionScript) =>
        new()
        {
            Id = id,
            Name = id,
            Description = id,
            DetectionType = DetectionType.PowerShellScript,
            PowerShellScripts = new[]
            {
                new PowerShellScriptSetting { DetectionScript = detectionScript }
            }
        };

    [Fact]
    public async Task DetectAsync_ReturnsTrue_ForScriptEmittingTrue()
    {
        var svc = NewService();
        var result = await svc.DetectAsync(new[] { Setting("a", "$true") });
        result.Should().ContainKey("a").WhoseValue.Should().BeTrue();
    }

    [Fact]
    public async Task DetectAsync_ReturnsFalse_ForScriptEmittingFalse()
    {
        var svc = NewService();
        var result = await svc.DetectAsync(new[] { Setting("a", "$false") });
        result.Should().ContainKey("a").WhoseValue.Should().BeFalse();
    }

    [Fact]
    public async Task DetectAsync_AcceptsNumericLiteralOutput()
    {
        var svc = NewService();
        var result = await svc.DetectAsync(new[]
        {
            Setting("one", "1"),
            Setting("zero", "0"),
        });
        result["one"].Should().BeTrue();
        result["zero"].Should().BeFalse();
    }

    [Fact]
    public async Task DetectAsync_ReturnsFalse_WhenScriptThrows()
    {
        var svc = NewService();
        var result = await svc.DetectAsync(new[]
        {
            Setting("a", "throw 'boom'"),
        });
        result["a"].Should().BeFalse();
        _log.Verify(l => l.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("'a'"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task DetectAsync_ReturnsFalse_WhenOutputIsMalformed()
    {
        var svc = NewService();
        var result = await svc.DetectAsync(new[]
        {
            Setting("a", "'hello world'"),
        });
        result["a"].Should().BeFalse();
    }

    [Fact]
    public async Task DetectAsync_RunsMultipleSettings_InOneCall()
    {
        var svc = NewService();
        var result = await svc.DetectAsync(new[]
        {
            Setting("a", "$true"),
            Setting("b", "$false"),
            Setting("c", "$true"),
        });
        result.Should().HaveCount(3);
        result["a"].Should().BeTrue();
        result["b"].Should().BeFalse();
        result["c"].Should().BeTrue();
    }

    [Fact]
    public async Task DetectAsync_SkipsSettings_WithoutDetectionScript()
    {
        var svc = NewService();
        var withScript = Setting("with", "$true");
        var withoutScript = new SettingDefinition
        {
            Id = "without",
            Name = "without",
            Description = "without",
            DetectionType = DetectionType.PowerShellScript,
            PowerShellScripts = new[] { new PowerShellScriptSetting { EnabledScript = "Enable" } }
        };

        var result = await svc.DetectAsync(new[] { withScript, withoutScript });

        result.Should().ContainKey("with");
        result.Should().NotContainKey("without");
    }

    [Fact]
    public async Task DetectAsync_AbortsBatch_WhenBatchTimeoutExceeded()
    {
        var svc = NewService();
        // Each setting sleeps 6 seconds; with two of them the batch will breach the 10 s limit.
        var result = await svc.DetectAsync(new[]
        {
            Setting("a", "Start-Sleep -Seconds 6; $true"),
            Setting("b", "Start-Sleep -Seconds 6; $true"),
        });

        // Whichever scripts didn't complete should read as false.
        result.Values.Should().Contain(false);
    }
}
