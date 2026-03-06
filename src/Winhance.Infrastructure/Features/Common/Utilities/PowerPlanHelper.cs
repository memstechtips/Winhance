namespace Winhance.Infrastructure.Features.Common.Utilities;

/// <summary>
/// Shared power plan identification logic used by PowerService and PowerPlanComboBoxService.
/// </summary>
internal static class PowerPlanHelper
{
    public static bool IsUltimatePerformancePlan(string planName)
    {
        var cleanName = CleanPlanName(planName).ToLowerInvariant();

        var knownNames = new[]
        {
            "ultimate performance",
            "rendimiento máximo",
            "prestazioni ottimali",
            "höchstleistung",
            "performances optimales",
            "desempenho máximo",
            "ultieme prestaties",
            "максимальная производительность"
        };

        if (knownNames.Contains(cleanName))
            return true;

        var ultimateWords = new[] { "ultimate", "ultieme", "máximo", "optimal", "höchst" };
        var performanceWords = new[] { "performance", "prestazioni", "leistung", "performances", "desempenho" };

        bool hasUltimateWord = ultimateWords.Any(word => cleanName.Contains(word));
        bool hasPerformanceWord = performanceWords.Any(word => cleanName.Contains(word));

        return hasUltimateWord && hasPerformanceWord;
    }

    public static string CleanPlanName(string name)
    {
        return name?.Trim() ?? string.Empty;
    }
}
