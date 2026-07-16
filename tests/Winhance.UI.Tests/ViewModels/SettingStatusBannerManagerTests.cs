using System.Collections.Generic;
using FluentAssertions;
using Microsoft.UI.Xaml.Controls;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;
using Xunit;

namespace Winhance.UI.Tests.ViewModels;

public class SettingStatusBannerManagerTests
{
    private readonly Mock<ILocalizationService> _mockLocalizationService;
    private readonly SettingStatusBannerManager _manager;

    public SettingStatusBannerManagerTests()
    {
        _mockLocalizationService = new Mock<ILocalizationService>();
        _mockLocalizationService
            .Setup(l => l.GetString(It.IsAny<string>()))
            .Returns((string key) => key);

        _manager = new SettingStatusBannerManager(_mockLocalizationService.Object);
    }

    // ──────────────────────────────────────────────────
    // ComputeBannerForValue
    // ──────────────────────────────────────────────────

    [Fact]
    public void ComputeBannerForValue_IntValue_NoWarningsNoCrossGroupNoCompat_ReturnsClear()
    {
        var result = _manager.ComputeBannerForValue(0, null, null, 0, null);

        result.Should().NotBeNull();
        result!.Value.Message.Should().BeNull();
        result.Value.Severity.Should().Be(InfoBarSeverity.Informational);
    }

    [Fact]
    public void ComputeBannerForValue_NonIntValue_WithNoCompatibility_ReturnsClear()
    {
        var result = _manager.ComputeBannerForValue("not-an-int", null, null, 0, null);

        result.Should().NotBeNull();
        result!.Value.Message.Should().BeNull();
    }

    [Fact]
    public void ComputeBannerForValue_NonIntValue_WithCompatibility_ReturnsNull()
    {
        // A non-int value with a compatibility message keeps the existing banner (returns null).
        var result = _manager.ComputeBannerForValue("not-an-int", null, null, 0, "Win11 only");

        result.Should().BeNull();
    }

    [Fact]
    public void ComputeBannerForValue_WithMatchingOptionWarning_ReturnsErrorBanner()
    {
        var optionWarnings = new string?[] { null, "Security risk!" };

        var result = _manager.ComputeBannerForValue(1, optionWarnings, null, 2, null);

        result.Should().NotBeNull();
        result!.Value.Message.Should().Be("Security risk!");
        result.Value.Severity.Should().Be(InfoBarSeverity.Error);
    }

    [Fact]
    public void ComputeBannerForValue_WithNonMatchingOptionWarning_ReturnsClear()
    {
        var optionWarnings = new string?[] { null, "Security risk!" };

        // index 0 has no warning
        var result = _manager.ComputeBannerForValue(0, optionWarnings, null, 2, null);

        result.Should().NotBeNull();
        result!.Value.Message.Should().BeNull();
    }

    [Fact]
    public void ComputeBannerForValue_WithCrossGroupMessage_CustomIndex_ShowsCrossGroupMessage()
    {
        // last index (2) is the Custom option of a 3-option setting
        var result = _manager.ComputeBannerForValue(2, null, "Cross-group info message", 3, null);

        result.Should().NotBeNull();
        result!.Value.Message.Should().Be("Cross-group info message");
        result.Value.Severity.Should().Be(InfoBarSeverity.Warning);
    }

    [Fact]
    public void ComputeBannerForValue_WithCrossGroupMessage_NonCustomIndex_ReturnsClear()
    {
        // index 0 is not the Custom option
        var result = _manager.ComputeBannerForValue(0, null, "Cross-group info message", 3, null);

        result.Should().NotBeNull();
        result!.Value.Message.Should().BeNull();
    }

    [Fact]
    public void ComputeBannerForValue_WithCrossGroupMessage_CustomStateIndex_ShowsCrossGroupMessage()
    {
        var result = _manager.ComputeBannerForValue(ComboBoxConstants.CustomStateIndex, null, "Custom state message", 3, null);

        result.Should().NotBeNull();
        result!.Value.Message.Should().Be("Custom state message");
        result.Value.Severity.Should().Be(InfoBarSeverity.Warning);
    }

    [Fact]
    public void ComputeBannerForValue_WithCompatibilityMessage_NoWarning_ReturnsCompatibilityBanner()
    {
        var result = _manager.ComputeBannerForValue(0, null, null, 0, "Requires Windows 11 22H2+");

        result.Should().NotBeNull();
        result!.Value.Message.Should().Be("Requires Windows 11 22H2+");
        result.Value.Severity.Should().Be(InfoBarSeverity.Warning);
    }

    [Fact]
    public void ComputeBannerForValue_NoWarningsNoCompat_ReturnsClear()
    {
        var result = _manager.ComputeBannerForValue(0, null, null, 0, null);

        result.Should().NotBeNull();
        result!.Value.Message.Should().BeNull();
    }

    // ──────────────────────────────────────────────────
    // GetRestartBanner
    // ──────────────────────────────────────────────────

    [Fact]
    public void GetRestartBanner_NoRestartRequired_ReturnsNull()
    {
        // Act
        var result = _manager.GetRestartBanner(false, true);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetRestartBanner_NotChangedThisSession_ReturnsNull()
    {
        // Act
        var result = _manager.GetRestartBanner(true, hasChangedThisSession: false);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetRestartBanner_RequiresRestartAndChangedThisSession_ReturnsWarningBanner()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString("Common_RestartRequired"))
            .Returns("Restart your PC to apply changes.");

        // Act
        var result = _manager.GetRestartBanner(true, hasChangedThisSession: true);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Message.Should().Be("Restart your PC to apply changes.");
        result.Value.Severity.Should().Be(InfoBarSeverity.Warning);
    }

    [Fact]
    public void GetRestartBanner_RequiresRestartAndChanged_CallsLocalizationService()
    {
        // Act
        _manager.GetRestartBanner(true, hasChangedThisSession: true);

        // Assert
        _mockLocalizationService.Verify(l => l.GetString("Common_RestartRequired"), Times.Once);
    }

    // ──────────────────────────────────────────────────
    // BannerState record struct
    // ──────────────────────────────────────────────────

    [Fact]
    public void BannerState_Clear_HasNullMessageAndInformationalSeverity()
    {
        // Act
        var clear = SettingStatusBannerManager.BannerState.Clear;

        // Assert
        clear.Message.Should().BeNull();
        clear.Severity.Should().Be(InfoBarSeverity.Informational);
    }
}
