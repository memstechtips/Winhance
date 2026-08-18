using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.Common.Catalog;

/// <summary>Builds the power-plan dropdown's option list in the new GUID-valued <see cref="DynamicOption"/> shape.
/// A faithful port of <c>PowerPlanComboBoxService.GetPowerPlanOptionsAsync</c> (the old index-valued path), so the
/// new model reproduces the same dropdown: every predefined plan appears (matched to a system plan by GUID, else the
/// Ultimate-Performance heuristic, else cleaned-name; a not-installed predefined still appears with
/// ExistsOnSystem=false), then any unmatched custom system plans, all sorted by label. The option's value is the
/// installed system GUID when present, else the predefined GUID (selecting a not-installed predefined creates/imports
/// it - that side-effect is the apply path's concern). Labels are the predefined LocalizationKey (PowerPlan_*) or the
/// custom plan's cleaned name, matching the old DisplayName exactly so the UI's PowerPlan_* localization still applies.</summary>
internal static class PowerPlanOptions
{
    public static List<DynamicOption> Build(IReadOnlyList<PowerPlan> systemPlans)
    {
        var options = new List<DynamicOption>();
        var processedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var predefined in PowerPlanCatalog.BuiltInPowerPlans)
        {
            var match = systemPlans.FirstOrDefault(sp =>
                string.Equals(sp.Guid, predefined.Guid, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                match = predefined.Name == "Ultimate Performance"
                    ? systemPlans.FirstOrDefault(sp => PowerPlanHelper.IsUltimatePerformancePlan(sp.Name))
                    : systemPlans.FirstOrDefault(sp =>
                        string.Equals(PowerPlanHelper.CleanPlanName(sp.Name), predefined.Name, StringComparison.OrdinalIgnoreCase));
            }

            // A not-installed predefined still appears (ExistsOnSystem=false), valued by the predefined GUID.
            string guid = match?.Guid ?? predefined.Guid;
            options.Add(new DynamicOption(predefined.LocalizationKey, guid.ToLowerInvariant(), ExistsOnSystem: match is not null));

            if (match is not null)
            {
                processedGuids.Add(match.Guid);
                processedNames.Add(PowerPlanHelper.CleanPlanName(match.Name));
            }
        }

        var unmatched = systemPlans.Where(sp =>
            !processedGuids.Contains(sp.Guid) &&
            !processedNames.Contains(PowerPlanHelper.CleanPlanName(sp.Name)));
        foreach (var sp in unmatched)
            options.Add(new DynamicOption(PowerPlanHelper.CleanPlanName(sp.Name), (sp.Guid ?? string.Empty).ToLowerInvariant()));

        // Match the old service: sort the whole list by the display label (loc key for predefined, cleaned name for
        // custom) and that order is the dropdown order.
        return options.OrderBy(o => o.Label).ToList();
    }
}
