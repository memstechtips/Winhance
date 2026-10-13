using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog;

// Catalog reference data and the spine of the power-plan dropdown: PowerPlanOptions.Build joins these against the
// installed schemes, so an offered-but-not-installed plan still appears and is created on selection.
public static class PowerPlanCatalog
{
    public static readonly List<PredefinedPowerPlan> BuiltInPowerPlans = new List<PredefinedPowerPlan>
    {
        new("Power saver", "Saves energy by reducing computer performance", "PowerPlan_PowerSaver_Name", "a1841308-3541-4fab-bc81-f71556f20b4a"),
        new("Balanced", "Balances performance with energy consumption", "PowerPlan_Balanced_Name", BalancedGuid),
        new("High performance", "Favors performance over energy consumption", "PowerPlan_HighPerformance_Name", "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"),
        new("Ultimate Performance", "Maximum performance with no power saving measures", "PowerPlan_UltimatePerformance_Name", "e9a42b02-d5df-448d-aa00-03f14749eb61"),
        new("Winhance Power Plan", "Optimized power plan for gaming and performance", "PowerPlan_WinhancePowerPlan_Name", "57696e68-616e-6365-506f-776572000000")
    };

    // Every plan Winhance creates, its own and a custom one, in the app and in an answer file, is a copy of Balanced:
    // the one scheme every Windows install has, where Ultimate Performance can be hidden or blocked. AMD's X3D
    // processors are also reported to steer games to their V-Cache cores only from Balanced or a copy of it.
    public const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";

    // The copy of Balanced that Winhance creates and fills with its Recommended power values.
    public const string WinhancePowerPlanGuid = "57696e68-616e-6365-506f-776572000000";

    public static bool IsWinhancePowerPlan(string? guid) =>
        string.Equals(guid, WinhancePowerPlanGuid, System.StringComparison.OrdinalIgnoreCase);

    // One locale (fa) translates around the brand rather than keeping the whole English name, so the brand alone
    // is what is matched.
    public static bool HasWinhanceName(string? name) =>
        name?.Contains("Winhance", System.StringComparison.OrdinalIgnoreCase) == true;
}
