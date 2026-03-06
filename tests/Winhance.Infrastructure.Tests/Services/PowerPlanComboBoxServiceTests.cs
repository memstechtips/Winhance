using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class PowerPlanComboBoxServiceTests
{
    private readonly Mock<IPowerSettingsQueryService> _mockQueryService = new();
    private readonly Mock<ILogService> _mockLog = new();
    private readonly PowerPlanComboBoxService _sut;

    public PowerPlanComboBoxServiceTests()
    {
        _sut = new PowerPlanComboBoxService(
            _mockQueryService.Object,
            _mockLog.Object);
    }

    private static SettingDefinition CreateSetting(string id = "power-plan") => new()
    {
        Id = id,
        Name = "Power Plan",
        Description = "Select a power plan"
    };

    // ── SetupPowerPlanComboBoxAsync ──

    [Fact]
    public async Task SetupPowerPlanComboBoxAsync_WithSystemPlans_ReturnsSuccessWithOptions()
    {
        var systemPlans = new List<PowerPlan>
        {
            new() { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e", IsActive = true },
            new() { Name = "High performance", Guid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", IsActive = false }
        };
        _mockQueryService
            .Setup(q => q.GetAvailablePowerPlansAsync())
            .ReturnsAsync(systemPlans);
        _mockQueryService
            .Setup(q => q.GetActivePowerPlanAsync())
            .ReturnsAsync(systemPlans[0]);

        var setting = CreateSetting();
        var result = await _sut.SetupPowerPlanComboBoxAsync(setting, null);

        result.Success.Should().BeTrue();
        result.Options.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SetupPowerPlanComboBoxAsync_WhenExceptionThrown_ReturnsFailureResult()
    {
        _mockQueryService
            .Setup(q => q.GetAvailablePowerPlansAsync())
            .ThrowsAsync(new InvalidOperationException("Query failed"));

        var result = await _sut.SetupPowerPlanComboBoxAsync(CreateSetting(), null);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Query failed");
    }

    // ── GetPowerPlanOptionsAsync ──

    [Fact]
    public async Task GetPowerPlanOptionsAsync_MatchesPredefinedAndSystemPlans()
    {
        var systemPlans = new List<PowerPlan>
        {
            new() { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e", IsActive = true }
        };
        _mockQueryService
            .Setup(q => q.GetAvailablePowerPlansAsync())
            .ReturnsAsync(systemPlans);

        var options = await _sut.GetPowerPlanOptionsAsync();

        // Should include predefined plans + the matched system plan
        options.Should().NotBeEmpty();
        // Balanced should be matched and marked as existing on system
        var balanced = options.FirstOrDefault(o => o.SystemPlan?.Guid == "381b4222-f694-41f0-9685-ff5bb260df2e");
        balanced.Should().NotBeNull();
        balanced!.ExistsOnSystem.Should().BeTrue();
        balanced.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetPowerPlanOptionsAsync_UnmatchedSystemPlan_IsIncluded()
    {
        var customGuid = "00000000-0000-0000-0000-111111111111";
        var systemPlans = new List<PowerPlan>
        {
            new() { Name = "Custom Plan", Guid = customGuid, IsActive = false }
        };
        _mockQueryService
            .Setup(q => q.GetAvailablePowerPlansAsync())
            .ReturnsAsync(systemPlans);

        var options = await _sut.GetPowerPlanOptionsAsync();

        var customOption = options.FirstOrDefault(o => o.SystemPlan?.Guid == customGuid);
        customOption.Should().NotBeNull();
        customOption!.DisplayName.Should().Be("Custom Plan");
        customOption.PredefinedPlan.Should().BeNull();
    }

    [Fact]
    public async Task GetPowerPlanOptionsAsync_OptionsAreSortedByDisplayName()
    {
        var systemPlans = new List<PowerPlan>
        {
            new() { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e", IsActive = false },
            new() { Name = "High performance", Guid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", IsActive = false }
        };
        _mockQueryService
            .Setup(q => q.GetAvailablePowerPlansAsync())
            .ReturnsAsync(systemPlans);

        var options = await _sut.GetPowerPlanOptionsAsync();

        var displayNames = options.Select(o => o.DisplayName).ToList();
        displayNames.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetPowerPlanOptionsAsync_IndexesAreSequential()
    {
        var systemPlans = new List<PowerPlan>
        {
            new() { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e", IsActive = false }
        };
        _mockQueryService
            .Setup(q => q.GetAvailablePowerPlansAsync())
            .ReturnsAsync(systemPlans);

        var options = await _sut.GetPowerPlanOptionsAsync();

        for (int i = 0; i < options.Count; i++)
        {
            options[i].Index.Should().Be(i);
        }
    }

    // ── ResolvePowerPlanByIndexAsync ──

    [Fact]
    public async Task ResolvePowerPlanByIndexAsync_ValidIndex_ReturnsSuccessWithGuid()
    {
        var balancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
        var systemPlans = new List<PowerPlan>
        {
            new() { Name = "Balanced", Guid = balancedGuid, IsActive = true }
        };
        _mockQueryService
            .Setup(q => q.GetAvailablePowerPlansAsync())
            .ReturnsAsync(systemPlans);

        var options = await _sut.GetPowerPlanOptionsAsync();
        var balancedOption = options.First(o => o.SystemPlan?.Guid == balancedGuid);

        var result = await _sut.ResolvePowerPlanByIndexAsync(balancedOption.Index);

        result.Success.Should().BeTrue();
        result.Guid.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ResolvePowerPlanByIndexAsync_NegativeIndex_ReturnsFailure()
    {
        _mockQueryService
            .Setup(q => q.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan>
            {
                new() { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e", IsActive = true }
            });

        var result = await _sut.ResolvePowerPlanByIndexAsync(-1);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Invalid power plan index");
    }

    [Fact]
    public async Task ResolvePowerPlanByIndexAsync_IndexOutOfRange_ReturnsFailure()
    {
        _mockQueryService
            .Setup(q => q.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan>());

        var result = await _sut.ResolvePowerPlanByIndexAsync(999);

        result.Success.Should().BeFalse();
    }

    // ── ResolveIndexFromRawValuesAsync ──

    [Fact]
    public async Task ResolveIndexFromRawValuesAsync_MatchByGuid_ReturnsCorrectIndex()
    {
        var balancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
        _mockQueryService
            .Setup(q => q.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan>
            {
                new() { Name = "Balanced", Guid = balancedGuid, IsActive = true }
            });

        var rawValues = new Dictionary<string, object?>
        {
            { "ActivePowerPlanGuid", balancedGuid }
        };

        var options = await _sut.GetPowerPlanOptionsAsync();
        var expectedIndex = options.First(o => o.SystemPlan?.Guid == balancedGuid).Index;

        var result = await _sut.ResolveIndexFromRawValuesAsync(CreateSetting(), rawValues);

        result.Should().Be(expectedIndex);
    }

    [Fact]
    public async Task ResolveIndexFromRawValuesAsync_NoMatch_ReturnsZero()
    {
        _mockQueryService
            .Setup(q => q.GetAvailablePowerPlansAsync())
            .ReturnsAsync(new List<PowerPlan>
            {
                new() { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e", IsActive = true }
            });

        var rawValues = new Dictionary<string, object?>
        {
            { "ActivePowerPlanGuid", "non-existent-guid" }
        };

        var result = await _sut.ResolveIndexFromRawValuesAsync(CreateSetting(), rawValues);

        result.Should().Be(0);
    }

    [Fact]
    public async Task ResolveIndexFromRawValuesAsync_WhenExceptionThrown_ReturnsZero()
    {
        _mockQueryService
            .Setup(q => q.GetAvailablePowerPlansAsync())
            .ThrowsAsync(new InvalidOperationException("Boom"));

        var result = await _sut.ResolveIndexFromRawValuesAsync(
            CreateSetting(),
            new Dictionary<string, object?>());

        result.Should().Be(0);
    }

    // ── InvalidateCache ──

    [Fact]
    public void InvalidateCache_DelegatesToQueryService()
    {
        _sut.InvalidateCache();

        _mockQueryService.Verify(q => q.InvalidateCache(), Times.Once);
    }
}
