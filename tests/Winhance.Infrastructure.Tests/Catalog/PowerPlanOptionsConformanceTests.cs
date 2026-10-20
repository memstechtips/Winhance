using Moq;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Catalog;
using Winhance.TestSupport;
using Xunit;

namespace Winhance.Infrastructure.Tests.Catalog;

// The LIVE source of the power-plan options; without this the dropdown's CONTENT would have zero coverage.
// Pinned: every predefined plan is offered even when NOT installed (ExistsOnSystem=false, its canonical GUID);
// an installed plan matches by GUID, else cleaned NAME, else (Ultimate Performance only) the localized-name
// heuristic, and carries the SYSTEM's GUID; custom plans are appended; values lowercased; ordered by Label = dropdown order.
public class PowerPlanOptionsConformanceTests
{
    private const string PowerSaverGuid = "a1841308-3541-4fab-bc81-f71556f20b4a";
    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private const string HighPerfGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string UltimateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    private const string WinhanceGuid = "57696e68-616e-6365-506f-776572000000";

    // An unstubbed mock reports every key missing, so each predefined plan keeps its English catalog name.
    private static ILocalizationService Untranslated() => new Mock<ILocalizationService>().Object;

    private static void AssertOptions(List<PowerPlan> systemPlans, params (string Label, string Value, bool Exists)[] expected)
    {
        var built = PowerPlanOptions.Build(systemPlans, activeGuid: null, Untranslated());

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
        ("Balanced", BalancedGuid, true),
        ("High performance", HighPerfGuid, true),
        ("Power saver", PowerSaverGuid, true),
        ("Ultimate Performance", UltimateGuid, true),
        ("Winhance Power Plan", WinhanceGuid, true));

    [Fact]
    public void Only_some_predefined_installed_others_appear_not_installed() => AssertOptions(
        new List<PowerPlan>
        {
            new() { Name = "Balanced", Guid = BalancedGuid, IsActive = true },
            new() { Name = "High performance", Guid = HighPerfGuid },
        },
        ("Balanced", BalancedGuid, true),
        ("High performance", HighPerfGuid, true),
        ("Power saver", PowerSaverGuid, false),
        ("Ultimate Performance", UltimateGuid, false),
        ("Winhance Power Plan", WinhanceGuid, false));

    [Fact]
    public void Custom_plan_appears_as_an_unmatched_system_plan() => AssertOptions(
        new List<PowerPlan>
        {
            new() { Name = "Balanced", Guid = BalancedGuid, IsActive = true },
            // UPPERCASE on purpose: pins Build()'s .ToLowerInvariant() (expected value is lowercase).
            new() { Name = "My Custom Gaming Plan", Guid = "AAAAAAAA-1111-2222-3333-444444444444" },
        },
        // The custom plan sorts among the predefined names ('H' < 'M' < 'P') -- Label order IS dropdown order.
        ("Balanced", BalancedGuid, true),
        ("High performance", HighPerfGuid, false),
        ("My Custom Gaming Plan", "aaaaaaaa-1111-2222-3333-444444444444", true),
        ("Power saver", PowerSaverGuid, false),
        ("Ultimate Performance", UltimateGuid, false),
        ("Winhance Power Plan", WinhanceGuid, false));

    [Fact]
    public void Predefined_matched_by_name_when_guid_differs() => AssertOptions(
        new List<PowerPlan>
        {
            // A "Balanced" plan with a non-canonical GUID still matches the predefined by cleaned name,
            // and the option then carries the SYSTEM's GUID (not the canonical one).
            new() { Name = "Balanced", Guid = "deadbeef-0000-0000-0000-000000000000", IsActive = true },
        },
        ("Balanced", "deadbeef-0000-0000-0000-000000000000", true),
        ("High performance", HighPerfGuid, false),
        ("Power saver", PowerSaverGuid, false),
        ("Ultimate Performance", UltimateGuid, false),
        ("Winhance Power Plan", WinhanceGuid, false));

    [Fact]
    public void Ultimate_performance_matched_by_heuristic_when_guid_differs() => AssertOptions(
        new List<PowerPlan>
        {
            new() { Name = "Ultimate Performance", Guid = "11112222-3333-4444-5555-666677778888", IsActive = true },
        },
        ("Balanced", BalancedGuid, false),
        ("High performance", HighPerfGuid, false),
        ("Power saver", PowerSaverGuid, false),
        ("Ultimate Performance", "11112222-3333-4444-5555-666677778888", true),
        ("Winhance Power Plan", WinhanceGuid, false));

    [Fact]
    public void No_plans_installed_all_predefined_appear_not_installed() => AssertOptions(
        new List<PowerPlan>(),
        ("Balanced", BalancedGuid, false),
        ("High performance", HighPerfGuid, false),
        ("Power saver", PowerSaverGuid, false),
        ("Ultimate Performance", UltimateGuid, false),
        ("Winhance Power Plan", WinhanceGuid, false));

    [Fact]
    public void A_translated_predefined_plan_carries_the_translation()
    {
        var loc = new Mock<ILocalizationService>().PresentKey("PowerPlan_Balanced_Name", "Ausgeglichen").Object;

        var built = PowerPlanOptions.Build(
            new List<PowerPlan> { new() { Name = "Balanced", Guid = BalancedGuid } }, activeGuid: null, loc);

        Assert.Contains(built, o => o.Label == "Ausgeglichen" && o.Value == BalancedGuid);
    }

    // Windows refuses to delete the scheme it is running on.
    [Fact]
    public void Only_an_installed_plan_that_is_not_active_can_be_deleted()
    {
        var built = PowerPlanOptions.Build(
            new List<PowerPlan>
            {
                new() { Name = "Balanced", Guid = BalancedGuid, IsActive = true },
                new() { Name = "High performance", Guid = HighPerfGuid },
            },
            BalancedGuid,
            Untranslated());

        Assert.False(built.Single(o => o.Value == BalancedGuid).CanDelete);
        Assert.True(built.Single(o => o.Value == HighPerfGuid).CanDelete);
        Assert.False(built.Single(o => o.Value == WinhanceGuid).CanDelete);
    }
}
