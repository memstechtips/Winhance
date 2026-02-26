using FluentAssertions;
using Microsoft.Win32;
using Moq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class RecommendedSettingsApplierTests
{
    private readonly Mock<IDomainServiceRouter> _mockRouter = new();
    private readonly Mock<IRecommendedSettingsService> _mockRecommendedService = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly Mock<ISettingApplicationService> _mockAppService = new();
    private readonly RecommendedSettingsApplier _applier;

    public RecommendedSettingsApplierTests()
    {
        _applier = new RecommendedSettingsApplier(
            _mockRouter.Object,
            _mockRecommendedService.Object,
            _mockLog.Object);
    }

    private static SettingDefinition CreateToggleSetting(
        string id,
        object? recommendedValue,
        object? enabledValue = null) => new()
    {
        Id = id,
        Name = $"Setting {id}",
        Description = $"Description for {id}",
        InputType = InputType.Toggle,
        RegistrySettings = new[]
        {
            new RegistrySetting
            {
                KeyPath = @"HKLM\Software\Test",
                ValueName = "TestValue",
                ValueType = RegistryValueKind.DWord,
                RecommendedValue = recommendedValue,
                EnabledValue = enabledValue ?? recommendedValue,
            }
        }
    };

    private static SettingDefinition CreateSelectionSetting(
        string id,
        string recommendedOption,
        Dictionary<string, int> comboBoxOptions) => new()
    {
        Id = id,
        Name = $"Setting {id}",
        Description = $"Description for {id}",
        InputType = InputType.Selection,
        RegistrySettings = new[]
        {
            new RegistrySetting
            {
                KeyPath = @"HKLM\Software\Test",
                ValueName = "TestValue",
                ValueType = RegistryValueKind.DWord,
                IsPrimary = true,
                RecommendedValue = comboBoxOptions[recommendedOption],
                CustomProperties = new Dictionary<string, object>
                {
                    ["RecommendedOption"] = recommendedOption,
                    ["ComboBoxOptions"] = comboBoxOptions,
                },
            }
        }
    };

    private void SetupDomainService(string settingId, string domainName = "TestDomain")
    {
        var mockDomain = new Mock<IDomainService>();
        mockDomain.Setup(d => d.DomainName).Returns(domainName);
        _mockRouter.Setup(r => r.GetDomainService(settingId)).Returns(mockDomain.Object);
    }

    // ---------------------------------------------------------------
    // Test Case 1: Settings with Toggle input - applies recommended value
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedSettingsForDomainAsync_ToggleSetting_AppliesRecommendedValue()
    {
        // Arrange
        const string settingId = "toggle-setting";
        SetupDomainService(settingId);

        var setting = CreateToggleSetting(settingId, recommendedValue: 1, enabledValue: 1);
        _mockRecommendedService
            .Setup(s => s.GetRecommendedSettingsAsync(settingId))
            .ReturnsAsync(new[] { setting });

        _mockAppService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        await _applier.ApplyRecommendedSettingsForDomainAsync(settingId, _mockAppService.Object);

        // Assert
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == settingId &&
            r.Enable == true &&
            r.Value != null && r.Value.Equals(1) &&
            r.SkipValuePrerequisites == true
        )), Times.Once);
    }

    [Fact]
    public async Task ApplyRecommendedSettingsForDomainAsync_ToggleSetting_EnableFalse_WhenRecommendedNotEqualEnabled()
    {
        // Arrange: RecommendedValue = 0, EnabledValue = 1 => enableValue = false
        const string settingId = "toggle-disable";
        SetupDomainService(settingId);

        var setting = CreateToggleSetting(settingId, recommendedValue: 0, enabledValue: 1);
        _mockRecommendedService
            .Setup(s => s.GetRecommendedSettingsAsync(settingId))
            .ReturnsAsync(new[] { setting });

        _mockAppService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        await _applier.ApplyRecommendedSettingsForDomainAsync(settingId, _mockAppService.Object);

        // Assert
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == settingId &&
            r.Enable == false &&
            r.Value != null && r.Value.Equals(0) &&
            r.SkipValuePrerequisites == true
        )), Times.Once);
    }

    [Fact]
    public async Task ApplyRecommendedSettingsForDomainAsync_MultipleToggleSettings_AppliesAll()
    {
        // Arrange
        const string settingId = "first-setting";
        SetupDomainService(settingId);

        var setting1 = CreateToggleSetting("setting-a", recommendedValue: 1, enabledValue: 1);
        var setting2 = CreateToggleSetting("setting-b", recommendedValue: 0, enabledValue: 1);

        _mockRecommendedService
            .Setup(s => s.GetRecommendedSettingsAsync(settingId))
            .ReturnsAsync(new[] { setting1, setting2 });

        _mockAppService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        await _applier.ApplyRecommendedSettingsForDomainAsync(settingId, _mockAppService.Object);

        // Assert
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "setting-a" && r.Enable == true
        )), Times.Once);

        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "setting-b" && r.Enable == false
        )), Times.Once);
    }

    // ---------------------------------------------------------------
    // Test Case 2: Settings with Selection input - applies recommended selection index
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedSettingsForDomainAsync_SelectionSetting_AppliesRecommendedIndex()
    {
        // Arrange
        const string settingId = "selection-setting";
        SetupDomainService(settingId);

        // ComboBoxOptions are ordered alphabetically by key: "High"=3, "Low"=1, "Medium"=2
        // Sorted order: High(0), Low(1), Medium(2)
        // "Medium" with value 2 is at alphabetical index 2
        var comboBoxOptions = new Dictionary<string, int>
        {
            ["High"] = 3,
            ["Low"] = 1,
            ["Medium"] = 2,
        };

        var setting = CreateSelectionSetting(settingId, "Medium", comboBoxOptions);
        _mockRecommendedService
            .Setup(s => s.GetRecommendedSettingsAsync(settingId))
            .ReturnsAsync(new[] { setting });

        _mockAppService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        await _applier.ApplyRecommendedSettingsForDomainAsync(settingId, _mockAppService.Object);

        // Assert: "Medium" is at index 2 (alphabetical: High=0, Low=1, Medium=2)
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == settingId &&
            r.Enable == true &&
            r.Value != null && r.Value.Equals(2) &&
            r.SkipValuePrerequisites == true
        )), Times.Once);
    }

    [Fact]
    public async Task ApplyRecommendedSettingsForDomainAsync_SelectionSetting_NoPrimaryRegistry_UsesFallbackValue()
    {
        // Arrange: Selection setting without primary registry (no RecommendedOption),
        // so it falls through to the else branch using recommendedValue directly
        const string settingId = "selection-fallback";
        SetupDomainService(settingId);

        var setting = new SettingDefinition
        {
            Id = settingId,
            Name = "Fallback Selection",
            Description = "Test",
            InputType = InputType.Selection,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKLM\Software\Test",
                    ValueName = "TestValue",
                    ValueType = RegistryValueKind.DWord,
                    IsPrimary = false,
                    RecommendedValue = 42,
                }
            }
        };

        _mockRecommendedService
            .Setup(s => s.GetRecommendedSettingsAsync(settingId))
            .ReturnsAsync(new[] { setting });

        _mockAppService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        await _applier.ApplyRecommendedSettingsForDomainAsync(settingId, _mockAppService.Object);

        // Assert: Falls through to the else branch since GetRecommendedOptionFromSetting returns null
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == settingId &&
            r.Enable == true &&
            r.Value != null && r.Value.Equals(42) &&
            r.SkipValuePrerequisites == true
        )), Times.Once);
    }

    // ---------------------------------------------------------------
    // Test Case 3: No recommended settings - does nothing
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedSettingsForDomainAsync_NoRecommendedSettings_DoesNothing()
    {
        // Arrange
        const string settingId = "no-recommended";
        SetupDomainService(settingId);

        _mockRecommendedService
            .Setup(s => s.GetRecommendedSettingsAsync(settingId))
            .ReturnsAsync(Enumerable.Empty<SettingDefinition>());

        // Act
        await _applier.ApplyRecommendedSettingsForDomainAsync(settingId, _mockAppService.Object);

        // Assert
        _mockAppService.Verify(
            s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()),
            Times.Never);
    }

    [Fact]
    public async Task ApplyRecommendedSettingsForDomainAsync_EmptyList_LogsNoSettingsFound()
    {
        // Arrange
        const string settingId = "empty-domain";
        SetupDomainService(settingId, "EmptyDomain");

        _mockRecommendedService
            .Setup(s => s.GetRecommendedSettingsAsync(settingId))
            .ReturnsAsync(Array.Empty<SettingDefinition>());

        // Act
        await _applier.ApplyRecommendedSettingsForDomainAsync(settingId, _mockAppService.Object);

        // Assert: Verify it logged the "no recommended settings" message
        _mockLog.Verify(l => l.Log(
            LogLevel.Info,
            It.Is<string>(msg => msg.Contains("No recommended settings found")),
            null), Times.Once);
    }

    // ---------------------------------------------------------------
    // Test Case 4: Domain service not found - handles gracefully
    // ---------------------------------------------------------------

    [Fact]
    public async Task ApplyRecommendedSettingsForDomainAsync_DomainServiceNotFound_ThrowsAndLogs()
    {
        // Arrange: Router throws when domain is not found
        const string settingId = "unknown-domain";
        _mockRouter
            .Setup(r => r.GetDomainService(settingId))
            .Throws(new ArgumentException($"No domain service found for '{settingId}'"));

        // Act
        var action = () => _applier.ApplyRecommendedSettingsForDomainAsync(settingId, _mockAppService.Object);

        // Assert: The outer catch re-throws after logging
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage($"*{settingId}*");

        _mockLog.Verify(l => l.Log(
            LogLevel.Error,
            It.Is<string>(msg => msg.Contains("Error applying recommended settings")),
            null), Times.Once);
    }

    [Fact]
    public async Task ApplyRecommendedSettingsForDomainAsync_IndividualSettingFails_ContinuesWithOthers()
    {
        // Arrange: Two settings, first one fails during ApplySetting
        const string settingId = "partial-fail";
        SetupDomainService(settingId);

        var setting1 = CreateToggleSetting("fail-setting", recommendedValue: 1, enabledValue: 1);
        var setting2 = CreateToggleSetting("succeed-setting", recommendedValue: 1, enabledValue: 1);

        _mockRecommendedService
            .Setup(s => s.GetRecommendedSettingsAsync(settingId))
            .ReturnsAsync(new[] { setting1, setting2 });

        _mockAppService
            .Setup(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "fail-setting")))
            .ThrowsAsync(new InvalidOperationException("Apply failed"));

        _mockAppService
            .Setup(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r => r.SettingId == "succeed-setting")))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act: Should not throw despite individual failure
        await _applier.ApplyRecommendedSettingsForDomainAsync(settingId, _mockAppService.Object);

        // Assert: Second setting was still applied
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == "succeed-setting"
        )), Times.Once);

        // Assert: Warning logged for the failed setting
        _mockLog.Verify(l => l.Log(
            LogLevel.Warning,
            It.Is<string>(msg => msg.Contains("fail-setting") && msg.Contains("Apply failed")),
            null), Times.Once);
    }

    [Fact]
    public async Task ApplyRecommendedSettingsForDomainAsync_OtherInputType_AppliesWithRecommendedValue()
    {
        // Arrange: A setting with a non-Toggle, non-Selection input type (e.g., NumericRange)
        const string settingId = "numeric-setting";
        SetupDomainService(settingId);

        var setting = new SettingDefinition
        {
            Id = settingId,
            Name = "Numeric Setting",
            Description = "Test",
            InputType = InputType.NumericRange,
            RegistrySettings = new[]
            {
                new RegistrySetting
                {
                    KeyPath = @"HKLM\Software\Test",
                    ValueName = "NumericVal",
                    ValueType = RegistryValueKind.DWord,
                    RecommendedValue = 75,
                }
            }
        };

        _mockRecommendedService
            .Setup(s => s.GetRecommendedSettingsAsync(settingId))
            .ReturnsAsync(new[] { setting });

        _mockAppService
            .Setup(s => s.ApplySettingAsync(It.IsAny<ApplySettingRequest>()))
            .ReturnsAsync(OperationResult.Succeeded());

        // Act
        await _applier.ApplyRecommendedSettingsForDomainAsync(settingId, _mockAppService.Object);

        // Assert: Falls through to the else branch, Enable=true, Value=75
        _mockAppService.Verify(s => s.ApplySettingAsync(It.Is<ApplySettingRequest>(r =>
            r.SettingId == settingId &&
            r.Enable == true &&
            r.Value != null && r.Value.Equals(75) &&
            r.SkipValuePrerequisites == true
        )), Times.Once);
    }
}
