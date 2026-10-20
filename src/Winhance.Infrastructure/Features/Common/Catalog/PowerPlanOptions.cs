using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Optimize.Models;
using Winhance.Infrastructure.Features.Common.Utilities;

namespace Winhance.Infrastructure.Features.Common.Catalog;

// Every predefined plan appears (matched to a system plan by GUID, else the Ultimate-Performance heuristic,
// else cleaned name; a not-installed predefined still appears with ExistsOnSystem=false), then unmatched custom
// plans, all sorted by label. Value = the installed GUID when present, else the predefined GUID (selecting a
// not-installed one creates/imports it on apply).
internal static class PowerPlanOptions
{
    public static List<DynamicOption> Build(
        IReadOnlyList<PowerPlan> systemPlans, string? activeGuid, ILocalizationService localization)
    {
        var options = new List<DynamicOption>();
        var processedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Windows refuses to delete the scheme it is running on, so the active one is never offered for deletion.
        bool IsActive(string? guid) =>
            guid is { Length: > 0 } && string.Equals(guid, activeGuid, StringComparison.OrdinalIgnoreCase);

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

            string guid = match?.Guid ?? predefined.Guid;
            var label = localization.TryGetString(predefined.LocalizationKey, out var text) ? text : predefined.Name;
            options.Add(new DynamicOption(
                label,
                guid.ToLowerInvariant(),
                ExistsOnSystem: match is not null,
                CanDelete: match is not null && !IsActive(guid)));

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
            options.Add(new DynamicOption(
                PowerPlanHelper.CleanPlanName(sp.Name),
                (sp.Guid ?? string.Empty).ToLowerInvariant(),
                CanDelete: !IsActive(sp.Guid)));

        return options.OrderBy(o => o.Label).ToList();
    }
}
