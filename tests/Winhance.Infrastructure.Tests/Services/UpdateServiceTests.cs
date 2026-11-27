using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Optimize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class UpdateServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();
    private readonly Mock<IPowerShellRunner> _mockPowerShellRunner = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IStateWriter> _mockStateWriter = new();
    private readonly Mock<IAsyncEffectRunner> _mockAsyncEffects = new();
    private readonly UpdateService _service;

    public UpdateServiceTests()
    {
        _service = new UpdateService(
            _mockLogService.Object,
            _mockProcessExecutor.Object,
            _mockPowerShellRunner.Object,
            _mockFileSystemService.Object,
            _mockStateWriter.Object,
            _mockAsyncEffects.Object);
        _mockAsyncEffects
            .Setup(r => r.RunAllAsync(It.IsAny<IReadOnlyList<Effect>>()))
            .ReturnsAsync(Array.Empty<string>());
    }

    [Fact]
    public async Task TryApplySpecialSettingAsync_NonUpdatesPolicyMode_ReturnsFalse()
    {
        var result = await _service.TryApplySpecialSettingAsync("some-other-setting", 0);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryApplySpecialSettingAsync_UpdatesPolicyMode_NonIntValue_ReturnsFalse()
    {
        var result = await _service.TryApplySpecialSettingAsync("updates-policy-mode", "not-an-int");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyUpdatesPolicyModeAsync_NonIntValue_ThrowsArgumentException()
    {
        var act = () => _service.ApplyUpdatesPolicyModeAsync("invalid");

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*integer selection index*");
    }

    [Fact]
    public async Task ApplyUpdatesPolicyModeAsync_InvalidIndex_ThrowsArgumentException()
    {
        SetupProcessExecutor();

        var act = () => _service.ApplyUpdatesPolicyModeAsync(99);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid selection index: 99*");
    }

    [Fact]
    public async Task ApplyUpdatesPolicyModeAsync_NormalMode_EnablesServicesAndTasks()
    {
        SetupProcessExecutor();
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystemService.Setup(f => f.GetFileNameWithoutExtension(It.IsAny<string>()))
            .Returns<string>(s => System.IO.Path.GetFileNameWithoutExtension(s));

        await _service.ApplyUpdatesPolicyModeAsync(0);

        _mockProcessExecutor.Verify(
            p => p.ExecuteAsync("cmd.exe", It.Is<string>(s => s.Contains("sc config"))),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ApplyUpdatesPolicyModeAsync_SecurityOnlyMode_AppliesRegistrySettings()
    {
        SetupProcessExecutor();
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystemService.Setup(f => f.GetFileNameWithoutExtension(It.IsAny<string>()))
            .Returns<string>(s => System.IO.Path.GetFileNameWithoutExtension(s));

        await _service.ApplyUpdatesPolicyModeAsync(1);

        _mockProcessExecutor.Verify(
            p => p.ExecuteAsync("cmd.exe", It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ApplyUpdatesPolicyModeAsync_NormalMode_RunsTheStatesAsyncEffects()
    {
        SetupProcessExecutor();
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystemService.Setup(f => f.GetFileNameWithoutExtension(It.IsAny<string>()))
            .Returns<string>(s => System.IO.Path.GetFileNameWithoutExtension(s));

        await _service.ApplyUpdatesPolicyModeAsync(0);

        _mockAsyncEffects.Verify(r => r.RunAllAsync(It.IsAny<IReadOnlyList<Effect>>()), Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ApplyUpdatesPolicyModeAsync_SecurityOnlyAndPaused_EnableTheUpdateTasks(int index)
    {
        SetupProcessExecutor();
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystemService.Setup(f => f.GetFileNameWithoutExtension(It.IsAny<string>()))
            .Returns<string>(s => System.IO.Path.GetFileNameWithoutExtension(s));

        await _service.ApplyUpdatesPolicyModeAsync(index);

        VerifyTaskScript("Enable-ScheduledTask", @"\Microsoft\Windows\UpdateOrchestrator\", Times.Once());
        VerifyTaskScript("Enable-ScheduledTask", @"\Microsoft\Windows\WindowsUpdate\", Times.Once());
    }

    private void VerifyTaskScript(string cmdlet, string folder, Times times) =>
        _mockPowerShellRunner.Verify(
            r => r.RunScriptAsync(
                It.Is<string>(s => s.Contains($"-TaskPath '{folder}'") && s.Contains(cmdlet)),
                It.IsAny<IProgress<TaskProgressDetail>?>(),
                It.IsAny<CancellationToken>()),
            times);

    private void SetupProcessExecutor()
    {
        _mockProcessExecutor
            .Setup(p => p.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0, StandardOutput = "", StandardError = "" });
    }
}
