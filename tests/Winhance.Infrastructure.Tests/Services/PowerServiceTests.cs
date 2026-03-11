using System;
using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Common.Native;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Optimize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class PowerServiceTests
{
    private readonly Mock<ILogService> _logService;
    private readonly Mock<IPowerSettingsQueryService> _powerSettingsQueryService;
    private readonly Mock<ICompatibleSettingsRegistry> _compatibleSettingsRegistry;
    private readonly Mock<IEventBus> _eventBus;
    private readonly Mock<IPowerPlanComboBoxService> _powerPlanComboBoxService;
    private readonly Mock<IProcessExecutor> _processExecutor;
    private readonly Mock<IFileSystemService> _fileSystemService;
    private readonly Mock<IPowerSchemeOperations> _powerSchemeOperations;
    private readonly PowerService _sut;

    public PowerServiceTests()
    {
        _logService = new Mock<ILogService>();
        _powerSettingsQueryService = new Mock<IPowerSettingsQueryService>();
        _compatibleSettingsRegistry = new Mock<ICompatibleSettingsRegistry>();
        _eventBus = new Mock<IEventBus>();
        _powerPlanComboBoxService = new Mock<IPowerPlanComboBoxService>();
        _processExecutor = new Mock<IProcessExecutor>();
        _fileSystemService = new Mock<IFileSystemService>();
        _powerSchemeOperations = new Mock<IPowerSchemeOperations>();

        _sut = new PowerService(
            _logService.Object,
            _powerSettingsQueryService.Object,
            _compatibleSettingsRegistry.Object,
            _eventBus.Object,
            _powerPlanComboBoxService.Object,
            _processExecutor.Object,
            _fileSystemService.Object,
            _powerSchemeOperations.Object);
    }

    private static SettingDefinition MakeSetting(string id, string? name = null, string? description = null) =>
        new()
        {
            Id = id,
            Name = name ?? id,
            Description = description ?? $"Description for {id}",
        };

    [Fact]
    public void DomainName_ReturnsPower()
    {
        _sut.DomainName.Should().Be(FeatureIds.Power);
    }

    [Fact]
    public void DomainName_EqualsLiteralPowerString()
    {
        _sut.DomainName.Should().Be("Power");
    }

    [Fact]
    public async Task GetSettingsAsync_ReturnsFilteredSettings()
    {
        // Arrange
        var expectedSettings = new List<SettingDefinition>
        {
            MakeSetting("power-plan-selection", "Power Plan"),
            MakeSetting("power-display-timeout", "Turn off the display"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.Power))
            .Returns(expectedSettings);

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        result.Should().BeEquivalentTo(expectedSettings);
        _compatibleSettingsRegistry.Verify(r => r.GetFilteredSettings(FeatureIds.Power), Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_CachesResults_SecondCallReturnsSameReference()
    {
        // Arrange
        var expectedSettings = new List<SettingDefinition>
        {
            MakeSetting("power-plan-selection", "Power Plan"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.Power))
            .Returns(expectedSettings);

        // Act
        var firstResult = await _sut.GetSettingsAsync();
        var secondResult = await _sut.GetSettingsAsync();

        // Assert
        firstResult.Should().BeSameAs(secondResult);
        _compatibleSettingsRegistry.Verify(r => r.GetFilteredSettings(FeatureIds.Power), Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_WhenRegistryThrows_ReturnsEmptyAndLogs()
    {
        // Arrange
        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.Power))
            .Throws(new InvalidOperationException("Registry not initialized"));

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        result.Should().BeEmpty();
        _logService.Verify(
            l => l.Log(LogLevel.Error, It.Is<string>(s => s.Contains("Error loading Power settings"))),
            Times.Once);
    }

    [Fact]
    public async Task GetSettingsAsync_SettingsIncludeExpectedIds()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            MakeSetting("power-plan-selection"),
            MakeSetting("power-display-timeout"),
            MakeSetting("power-harddisk-timeout"),
        };

        _compatibleSettingsRegistry
            .Setup(r => r.GetFilteredSettings(FeatureIds.Power))
            .Returns(settings);

        // Act
        var result = await _sut.GetSettingsAsync();

        // Assert
        var resultList = result.ToList();
        resultList.Should().Contain(s => s.Id == "power-plan-selection");
        resultList.Should().Contain(s => s.Id == "power-display-timeout");
        resultList.Should().Contain(s => s.Id == "power-harddisk-timeout");
    }

    [Fact]
    public async Task InvalidateCache_ClearsCache_NextCallReloads()
    {
        // Arrange
        var firstSettings = new List<SettingDefinition>
        {
            MakeSetting("power-plan-selection"),
        };
        var secondSettings = new List<SettingDefinition>
        {
            MakeSetting("power-plan-selection"),
            MakeSetting("power-display-timeout"),
        };

        _compatibleSettingsRegistry
            .SetupSequence(r => r.GetFilteredSettings(FeatureIds.Power))
            .Returns(firstSettings)
            .Returns(secondSettings);

        // Act
        var first = await _sut.GetSettingsAsync();
        _sut.InvalidateCache();
        var afterInvalidation = await _sut.GetSettingsAsync();

        // Assert
        first.Should().HaveCount(1);
        afterInvalidation.Should().HaveCount(2);
        _compatibleSettingsRegistry.Verify(r => r.GetFilteredSettings(FeatureIds.Power), Times.Exactly(2));
    }

    [Fact]
    public async Task GetActivePowerPlanAsync_DelegatesToQueryService()
    {
        // Arrange
        var expectedPlan = new Winhance.Core.Features.Optimize.Models.PowerPlan
        {
            Name = "Balanced",
            Guid = "381b4222-f694-41f0-9685-ff5bb260df2e"
        };

        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(expectedPlan);

        // Act
        var result = await _sut.GetActivePowerPlanAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Balanced");
        result.Guid.Should().Be("381b4222-f694-41f0-9685-ff5bb260df2e");
    }

    [Fact]
    public async Task GetActivePowerPlanAsync_WhenQueryServiceThrows_ReturnsNull()
    {
        // Arrange
        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ThrowsAsync(new Exception("Query failed"));

        // Act
        var result = await _sut.GetActivePowerPlanAsync();

        // Assert
        result.Should().BeNull();
        _logService.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("Error getting active power plan"))),
            Times.Once);
    }

    [Fact]
    public async Task GetAvailablePowerPlansAsync_ReturnsPlansList()
    {
        // Arrange
        var plans = new List<Winhance.Core.Features.Optimize.Models.PowerPlan>
        {
            new() { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e" },
            new() { Name = "High Performance", Guid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" },
        };

        _powerSettingsQueryService
            .Setup(s => s.GetAvailablePowerPlansAsync())
            .ReturnsAsync(plans);

        // Act
        var result = await _sut.GetAvailablePowerPlansAsync();

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAvailablePowerPlansAsync_WhenQueryServiceThrows_ReturnsEmpty()
    {
        // Arrange
        _powerSettingsQueryService
            .Setup(s => s.GetAvailablePowerPlansAsync())
            .ThrowsAsync(new Exception("Query failed"));

        // Act
        var result = await _sut.GetAvailablePowerPlansAsync();

        // Assert
        result.Should().BeEmpty();
        _logService.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("Error getting available power plans"))),
            Times.Once);
    }

    [Fact]
    public async Task TryApplySpecialSettingAsync_NonPowerPlanSetting_ReturnsFalse()
    {
        // Arrange
        var setting = MakeSetting("some-other-setting");

        // Act
        var result = await _sut.TryApplySpecialSettingAsync(setting, 0);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DiscoverSpecialSettingsAsync_WithPowerPlanSetting_ReturnsActivePlanInfo()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            MakeSetting("power-plan-selection"),
        };

        var activePlan = new Winhance.Core.Features.Optimize.Models.PowerPlan
        {
            Name = "Balanced",
            Guid = "381b4222-f694-41f0-9685-ff5bb260df2e"
        };

        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(activePlan);

        // Act
        var result = await _sut.DiscoverSpecialSettingsAsync(settings);

        // Assert
        result.Should().ContainKey("power-plan-selection");
        result["power-plan-selection"]["ActivePowerPlan"].Should().Be("Balanced");
        result["power-plan-selection"]["ActivePowerPlanGuid"].Should().Be("381b4222-f694-41f0-9685-ff5bb260df2e");
    }

    [Fact]
    public async Task DiscoverSpecialSettingsAsync_WithoutPowerPlanSetting_ReturnsEmptyDictionary()
    {
        // Arrange
        var settings = new List<SettingDefinition>
        {
            MakeSetting("some-other-setting"),
        };

        // Act
        var result = await _sut.DiscoverSpecialSettingsAsync(settings);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task TryApplySpecialSettingAsync_CorruptWinhancePlan_DeletesGhostAndAttemptsImport()
    {
        // Arrange — ghost plan exists with Winhance GUID but wrong name
        var winhanceGuid = "57696e68-616e-6365-506f-776572000000";
        var setting = MakeSetting(SettingIds.PowerPlanSelection);

        var ghostPlan = new PowerPlan
        {
            Name = "Unknown Power Plan",
            Guid = winhanceGuid,
            IsActive = false
        };

        _powerPlanComboBoxService
            .Setup(s => s.ResolvePowerPlanByIndexAsync(4))
            .ReturnsAsync(new PowerPlanResolutionResult
            {
                Success = true,
                Guid = winhanceGuid,
                DisplayName = "Winhance Power Plan"
            });

        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e" });

        _powerSettingsQueryService
            .Setup(s => s.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan> { ghostPlan });

        _powerSchemeOperations
            .Setup(s => s.DeleteScheme(It.IsAny<Guid>()))
            .Returns(PowerProf.ERROR_SUCCESS);

        // powercfg /duplicatescheme — return output with the actual GUID assigned
        var assignedGuid = "159d8424-9c94-4b24-ada1-b427b29e9b2e";
        _processExecutor
            .Setup(p => p.ExecuteAsync(It.IsAny<string>(), It.IsAny<string>(), default))
            .ReturnsAsync(new ProcessExecutionResult
            {
                ExitCode = 0,
                StandardOutput = $"Power Scheme GUID: {assignedGuid}  (Ultimate Performance)"
            });

        _powerSchemeOperations
            .Setup(s => s.SetActiveScheme(It.IsAny<Guid>()))
            .Returns(PowerProf.ERROR_SUCCESS);

        _powerSchemeOperations
            .Setup(s => s.WriteFriendlyName(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(PowerProf.ERROR_SUCCESS);

        _powerSchemeOperations
            .Setup(s => s.WriteDescription(It.IsAny<Guid>(), It.IsAny<string>()))
            .Returns(PowerProf.ERROR_SUCCESS);

        // Act
        var result = await _sut.TryApplySpecialSettingAsync(setting, 4);

        // Assert — ghost plan should be deleted
        _powerSchemeOperations.Verify(
            s => s.DeleteScheme(Guid.Parse(winhanceGuid)),
            Times.AtLeastOnce);

        // Should log about the corrupt plan
        _logService.Verify(
            l => l.Log(LogLevel.Warning, It.Is<string>(s => s.Contains("corrupt Winhance plan"))),
            Times.Once);

        // Name should be set on the actual GUID powercfg assigned, not the requested one
        _powerSchemeOperations.Verify(
            s => s.WriteFriendlyName(Guid.Parse(assignedGuid), "Winhance Power Plan"),
            Times.Once);
    }

    [Fact]
    public async Task TryApplySpecialSettingAsync_ValidWinhancePlan_DoesNotDelete()
    {
        // Arrange — valid Winhance plan exists with correct name
        var winhanceGuid = "57696e68-616e-6365-506f-776572000000";
        var setting = MakeSetting(SettingIds.PowerPlanSelection);

        var validPlan = new PowerPlan
        {
            Name = "Winhance Power Plan",
            Guid = winhanceGuid,
            IsActive = false
        };

        _powerPlanComboBoxService
            .Setup(s => s.ResolvePowerPlanByIndexAsync(4))
            .ReturnsAsync(new PowerPlanResolutionResult
            {
                Success = true,
                Guid = winhanceGuid,
                DisplayName = "Winhance Power Plan"
            });

        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(new PowerPlan { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e" });

        _powerSettingsQueryService
            .Setup(s => s.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan> { validPlan });

        _powerSchemeOperations
            .Setup(s => s.SetActiveScheme(It.IsAny<Guid>()))
            .Returns(PowerProf.ERROR_SUCCESS);

        // Act
        var result = await _sut.TryApplySpecialSettingAsync(setting, 4);

        // Assert — should NOT delete the valid plan
        _powerSchemeOperations.Verify(
            s => s.DeleteScheme(Guid.Parse(winhanceGuid)),
            Times.Never);

        // Should succeed by just activating the existing plan
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DiscoverSpecialSettingsAsync_CorruptWinhancePlanActive_DeletesGhostAndSwitchesToBalanced()
    {
        // Arrange — ghost Winhance plan is active with wrong name
        var winhanceGuid = "57696e68-616e-6365-506f-776572000000";
        var balancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
        var settings = new List<SettingDefinition> { MakeSetting(SettingIds.PowerPlanSelection) };

        var ghostPlan = new PowerPlan
        {
            Name = "Unknown Power Plan",
            Guid = winhanceGuid,
            IsActive = true
        };

        var balancedPlan = new PowerPlan
        {
            Name = "Balanced",
            Guid = balancedGuid,
            IsActive = false
        };

        _powerSettingsQueryService
            .Setup(s => s.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan> { balancedPlan, ghostPlan });

        // After cleanup, active plan is Balanced
        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(balancedPlan);

        _powerSchemeOperations
            .Setup(s => s.SetActiveScheme(Guid.Parse(balancedGuid)))
            .Returns(PowerProf.ERROR_SUCCESS);

        _powerSchemeOperations
            .Setup(s => s.DeleteScheme(Guid.Parse(winhanceGuid)))
            .Returns(PowerProf.ERROR_SUCCESS);

        // Act
        var result = await _sut.DiscoverSpecialSettingsAsync(settings);

        // Assert — should switch to Balanced before deleting
        _powerSchemeOperations.Verify(
            s => s.SetActiveScheme(Guid.Parse(balancedGuid)),
            Times.Once);

        // Should delete the ghost
        _powerSchemeOperations.Verify(
            s => s.DeleteScheme(Guid.Parse(winhanceGuid)),
            Times.Once);

        // Should invalidate cache
        _powerSettingsQueryService.Verify(
            s => s.InvalidateCache(),
            Times.AtLeastOnce);

        // Result should reflect Balanced as active
        result.Should().ContainKey(SettingIds.PowerPlanSelection);
        result[SettingIds.PowerPlanSelection]["ActivePowerPlan"].Should().Be("Balanced");
    }

    [Fact]
    public async Task DiscoverSpecialSettingsAsync_ValidWinhancePlanActive_DoesNotDelete()
    {
        // Arrange — valid Winhance plan is active
        var winhanceGuid = "57696e68-616e-6365-506f-776572000000";
        var settings = new List<SettingDefinition> { MakeSetting(SettingIds.PowerPlanSelection) };

        var validPlan = new PowerPlan
        {
            Name = "Winhance Power Plan",
            Guid = winhanceGuid,
            IsActive = true
        };

        _powerSettingsQueryService
            .Setup(s => s.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan> { validPlan });

        _powerSettingsQueryService
            .Setup(s => s.GetActivePowerPlanAsync())
            .ReturnsAsync(validPlan);

        // Act
        var result = await _sut.DiscoverSpecialSettingsAsync(settings);

        // Assert — should NOT delete valid plan
        _powerSchemeOperations.Verify(
            s => s.DeleteScheme(It.IsAny<Guid>()),
            Times.Never);

        // Should report Winhance as active
        result[SettingIds.PowerPlanSelection]["ActivePowerPlan"].Should().Be("Winhance Power Plan");
        result[SettingIds.PowerPlanSelection]["ActivePowerPlanGuid"].Should().Be(winhanceGuid);
    }
}
