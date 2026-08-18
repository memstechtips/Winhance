using FluentAssertions;
using Moq;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Native;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Optimize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

/// <summary>
/// PowerService's live surface is the corrupt-plan cleanup (CleanupCorruptWinhancePlanAsync) plus the
/// GetActive/GetAvailable/Delete plan queries; power-plan apply runs through the catalog engine, not here.
/// TryApplySpecialSettingAsync is a dead stub.
/// </summary>
public class PowerServiceTests
{
    private readonly Mock<ILogService> _logService;
    private readonly Mock<IPowerSettingsQueryService> _powerSettingsQueryService;
    private readonly Mock<IPowerSchemeOperations> _powerSchemeOperations;
    private readonly PowerService _sut;

    public PowerServiceTests()
    {
        _logService = new Mock<ILogService>();
        _powerSettingsQueryService = new Mock<IPowerSettingsQueryService>();
        _powerSchemeOperations = new Mock<IPowerSchemeOperations>();

        _sut = new PowerService(
            _logService.Object,
            _powerSettingsQueryService.Object,
            _powerSchemeOperations.Object);
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
    public async Task TryApplySpecialSettingAsync_IsADeadStub_AlwaysReturnsFalse()
    {
        // PowerService is not an apply handler (power-plan apply runs through the catalog engine); the interface
        // method remains only because PowerService is still a discovery handler, and it handles nothing.

        var result = await _sut.TryApplySpecialSettingAsync(SettingIds.PowerPlanSelection, 0);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CleanupCorruptWinhancePlanAsync_CorruptWinhancePlanActive_DeletesGhostAndSwitchesToBalanced()
    {
        // Arrange — ghost Winhance plan is active with wrong name
        var winhanceGuid = "57696e68-616e-6365-506f-776572000000";
        var balancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

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
        await _sut.CleanupCorruptWinhancePlanAsync();

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
    }

    [Fact]
    public async Task CleanupCorruptWinhancePlanAsync_ValidWinhancePlanActive_DoesNotDelete()
    {
        // Arrange — valid Winhance plan is active
        var winhanceGuid = "57696e68-616e-6365-506f-776572000000";

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
        await _sut.CleanupCorruptWinhancePlanAsync();

        // Assert — should NOT delete valid plan
        _powerSchemeOperations.Verify(
            s => s.DeleteScheme(It.IsAny<Guid>()),
            Times.Never);
    }
}
