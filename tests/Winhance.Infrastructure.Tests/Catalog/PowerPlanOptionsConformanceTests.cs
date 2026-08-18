using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Catalog;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

/// <summary>Machine-independent conformance for the power-plan dropdown built by
/// <see cref="PowerPlanOptions.Build"/> -- the LIVE source of the power-plan options
/// (SystemDetectionContext -> PowerPlanOptionSource -> the UI dropdown). Without this file the dropdown's
/// CONTENT would have zero coverage (SystemDetectionContextTests only asserts the context DELEGATES to Build).
///
/// The pinned contract: every predefined plan is offered even when NOT installed (ExistsOnSystem=false,
/// valued by its canonical GUID -- selecting it imports/creates the plan); an installed plan is matched by
/// GUID, else by cleaned NAME, else (Ultimate Performance only) by the localized-name heuristic, and then
/// carries the SYSTEM's GUID; unmatched system plans (the user's custom plans) are appended; every value is
/// lowercased; the list is ordered by Label, and that order IS the dropdown order.</summary>
public class PowerPlanOptionsConformanceTests
{
    private const string PowerSaverGuid = "a1841308-3541-4fab-bc81-f71556f20b4a";
    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string HighPerfGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string UltimateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    private const string WinhanceGuid = "57696e68-616e-6365-506f-776572000000";

    private static void AssertOptions(List<PowerPlan> systemPlans, params (string Label, string Value, bool Exists)[] expected)
    {
        var built = PowerPlanOptions.Build(systemPlans);

        Assert.Equal(expected.Length, built.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i].Label, built[i].Label);
            Assert.Equal(expected[i].Value, built[i].Value);
            Assert.Equal(expected[i].Exists, built[i].ExistsOnSystem);
        }
    }

    [Fact]
    public void All_predefined_plans_installed() => AssertOptions(
        new List<PowerPlan>
        {
            new() { Name = "Power saver", Guid = PowerSaverGuid },
            new() { Name = "Balanced", Guid = BalancedGuid, IsActive = true },
            new() { Name = "High performance", Guid = HighPerfGuid },
            new() { Name = "Ultimate Performance", Guid = UltimateGuid },
            new() { Name = "Winhance Power Plan", Guid = WinhanceGuid },
        },
        ("PowerPlan_Balanced_Name", BalancedGuid, true),
        ("PowerPlan_HighPerformance_Name", HighPerfGuid, true),
        ("PowerPlan_PowerSaver_Name", PowerSaverGuid, true),
        ("PowerPlan_UltimatePerformance_Name", UltimateGuid, true),
        ("PowerPlan_WinhancePowerPlan_Name", WinhanceGuid, true));

    [Fact]
    public void Only_some_predefined_installed_others_appear_not_installed() => AssertOptions(
        new List<PowerPlan>
        {
            new() { Name = "Balanced", Guid = BalancedGuid, IsActive = true },
            new() { Name = "High performance", Guid = HighPerfGuid },
        },
        ("PowerPlan_Balanced_Name", BalancedGuid, true),
        ("PowerPlan_HighPerformance_Name", HighPerfGuid, true),
        ("PowerPlan_PowerSaver_Name", PowerSaverGuid, false),
        ("PowerPlan_UltimatePerformance_Name", UltimateGuid, false),
        ("PowerPlan_WinhancePowerPlan_Name", WinhanceGuid, false));

    [Fact]
    public void Custom_plan_appears_as_an_unmatched_system_plan() => AssertOptions(
        new List<PowerPlan>
        {
            new() { Name = "Balanced", Guid = BalancedGuid, IsActive = true },
            // UPPERCASE on purpose: pins Build()'s .ToLowerInvariant() (expected value is lowercase).
            new() { Name = "My Custom Gaming Plan", Guid = "AAAAAAAA-1111-2222-3333-444444444444" },
        },
        // The custom plan sorts before the PowerPlan_* loc keys ('M' < 'P') -- Label order IS dropdown order.
        ("My Custom Gaming Plan", "aaaaaaaa-1111-2222-3333-444444444444", true),
        ("PowerPlan_Balanced_Name", BalancedGuid, true),
        ("PowerPlan_HighPerformance_Name", HighPerfGuid, false),
        ("PowerPlan_PowerSaver_Name", PowerSaverGuid, false),
        ("PowerPlan_UltimatePerformance_Name", UltimateGuid, false),
        ("PowerPlan_WinhancePowerPlan_Name", WinhanceGuid, false));

    [Fact]
    public void Predefined_matched_by_name_when_guid_differs() => AssertOptions(
        new List<PowerPlan>
        {
            // A "Balanced" plan with a non-canonical GUID still matches the predefined by cleaned name,
            // and the option then carries the SYSTEM's GUID (not the canonical one).
            new() { Name = "Balanced", Guid = "deadbeef-0000-0000-0000-000000000000", IsActive = true },
        },
        ("PowerPlan_Balanced_Name", "deadbeef-0000-0000-0000-000000000000", true),
        ("PowerPlan_HighPerformance_Name", HighPerfGuid, false),
        ("PowerPlan_PowerSaver_Name", PowerSaverGuid, false),
        ("PowerPlan_UltimatePerformance_Name", UltimateGuid, false),
        ("PowerPlan_WinhancePowerPlan_Name", WinhanceGuid, false));

    [Fact]
    public void Ultimate_performance_matched_by_heuristic_when_guid_differs() => AssertOptions(
        new List<PowerPlan>
        {
            new() { Name = "Ultimate Performance", Guid = "11112222-3333-4444-5555-666677778888", IsActive = true },
        },
        ("PowerPlan_Balanced_Name", BalancedGuid, false),
        ("PowerPlan_HighPerformance_Name", HighPerfGuid, false),
        ("PowerPlan_PowerSaver_Name", PowerSaverGuid, false),
        ("PowerPlan_UltimatePerformance_Name", "11112222-3333-4444-5555-666677778888", true),
        ("PowerPlan_WinhancePowerPlan_Name", WinhanceGuid, false));

    [Fact]
    public void No_plans_installed_all_predefined_appear_not_installed() => AssertOptions(
        new List<PowerPlan>(),
        ("PowerPlan_Balanced_Name", BalancedGuid, false),
        ("PowerPlan_HighPerformance_Name", HighPerfGuid, false),
        ("PowerPlan_PowerSaver_Name", PowerSaverGuid, false),
        ("PowerPlan_UltimatePerformance_Name", UltimateGuid, false),
        ("PowerPlan_WinhancePowerPlan_Name", WinhanceGuid, false));
}
