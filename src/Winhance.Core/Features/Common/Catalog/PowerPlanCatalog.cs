using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Catalog;

/// <summary>The predefined Windows power plans Winhance offers: name (used for OS name-matching), the
/// PowerPlan_* localization key shown in the dropdown, and the canonical scheme GUID -- plus the fixed
/// Winhance Power Plan GUID and its identity check.
///
/// This is CATALOG REFERENCE DATA: the engine consumes it as the SPINE of the power-plan dropdown
/// (PowerPlanOptions.Build joins these against the machine's installed schemes, fed by PowerPlanOptionSource
/// via SystemDetectionContext, so an offered-but-not-installed plan still appears and is created on selection).
///
/// (Named PowerPlanCatalog, not PowerPlanDefinitions: Common/Models/PowerPlanDefinitions.cs already holds the
/// PredefinedPowerPlan / PowerPlanComboBoxOption records, and this sits beside SettingCatalog.)</summary>
public static class PowerPlanCatalog
{
    public static readonly List<PredefinedPowerPlan> BuiltInPowerPlans = new List<PredefinedPowerPlan>
    {
        new("Power saver", "Saves energy by reducing computer performance", "PowerPlan_PowerSaver_Name", "a1841308-3541-4fab-bc81-f71556f20b4a"),
        new("Balanced", "Balances performance with energy consumption", "PowerPlan_Balanced_Name", "381b4222-f694-41f0-9685-ff5bb260df2e"),
        new("High performance", "Favors performance over energy consumption", "PowerPlan_HighPerformance_Name", "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"),
        new("Ultimate Performance", "Maximum performance with no power saving measures", "PowerPlan_UltimatePerformance_Name", "e9a42b02-d5df-448d-aa00-03f14749eb61"),
        new("Winhance Power Plan", "Optimized power plan for gaming and performance", "PowerPlan_WinhancePowerPlan_Name", "57696e68-616e-6365-506f-776572000000")
    };

    /// <summary>The fixed GUID of the Winhance Power Plan (the duplicate-from-Ultimate-Performance plan Winhance creates).</summary>
    public const string WinhancePowerPlanGuid = "57696e68-616e-6365-506f-776572000000";

    /// <summary>True when the GUID or friendly name identifies the Winhance Power Plan. Same check as PowerService's
    /// private IsWinhancePowerPlan; lifted to a shared helper so the apply funnel can gate the recommended-power
    /// re-apply after a power-plan activation without duplicating the magic GUID.</summary>
    public static bool IsWinhancePowerPlan(string? guid, string? name = null) =>
        string.Equals(guid, WinhancePowerPlanGuid, System.StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name?.Trim(), "Winhance Power Plan", System.StringComparison.OrdinalIgnoreCase);
}
