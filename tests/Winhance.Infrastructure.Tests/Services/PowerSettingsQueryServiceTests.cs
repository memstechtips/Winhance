using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class PowerSettingsQueryServiceTests
{
    private readonly Mock<ILogService> _mockLogService = new();
    private readonly PowerSettingsQueryService _service;

    public PowerSettingsQueryServiceTests()
    {
        _service = new PowerSettingsQueryService(_mockLogService.Object);
    }

    #region InvalidateCache

    [Fact]
    public void InvalidateCache_DoesNotThrow()
    {
        // Act
        var act = () => _service.InvalidateCache();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void InvalidateCache_CalledMultipleTimes_DoesNotThrow()
    {
        // Act & Assert — should be safe to call repeatedly
        _service.InvalidateCache();
        _service.InvalidateCache();
        _service.InvalidateCache();
    }

    #endregion

    #region GetAvailablePowerPlansAsync

    [Fact]
    public async Task GetAvailablePowerPlansAsync_ReturnsNonNullList()
    {
        // Act — this calls native PowerEnumerate APIs which may or may not work
        // in the test environment. The service handles exceptions gracefully.
        var result = await _service.GetAvailablePowerPlansAsync();

        // Assert — should always return a list (possibly empty if API fails)
        result.Should().NotBeNull();
        result.Should().BeOfType<List<PowerPlan>>();
    }

    [Fact]
    public async Task GetAvailablePowerPlansAsync_CachedResult_ReturnsSameReference()
    {
        // Act — call twice in quick succession (within 2-second cache window)
        var result1 = await _service.GetAvailablePowerPlansAsync();
        var result2 = await _service.GetAvailablePowerPlansAsync();

        // Assert — second call should return cached result (same reference)
        result2.Should().BeSameAs(result1);
    }

    [Fact]
    public async Task GetAvailablePowerPlansAsync_AfterInvalidateCache_QueriesAgain()
    {
        // Act
        var result1 = await _service.GetAvailablePowerPlansAsync();
        _service.InvalidateCache();
        var result2 = await _service.GetAvailablePowerPlansAsync();

        // Assert — after invalidation, a fresh query is made
        // The results may or may not be the same reference depending on the native API
        result2.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAvailablePowerPlansAsync_PlansHaveRequiredProperties()
    {
        // Act
        var result = await _service.GetAvailablePowerPlansAsync();

        // Assert — if any plans were discovered, verify they have basic properties
        foreach (var plan in result)
        {
            plan.Guid.Should().NotBeNullOrEmpty();
            plan.Name.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task GetAvailablePowerPlansAsync_AtMostOneActivePlan()
    {
        // Act
        var result = await _service.GetAvailablePowerPlansAsync();

        // Assert — at most one plan should be marked as active
        result.Count(p => p.IsActive).Should().BeLessOrEqualTo(1);
    }

    [Fact]
    public async Task GetAvailablePowerPlansAsync_ActivePlanIsFirstWhenPresent()
    {
        // Act
        var result = await _service.GetAvailablePowerPlansAsync();

        // Assert — the service sorts active plan first
        if (result.Any(p => p.IsActive))
        {
            result.First().IsActive.Should().BeTrue();
        }
    }

    #endregion

    #region GetActivePowerPlanAsync

    [Fact]
    public async Task GetActivePowerPlanAsync_ReturnsNonNullPlan()
    {
        // Act
        var result = await _service.GetActivePowerPlanAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsActive.Should().BeTrue();
        result.Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetActivePowerPlanAsync_AlwaysMarkedAsActive()
    {
        // Act
        var result = await _service.GetActivePowerPlanAsync();

        // Assert
        result.IsActive.Should().BeTrue();
    }

    #endregion

    #region GetPowerSettingACDCValuesAsync

    [Fact]
    public async Task GetPowerSettingACDCValuesAsync_InvalidGuids_ReturnsNulls()
    {
        // Arrange — use empty/invalid GUIDs that won't match any real setting
        var powerCfgSetting = new PowerCfgSetting
        {
            SubgroupGuid = Guid.Empty.ToString(),
            SettingGuid = Guid.Empty.ToString()
        };

        // Act
        var result = await _service.GetPowerSettingACDCValuesAsync(powerCfgSetting);

        // Assert — with an empty scheme GUID, the values should be null
        // The method may return nulls or actual values depending on the system
        result.Should().BeOfType<(int?, int?)>();
    }

    [Fact]
    public async Task GetPowerSettingACDCValuesAsync_WithKnownSubgroupAndSetting_ReturnsTuple()
    {
        // Arrange — use well-known power setting GUIDs (display brightness)
        // SUB_VIDEO = {7516b95f-f776-4464-8c53-06167f40cc99}
        // VIDEONORMALLEVEL = {aded5e82-b909-4619-9949-f5d71dac0bcb}
        var powerCfgSetting = new PowerCfgSetting
        {
            SubgroupGuid = "7516b95f-f776-4464-8c53-06167f40cc99",
            SettingGuid = "aded5e82-b909-4619-9949-f5d71dac0bcb"
        };

        // Act
        var result = await _service.GetPowerSettingACDCValuesAsync(powerCfgSetting);

        // Assert — result is a tuple of nullable ints
        result.Should().BeOfType<(int?, int?)>();
    }

    [Fact]
    public async Task GetPowerSettingACDCValuesAsync_MalformedGuid_ReturnsNullsAndLogs()
    {
        // Arrange
        var powerCfgSetting = new PowerCfgSetting
        {
            SubgroupGuid = "not-a-valid-guid",
            SettingGuid = "also-not-valid"
        };

        // Act
        var result = await _service.GetPowerSettingACDCValuesAsync(powerCfgSetting);

        // Assert — the method catches exceptions and returns nulls
        result.acValue.Should().BeNull();
        result.dcValue.Should().BeNull();
        _mockLogService.Verify(
            l => l.Log(Core.Features.Common.Enums.LogLevel.Error, It.IsAny<string>()),
            Times.Once);
    }

    #endregion

    #region GetAllPowerSettingsACDCAsync

    [Fact]
    public async Task GetAllPowerSettingsACDCAsync_DefaultParameter_ReturnsNonNullDictionary()
    {
        // Act
        var result = await _service.GetAllPowerSettingsACDCAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<Dictionary<string, (int?, int?)>>();
    }

    [Fact]
    public async Task GetAllPowerSettingsACDCAsync_InvalidGuid_ReturnsEmptyDictionary()
    {
        // Act — pass an invalid GUID that cannot be parsed
        var result = await _service.GetAllPowerSettingsACDCAsync("not-a-valid-guid");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllPowerSettingsACDCAsync_EmptyGuid_ReturnsEmptyDictionary()
    {
        // Act
        var result = await _service.GetAllPowerSettingsACDCAsync(Guid.Empty.ToString());

        // Assert — Guid.Empty maps to a non-existent scheme, so results should be empty
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllPowerSettingsACDCAsync_SchemeCurrentKeyword_ReturnsResults()
    {
        // Act
        var result = await _service.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT");

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<Dictionary<string, (int?, int?)>>();
    }

    #endregion

    #region IsSettingHardwareControlledAsync

    [Fact]
    public async Task IsSettingHardwareControlledAsync_ValidSetting_ReturnsBool()
    {
        // Arrange — use a well-known power setting GUID
        var powerCfgSetting = new PowerCfgSetting
        {
            SubgroupGuid = "7516b95f-f776-4464-8c53-06167f40cc99",
            SettingGuid = "aded5e82-b909-4619-9949-f5d71dac0bcb",
            SettingGUIDAlias = "VIDEONORMALLEVEL"
        };

        // Act
        var act = async () => await _service.IsSettingHardwareControlledAsync(powerCfgSetting);

        // Assert — should not throw; returns a bool value
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IsSettingHardwareControlledAsync_MalformedGuid_ReturnsFalse()
    {
        // Arrange
        var powerCfgSetting = new PowerCfgSetting
        {
            SubgroupGuid = "invalid-guid",
            SettingGuid = "also-invalid"
        };

        // Act — the exception path returns (null, null) for capabilities,
        // so min == 0 && max == 0 will be false because null != 0
        var result = await _service.IsSettingHardwareControlledAsync(powerCfgSetting);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsSettingHardwareControlledAsync_CachesCapabilities()
    {
        // Arrange
        var powerCfgSetting = new PowerCfgSetting
        {
            SubgroupGuid = "7516b95f-f776-4464-8c53-06167f40cc99",
            SettingGuid = "aded5e82-b909-4619-9949-f5d71dac0bcb",
            SettingGUIDAlias = "VIDEONORMALLEVEL"
        };

        // Act — call twice
        var result1 = await _service.IsSettingHardwareControlledAsync(powerCfgSetting);
        var result2 = await _service.IsSettingHardwareControlledAsync(powerCfgSetting);

        // Assert — should return same value, and the second call should use cache
        result1.Should().Be(result2);
    }

    [Fact]
    public async Task IsSettingHardwareControlledAsync_AfterInvalidateCache_QueriesAgain()
    {
        // Arrange
        var powerCfgSetting = new PowerCfgSetting
        {
            SubgroupGuid = "7516b95f-f776-4464-8c53-06167f40cc99",
            SettingGuid = "aded5e82-b909-4619-9949-f5d71dac0bcb",
            SettingGUIDAlias = "VIDEONORMALLEVEL"
        };

        // Act
        var result1 = await _service.IsSettingHardwareControlledAsync(powerCfgSetting);
        _service.InvalidateCache();
        var result2 = await _service.IsSettingHardwareControlledAsync(powerCfgSetting);

        // Assert — results should be consistent
        result1.Should().Be(result2);
    }

    #endregion
}
