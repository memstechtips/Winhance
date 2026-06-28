using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Catalog;
using Winhance.Infrastructure.Features.Common.Services;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>Proves the new <see cref="PowerPlanOptions.Build"/> reproduces the OLD power-plan dropdown list exactly:
/// for the same installed system plans, the option order, labels, install-status and resolved GUIDs match
/// <c>PowerPlanComboBoxService.GetPowerPlanOptionsAsync</c> element-for-element. The old option's stored value is its
/// <c>SystemPlan?.Guid ?? PredefinedPlan?.Guid</c> (the same resolution the old apply path used), lowercased to match
/// the new GUID-valued model. This is the adversarial old-model gate for the 7b-ui-1 port.</summary>
public class PowerPlanOptionsEquivalenceTests
{
    private static async Task AssertEquivalent(List<PowerPlan> systemPlans)
    {
        var query = new Mock<IPowerSettingsQueryService>();
        query.Setup(q => q.GetAvailablePowerPlansAsync()).ReturnsAsync(systemPlans);
        var old = await new PowerPlanComboBoxService(query.Object, new Mock<ILogService>().Object)
            .GetPowerPlanOptionsAsync();

        var built = PowerPlanOptions.Build(systemPlans);

        Assert.Equal(old.Count, built.Count);
        for (int i = 0; i < old.Count; i++)
        {
            Assert.Equal(old[i].DisplayName, built[i].Label);
            var oldGuid = (old[i].SystemPlan?.Guid ?? old[i].PredefinedPlan?.Guid ?? string.Empty).ToLowerInvariant();
            Assert.Equal(oldGuid, built[i].Value);
            Assert.Equal(old[i].ExistsOnSystem, built[i].ExistsOnSystem);
        }
    }

    [Fact]
    public Task All_predefined_plans_installed() => AssertEquivalent(new List<PowerPlan>
    {
        new() { Name = "Power saver", Guid = "a1841308-3541-4fab-bc81-f71556f20b4a" },
        new() { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e", IsActive = true },
        new() { Name = "High performance", Guid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" },
        new() { Name = "Ultimate Performance", Guid = "e9a42b02-d5df-448d-aa00-03f14749eb61" },
        new() { Name = "Winhance Power Plan", Guid = "57696e68-616e-6365-506f-776572000000" },
    });

    [Fact]
    public Task Only_some_predefined_installed_others_appear_not_installed() => AssertEquivalent(new List<PowerPlan>
    {
        new() { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e", IsActive = true },
        new() { Name = "High performance", Guid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c" },
    });

    [Fact]
    public Task Custom_plan_appears_as_an_unmatched_system_plan() => AssertEquivalent(new List<PowerPlan>
    {
        new() { Name = "Balanced", Guid = "381b4222-f694-41f0-9685-ff5bb260df2e", IsActive = true },
        new() { Name = "My Custom Gaming Plan", Guid = "aaaaaaaa-1111-2222-3333-444444444444" },
    });

    [Fact]
    public Task Predefined_matched_by_name_when_guid_differs() => AssertEquivalent(new List<PowerPlan>
    {
        // A "Balanced" plan with a non-canonical GUID still matches the predefined by cleaned name.
        new() { Name = "Balanced", Guid = "deadbeef-0000-0000-0000-000000000000", IsActive = true },
    });

    [Fact]
    public Task Ultimate_performance_matched_by_heuristic_when_guid_differs() => AssertEquivalent(new List<PowerPlan>
    {
        new() { Name = "Ultimate Performance", Guid = "11112222-3333-4444-5555-666677778888", IsActive = true },
    });

    [Fact]
    public Task No_plans_installed_all_predefined_appear_not_installed() => AssertEquivalent(new List<PowerPlan>());
}
