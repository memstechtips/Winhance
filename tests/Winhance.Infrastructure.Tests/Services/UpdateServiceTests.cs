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
    private readonly Mock<IWindowsRegistryService> _mockRegistryService = new();
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();
    private readonly Mock<IPowerShellRunner> _mockPowerShellRunner = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly Mock<IStateWriter> _mockStateWriter = new();
    private readonly UpdateService _service;

    public UpdateServiceTests()
    {
        _service = new UpdateService(
            _mockLogService.Object,
            _mockRegistryService.Object,
            _mockProcessExecutor.Object,
            _mockPowerShellRunner.Object,
            _mockFileSystemService.Object,
            _mockStateWriter.Object);
    }

    #region TryApplySpecialSettingAsync

    [Fact]
    public async Task TryApplySpecialSettingAsync_NonUpdatesPolicyMode_ReturnsFalse()
    {
        // Arrange

        // Act
        var result = await _service.TryApplySpecialSettingAsync("some-other-setting", 0);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryApplySpecialSettingAsync_UpdatesPolicyMode_NonIntValue_ReturnsFalse()
    {
        // Arrange

        // Act
        var result = await _service.TryApplySpecialSettingAsync("updates-policy-mode", "not-an-int");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ApplyUpdatesPolicyModeAsync

    [Fact]
    public async Task ApplyUpdatesPolicyModeAsync_NonIntValue_ThrowsArgumentException()
    {
        // Arrange

        // Act
        var act = () => _service.ApplyUpdatesPolicyModeAsync("invalid");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*integer selection index*");
    }

    [Fact]
    public async Task ApplyUpdatesPolicyModeAsync_InvalidIndex_ThrowsArgumentException()
    {
        // Arrange

        SetupProcessExecutor();

        // Act
        var act = () => _service.ApplyUpdatesPolicyModeAsync(99);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid selection index: 99*");
    }

    [Fact]
    public async Task ApplyUpdatesPolicyModeAsync_NormalMode_EnablesServicesAndTasks()
    {
        // Arrange

        SetupProcessExecutor();
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystemService.Setup(f => f.GetFileNameWithoutExtension(It.IsAny<string>()))
            .Returns<string>(s => System.IO.Path.GetFileNameWithoutExtension(s));

        // Act
        await _service.ApplyUpdatesPolicyModeAsync(0);

        // Assert — verify services were enabled (sc config and net start commands)
        _mockProcessExecutor.Verify(
            p => p.ExecuteAsync("cmd.exe", It.Is<string>(s => s.Contains("sc config"))),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ApplyUpdatesPolicyModeAsync_SecurityOnlyMode_AppliesRegistrySettings()
    {
        // Arrange

        SetupProcessExecutor();
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystemService.Setup(f => f.GetFileNameWithoutExtension(It.IsAny<string>()))
            .Returns<string>(s => System.IO.Path.GetFileNameWithoutExtension(s));

        // Act
        await _service.ApplyUpdatesPolicyModeAsync(1);

        // Assert — verify process commands were executed for enabling services
        _mockProcessExecutor.Verify(
            p => p.ExecuteAsync("cmd.exe", It.IsAny<string>()),
            Times.AtLeastOnce);
    }

    #endregion

    #region GetCurrentUpdatePolicyIndexAsync

    [Fact]
    public async Task GetCurrentUpdatePolicyIndexAsync_CriticalDllsRenamed_Returns3()
    {
        // Arrange — simulate that WaaSMedicSvc.dll backup exists and original is gone
        _mockFileSystemService.Setup(f => f.GetFileNameWithoutExtension("WaaSMedicSvc.dll"))
            .Returns("WaaSMedicSvc");
        _mockFileSystemService.Setup(f => f.GetFileNameWithoutExtension("wuaueng.dll"))
            .Returns("wuaueng");
        _mockFileSystemService.Setup(f => f.FileExists(@"C:\Windows\System32\WaaSMedicSvc_BAK.dll"))
            .Returns(true);
        _mockFileSystemService.Setup(f => f.FileExists(@"C:\Windows\System32\WaaSMedicSvc.dll"))
            .Returns(false);

        // Act
        var result = await _service.GetCurrentUpdatePolicyIndexAsync();

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public async Task GetCurrentUpdatePolicyIndexAsync_UpdatesPaused_Returns2()
    {
        // Arrange — no renamed DLLs
        _mockFileSystemService.Setup(f => f.GetFileNameWithoutExtension(It.IsAny<string>()))
            .Returns<string>(s => System.IO.Path.GetFileNameWithoutExtension(s));
        _mockFileSystemService.Setup(f => f.FileExists(It.Is<string>(p => p.Contains("_BAK"))))
            .Returns(false);

        // Simulate pause updates registry entries
        _mockRegistryService.Setup(r => r.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
            "PauseUpdatesStartTime"))
            .Returns("2025-01-01");

        // Act
        var result = await _service.GetCurrentUpdatePolicyIndexAsync();

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task GetCurrentUpdatePolicyIndexAsync_SecurityOnlyDefer_Returns1()
    {
        // Arrange — no renamed DLLs, no pause
        _mockFileSystemService.Setup(f => f.GetFileNameWithoutExtension(It.IsAny<string>()))
            .Returns<string>(s => System.IO.Path.GetFileNameWithoutExtension(s));
        _mockFileSystemService.Setup(f => f.FileExists(It.Is<string>(p => p.Contains("_BAK"))))
            .Returns(false);

        _mockRegistryService.Setup(r => r.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
            "PauseUpdatesStartTime"))
            .Returns((object?)null);
        _mockRegistryService.Setup(r => r.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
            "PauseUpdatesExpiryTime"))
            .Returns((object?)null);
        _mockRegistryService.Setup(r => r.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
            "PausedQualityDate"))
            .Returns((object?)null);
        _mockRegistryService.Setup(r => r.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
            "PausedFeatureDate"))
            .Returns((object?)null);

        // DeferFeatureUpdates = 1 means security only
        _mockRegistryService.Setup(r => r.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\WindowsUpdate\UX\Settings",
            "DeferFeatureUpdates"))
            .Returns(1);

        // Act
        var result = await _service.GetCurrentUpdatePolicyIndexAsync();

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task GetCurrentUpdatePolicyIndexAsync_NormalMode_Returns0()
    {
        // Arrange — no renamed DLLs, no pause, no defer
        _mockFileSystemService.Setup(f => f.GetFileNameWithoutExtension(It.IsAny<string>()))
            .Returns<string>(s => System.IO.Path.GetFileNameWithoutExtension(s));
        _mockFileSystemService.Setup(f => f.FileExists(It.Is<string>(p => p.Contains("_BAK"))))
            .Returns(false);

        _mockRegistryService.Setup(r => r.GetValue(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((object?)null);

        // Act
        var result = await _service.GetCurrentUpdatePolicyIndexAsync();

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region Helpers

    private void SetupProcessExecutor()
    {
        _mockProcessExecutor
            .Setup(p => p.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessExecutionResult { ExitCode = 0, StandardOutput = "", StandardError = "" });
    }

    #endregion
}
