using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Optimize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class UpdateServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly Mock<IWindowsRegistryService> _mockRegistryService = new();
    private readonly Mock<ICompatibleSettingsRegistry> _mockCompatibleSettingsRegistry = new();
    private readonly Mock<IProcessExecutor> _mockProcessExecutor = new();
    private readonly Mock<IPowerShellRunner> _mockPowerShellRunner = new();
    private readonly Mock<IFileSystemService> _mockFileSystemService = new();
    private readonly UpdateService _service;

    public UpdateServiceTests()
    {
        _service = new UpdateService(
            _mockLogService.Object,
            _mockRegistryService.Object,
            _mockCompatibleSettingsRegistry.Object,
            _mockProcessExecutor.Object,
            _mockPowerShellRunner.Object,
            _mockFileSystemService.Object);
    }

    #region DomainName

    [Fact]
    public void DomainName_ReturnsUpdate()
    {
        _service.DomainName.Should().Be("Update");
    }

    #endregion

    #region GetSettingsAsync

    [Fact]
    public async Task GetSettingsAsync_ReturnsSettingsFromRegistry()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            new SettingDefinition
            {
                Id = "updates-policy-mode",
                Name = "Windows Update Policy",
                Description = "Control Windows Update behavior"
            }
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Update"))
            .Returns(settings);

        // Act
        var result = await _service.GetSettingsAsync();

        // Assert
        result.Should().BeSameAs(settings);
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenRegistryThrows_ReturnsEmptyAndLogs()
    {
        // Arrange
        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Update"))
            .Throws(new InvalidOperationException("Update registry failure"));

        // Act
        var result = await _service.GetSettingsAsync();

        // Assert
        result.Should().BeEmpty();
        _mockLogService.Verify(
            l => l.Log(
                Core.Features.Common.Enums.LogLevel.Error,
                It.Is<string>(s => s.Contains("Update") && s.Contains("Update registry failure"))),
            Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_CalledTwice_ReturnsSameReference()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            new SettingDefinition
            {
                Id = "updates-policy-mode",
                Name = "Policy Mode",
                Description = "Test"
            }
        };

        _mockCompatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings("Update"))
            .Returns(settings);

        // Act
        var result1 = await _service.GetSettingsAsync();
        var result2 = await _service.GetSettingsAsync();

        // Assert
        result1.Should().BeSameAs(result2);
    }

    #endregion

    #region TryApplySpecialSettingAsync

    [Fact]
    public async Task TryApplySpecialSettingAsync_NonUpdatesPolicyMode_ReturnsFalse()
    {
        // Arrange
        var setting = new SettingDefinition
        {
            Id = "some-other-setting",
            Name = "Other",
            Description = "Not updates policy"
        };

        // Act
        var result = await _service.TryApplySpecialSettingAsync(setting, 0);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TryApplySpecialSettingAsync_UpdatesPolicyMode_NonIntValue_ReturnsFalse()
    {
        // Arrange
        var setting = new SettingDefinition
        {
            Id = "updates-policy-mode",
            Name = "Policy",
            Description = "Policy mode"
        };

        // Act
        var result = await _service.TryApplySpecialSettingAsync(setting, "not-an-int");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ApplyUpdatesPolicyModeAsync

    [Fact]
    public async Task ApplyUpdatesPolicyModeAsync_NonIntValue_ThrowsArgumentException()
    {
        // Arrange
        var setting = new SettingDefinition
        {
            Id = "updates-policy-mode",
            Name = "Policy",
            Description = "Policy mode"
        };

        // Act
        var act = () => _service.ApplyUpdatesPolicyModeAsync(setting, "invalid");

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*integer selection index*");
    }

    [Fact]
    public async Task ApplyUpdatesPolicyModeAsync_InvalidIndex_ThrowsArgumentException()
    {
        // Arrange
        var setting = new SettingDefinition
        {
            Id = "updates-policy-mode",
            Name = "Policy",
            Description = "Policy mode"
        };

        SetupProcessExecutor();

        // Act
        var act = () => _service.ApplyUpdatesPolicyModeAsync(setting, 99);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid selection index: 99*");
    }

    [Fact]
    public async Task ApplyUpdatesPolicyModeAsync_NormalMode_EnablesServicesAndTasks()
    {
        // Arrange
        var setting = new SettingDefinition
        {
            Id = "updates-policy-mode",
            Name = "Policy",
            Description = "Policy mode"
        };

        SetupProcessExecutor();
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystemService.Setup(f => f.GetFileNameWithoutExtension(It.IsAny<string>()))
            .Returns<string>(s => System.IO.Path.GetFileNameWithoutExtension(s));

        // Act
        await _service.ApplyUpdatesPolicyModeAsync(setting, 0);

        // Assert — verify services were enabled (sc config and net start commands)
        _mockProcessExecutor.Verify(
            p => p.ExecuteAsync("cmd.exe", It.Is<string>(s => s.Contains("sc config"))),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ApplyUpdatesPolicyModeAsync_SecurityOnlyMode_AppliesRegistrySettings()
    {
        // Arrange
        var setting = new SettingDefinition
        {
            Id = "updates-policy-mode",
            Name = "Policy",
            Description = "Policy mode"
        };

        SetupProcessExecutor();
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockFileSystemService.Setup(f => f.GetFileNameWithoutExtension(It.IsAny<string>()))
            .Returns<string>(s => System.IO.Path.GetFileNameWithoutExtension(s));

        // Act
        await _service.ApplyUpdatesPolicyModeAsync(setting, 1);

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

    #region DiscoverSpecialSettingsAsync

    [Fact]
    public async Task DiscoverSpecialSettingsAsync_NoMatchingSetting_ReturnsEmptyDictionary()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            new SettingDefinition
            {
                Id = "unrelated-setting",
                Name = "Unrelated",
                Description = "Not updates policy"
            }
        };

        // Act
        var result = await _service.DiscoverSpecialSettingsAsync(settings);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DiscoverSpecialSettingsAsync_WithUpdatesPolicyMode_ReturnsPolicyIndex()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            new SettingDefinition
            {
                Id = "updates-policy-mode",
                Name = "Policy Mode",
                Description = "Updates policy"
            }
        };

        // Set up for normal mode (index 0)
        _mockFileSystemService.Setup(f => f.GetFileNameWithoutExtension(It.IsAny<string>()))
            .Returns<string>(s => System.IO.Path.GetFileNameWithoutExtension(s));
        _mockFileSystemService.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
        _mockRegistryService.Setup(r => r.GetValue(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((object?)null);

        // Act
        var result = await _service.DiscoverSpecialSettingsAsync(settings);

        // Assert
        result.Should().ContainKey("updates-policy-mode");
        result["updates-policy-mode"].Should().ContainKey("CurrentPolicyIndex");
        result["updates-policy-mode"]["CurrentPolicyIndex"].Should().Be(0);
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
