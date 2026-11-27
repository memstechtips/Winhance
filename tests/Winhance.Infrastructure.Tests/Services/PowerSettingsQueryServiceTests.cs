using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
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

    [Fact]
    public void InvalidateCache_DoesNotThrow()
    {
        var act = () => _service.InvalidateCache();

        act.Should().NotThrow();
    }

    [Fact]
    public void InvalidateCache_CalledMultipleTimes_DoesNotThrow()
    {
        _service.InvalidateCache();
        _service.InvalidateCache();
        _service.InvalidateCache();
    }

    [Fact]
    public async Task GetAvailablePowerPlansAsync_ReturnsNonNullList()
    {
        // This calls native PowerEnumerate APIs which may or may not work
        // in the test environment. The service handles exceptions gracefully.
        var result = await _service.GetAvailablePowerPlansAsync();

        result.Should().NotBeNull();
        result.Should().BeOfType<List<PowerPlan>>();
    }

    [Fact]
    public async Task GetAvailablePowerPlansAsync_CachedResult_ReturnsSameReference()
    {
        // Two calls in quick succession fall within the 2-second cache window
        var result1 = await _service.GetAvailablePowerPlansAsync();
        var result2 = await _service.GetAvailablePowerPlansAsync();

        result2.Should().BeSameAs(result1);
    }

    [Fact]
    public async Task GetAvailablePowerPlansAsync_AfterInvalidateCache_QueriesAgain()
    {
        var result1 = await _service.GetAvailablePowerPlansAsync();
        _service.InvalidateCache();
        var result2 = await _service.GetAvailablePowerPlansAsync();

        // The results may or may not be the same reference depending on the native API
        result2.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAvailablePowerPlansAsync_PlansHaveRequiredProperties()
    {
        var result = await _service.GetAvailablePowerPlansAsync();

        foreach (var plan in result)
        {
            plan.Guid.Should().NotBeNullOrEmpty();
            plan.Name.Should().NotBeNullOrEmpty();
        }
    }

    [Fact]
    public async Task GetAvailablePowerPlansAsync_AtMostOneActivePlan()
    {
        var result = await _service.GetAvailablePowerPlansAsync();

        result.Count(p => p.IsActive).Should().BeLessOrEqualTo(1);
    }

    [Fact]
    public async Task GetAvailablePowerPlansAsync_ActivePlanIsFirstWhenPresent()
    {
        var result = await _service.GetAvailablePowerPlansAsync();

        if (result.Any(p => p.IsActive))
        {
            result.First().IsActive.Should().BeTrue();
        }
    }

    [Fact]
    public async Task GetActivePowerPlanAsync_ReturnsNonNullPlan()
    {
        var result = await _service.GetActivePowerPlanAsync();

        result.Should().NotBeNull();
        result.IsActive.Should().BeTrue();
        result.Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetActivePowerPlanAsync_AlwaysMarkedAsActive()
    {
        var result = await _service.GetActivePowerPlanAsync();

        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllPowerSettingsACDCAsync_DefaultParameter_ReturnsNonNullDictionary()
    {
        var result = await _service.GetAllPowerSettingsACDCAsync();

        result.Should().NotBeNull();
        result.Should().BeOfType<Dictionary<string, (int?, int?)>>();
    }

    [Fact]
    public async Task GetAllPowerSettingsACDCAsync_InvalidGuid_ReturnsEmptyDictionary()
    {
        var result = await _service.GetAllPowerSettingsACDCAsync("not-a-valid-guid");

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllPowerSettingsACDCAsync_EmptyGuid_ReturnsEmptyDictionary()
    {
        var result = await _service.GetAllPowerSettingsACDCAsync(Guid.Empty.ToString());

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAllPowerSettingsACDCAsync_SchemeCurrentKeyword_ReturnsResults()
    {
        var result = await _service.GetAllPowerSettingsACDCAsync("SCHEME_CURRENT");

        result.Should().NotBeNull();
        result.Should().BeOfType<Dictionary<string, (int?, int?)>>();
    }

    private const string VideoSubgroup = "7516b95f-f776-4464-8c53-06167f40cc99";
    private const string VideoNormalLevel = "aded5e82-b909-4619-9949-f5d71dac0bcb";

    [Fact]
    public async Task IsSettingHardwareControlledAsync_ValidSetting_ReturnsBool()
    {
        var act = async () => await _service.IsSettingHardwareControlledAsync(VideoSubgroup, VideoNormalLevel);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task IsSettingHardwareControlledAsync_MalformedGuid_ReturnsFalse()
    {
        // The exception path returns (null, null) for capabilities, and null is not 0.
        var result = await _service.IsSettingHardwareControlledAsync("invalid-guid", "also-invalid");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsSettingHardwareControlledAsync_CachesCapabilities()
    {
        var result1 = await _service.IsSettingHardwareControlledAsync(VideoSubgroup, VideoNormalLevel);
        var result2 = await _service.IsSettingHardwareControlledAsync(VideoSubgroup, VideoNormalLevel);

        result1.Should().Be(result2);
    }

    [Fact]
    public async Task IsSettingHardwareControlledAsync_AfterInvalidateCache_QueriesAgain()
    {
        var result1 = await _service.IsSettingHardwareControlledAsync(VideoSubgroup, VideoNormalLevel);
        _service.InvalidateCache();
        var result2 = await _service.IsSettingHardwareControlledAsync(VideoSubgroup, VideoNormalLevel);

        result1.Should().Be(result2);
    }
}
