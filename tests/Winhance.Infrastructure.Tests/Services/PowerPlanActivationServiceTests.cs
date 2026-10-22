using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Optimize.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Services;

public class PowerPlanActivationServiceTests
{
    private readonly Mock<IPowerSettingsQueryService> _query = new();
    private readonly Mock<IPowerSchemeOperations> _schemes = new();

    private PowerPlanActivationService CreateSut() => new(
        new Mock<ILogService>().Object,
        _query.Object,
        _schemes.Object,
        new Mock<IProcessExecutor>().Object,
        new Mock<IPowerCfgApplier>().Object,
        new Mock<IFileSystemService>().Object,
        new Mock<ICatalogSettingsRegistry>().Object);

    [Fact]
    public async Task The_Winhance_plan_is_a_copy_of_Balanced()
    {
        _query.Setup(q => q.GetAvailablePowerPlansAsync()).ReturnsAsync(new List<PowerPlan>());
        var created = Guid.Empty;
        _schemes.Setup(s => s.DuplicateScheme(It.IsAny<Guid>(), It.IsAny<Guid?>(), out created)).Returns(1u);
        var winhancePlan = PowerPlanCatalog.BuiltInPowerPlans.Single(p => p.Guid == PowerPlanCatalog.WinhancePowerPlanGuid);

        await CreateSut().ImportPowerPlanAsync(winhancePlan);

        _schemes.Verify(s => s.DuplicateScheme(
            Guid.Parse(PowerPlanCatalog.BalancedGuid), Guid.Parse(PowerPlanCatalog.WinhancePowerPlanGuid), out created), Times.Once);
        _schemes.Verify(s => s.DuplicateScheme(
            It.Is<Guid>(g => g != Guid.Parse(PowerPlanCatalog.BalancedGuid)), It.IsAny<Guid?>(), out created), Times.Never);
    }
}
