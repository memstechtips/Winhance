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
    // GetCompatibilityBanner
    // ──────────────────────────────────────────────────

    [Fact]
    public void GetCompatibilityBanner_NullDefinition_ReturnsNull()
    {
        // Act
        var result = _manager.GetCompatibilityBanner(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCompatibilityBanner_NoCompatibilityInfo_ReturnsNull()
    {
        // Arrange
        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting"
        };

        // Act
        var result = _manager.GetCompatibilityBanner(definition);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetCompatibilityBanner_WithCompatibilityMessage_ReturnsWarningBanner()
    {
        // Arrange
        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting",
            VersionCompatibilityMessage = "Windows 11 only"
        };

        // Act
        var result = _manager.GetCompatibilityBanner(definition);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Message.Should().Be("Windows 11 only");
        result.Value.Severity.Should().Be(InfoBarSeverity.Warning);
    }

    [Fact]
    public void GetCompatibilityBanner_WithEmptyCompatibilityMessage_ReturnsWarningBanner()
    {
        // Arrange
        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting",
            VersionCompatibilityMessage = ""
        };

        // Act
        var result = _manager.GetCompatibilityBanner(definition);

        // Assert
        // VersionCompatibilityMessage is a non-null string, so the pattern match succeeds
        result.Should().NotBeNull();
    }

    // ──────────────────────────────────────────────────
    // ComputeBannerForValue
    // ──────────────────────────────────────────────────

    [Fact]
    public void ComputeBannerForValue_NullDefinition_ReturnsClear()
    {
        // Act
        var result = _manager.ComputeBannerForValue(null, 0, null);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Message.Should().BeNull();
        result.Value.Severity.Should().Be(InfoBarSeverity.Informational);
    }

    [Fact]
    public void ComputeBannerForValue_NonIntValue_WithNoCompatibility_ReturnsClear()
    {
        // Arrange
        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting"
        };

        // Act
        var result = _manager.ComputeBannerForValue(definition, "not-an-int", null);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Message.Should().BeNull();
    }

    [Fact]
    public void ComputeBannerForValue_NonIntValue_WithCompatibility_ReturnsNull()
    {
        // Arrange - when there's a compatibility message, null means "keep existing banner"
        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting",
            VersionCompatibilityMessage = "Win11 only"
        };

        // Act
        var result = _manager.ComputeBannerForValue(definition, "not-an-int", null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ComputeBannerForValue_WithMatchingOptionWarning_ReturnsErrorBanner()
    {
        // Arrange
        var warningDict = new Dictionary<int, string>
        {
            { 1, "Security risk!" }
        };
        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting",
            ComboBox = new ComboBoxMetadata
            {
                DisplayNames = new[] { "Option A", "Option B" },
                OptionWarnings = warningDict
            }
        };

        // Act
        var result = _manager.ComputeBannerForValue(definition, 1, null);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Message.Should().Be("Security risk!");
        result.Value.Severity.Should().Be(InfoBarSeverity.Error);
    }

    [Fact]
    public void ComputeBannerForValue_WithNonMatchingOptionWarning_ReturnsClear()
    {
        // Arrange
        var warningDict = new Dictionary<int, string>
        {
            { 1, "Security risk!" }
        };
        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting",
            ComboBox = new ComboBoxMetadata
            {
                DisplayNames = new[] { "Option A", "Option B" },
                OptionWarnings = warningDict
            }
        };

        // Act - index 0 has no warning
        var result = _manager.ComputeBannerForValue(definition, 0, null);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Message.Should().BeNull();
    }

    [Fact]
    public void ComputeBannerForValue_WithCrossGroupChildSettings_CustomIndex_ShowsCrossGroupMessage()
    {
        // Arrange
        var displayNames = new[] { "Option A", "Option B", "Custom" };
        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting",
            CrossGroupChildSettings = new Dictionary<string, string> { ["child1"] = "child1" },
            ComboBox = new ComboBoxMetadata { DisplayNames = displayNames }
        };

        // Act - select last index (Custom)
        var result = _manager.ComputeBannerForValue(definition, 2, "Cross-group info message");

        // Assert
        result.Should().NotBeNull();
        result!.Value.Message.Should().Be("Cross-group info message");
        result.Value.Severity.Should().Be(InfoBarSeverity.Warning);
    }

    [Fact]
    public void ComputeBannerForValue_WithCrossGroupChildSettings_NonCustomIndex_ReturnsClear()
    {
        // Arrange
        var displayNames = new[] { "Option A", "Option B", "Custom" };
        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting",
            CrossGroupChildSettings = new Dictionary<string, string> { ["child1"] = "child1" },
            ComboBox = new ComboBoxMetadata { DisplayNames = displayNames }
        };

        // Act - select first index (not Custom)
        var result = _manager.ComputeBannerForValue(definition, 0, "Cross-group info message");

        // Assert
        result.Should().NotBeNull();
        result!.Value.Message.Should().BeNull();
    }

    [Fact]
    public void ComputeBannerForValue_WithCrossGroupChildSettings_CustomStateIndex_ShowsCrossGroupMessage()
    {
        // Arrange
        var displayNames = new[] { "Option A", "Option B", "Custom" };
        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting",
            CrossGroupChildSettings = new Dictionary<string, string> { ["child1"] = "child1" },
            ComboBox = new ComboBoxMetadata { DisplayNames = displayNames }
        };

        // Act - use ComboBoxConstants.CustomStateIndex (-1)
        var result = _manager.ComputeBannerForValue(definition, ComboBoxConstants.CustomStateIndex, "Custom state message");

        // Assert
        result.Should().NotBeNull();
        result!.Value.Message.Should().Be("Custom state message");
        result.Value.Severity.Should().Be(InfoBarSeverity.Warning);
    }

    [Fact]
    public void ComputeBannerForValue_WithCrossGroupChildSettings_CustomIndex_NoCrossGroupMessage_ShowsFallbackHeader()
    {
        // Arrange
        _mockLocalizationService
            .Setup(l => l.GetString("Setting_CrossGroupWarning_Header"))
            .Returns("Warning: Cross-group settings");

        var displayNames = new[] { "Option A", "Custom" };
        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting",
            CrossGroupChildSettings = new Dictionary<string, string> { ["child1"] = "child1" },
            ComboBox = new ComboBoxMetadata { DisplayNames = displayNames }
        };

        // Act - custom index with null crossGroupInfoMessage
        var result = _manager.ComputeBannerForValue(definition, 1, null);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Message.Should().Be("Warning: Cross-group settings");
        result.Value.Severity.Should().Be(InfoBarSeverity.Warning);
    }

    [Fact]
    public void ComputeBannerForValue_WithCompatibilityMessage_NoWarning_ReturnsCompatibilityBanner()
    {
        // Arrange
        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting",
            VersionCompatibilityMessage = "Requires Windows 11 22H2+"
        };

        // Act
        var result = _manager.ComputeBannerForValue(definition, 0, null);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Message.Should().Be("Requires Windows 11 22H2+");
        result.Value.Severity.Should().Be(InfoBarSeverity.Warning);
    }

    [Fact]
    public void ComputeBannerForValue_NoWarningsNoCompat_ReturnsClear()
    {
        // Arrange
        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting"
        };

        // Act
        var result = _manager.ComputeBannerForValue(definition, 0, null);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Message.Should().BeNull();
    }

    // ──────────────────────────────────────────────────
    // GetRestartBanner
    // ──────────────────────────────────────────────────

    [Fact]
    public void GetRestartBanner_NullDefinition_ReturnsNull()
    {
        // Act
        var result = _manager.GetRestartBanner(null, true);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetRestartBanner_NoRestartRequired_ReturnsNull()
    {
        // Arrange
        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting",
            RequiresRestart = false
        };

        // Act
        var result = _manager.GetRestartBanner(definition, true);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetRestartBanner_NotChangedThisSession_ReturnsNull()
    {
        // Arrange
        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting",
            RequiresRestart = true
        };

        // Act
        var result = _manager.GetRestartBanner(definition, hasChangedThisSession: false);

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

        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting",
            RequiresRestart = true
        };

        // Act
        var result = _manager.GetRestartBanner(definition, hasChangedThisSession: true);

        // Assert
        result.Should().NotBeNull();
        result!.Value.Message.Should().Be("Restart your PC to apply changes.");
        result.Value.Severity.Should().Be(InfoBarSeverity.Warning);
    }

    [Fact]
    public void GetRestartBanner_RequiresRestartAndChanged_CallsLocalizationService()
    {
        // Arrange
        var definition = new SettingDefinition
        {
            Id = "test",
            Name = "Test",
            Description = "Test setting",
            RequiresRestart = true
        };

        // Act
        _manager.GetRestartBanner(definition, hasChangedThisSession: true);

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
