using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Infrastructure.Features.Common.Services
{
    public class PowerPlanComboBoxService(
        IPowerSettingsQueryService powerSettingsQueryService,
        ILogService logService) : IPowerPlanComboBoxService
    {

        public async Task<ComboBoxSetupResult> SetupPowerPlanComboBoxAsync(SettingDefinition setting, object? currentValue)
        {
            logService.Log(LogLevel.Info, $"[PowerPlanComboBoxService] Setting up PowerPlan ComboBox for '{setting.Id}'");

            var result = new ComboBoxSetupResult();

            try
            {
                var options = await GetPowerPlanOptionsAsync().ConfigureAwait(false);
                var currentIndex = await GetCurrentPowerPlanIndexAsync(options).ConfigureAwait(false);

                foreach (var option in options)
                {
                    result.Options.Add(new ComboBoxOption(
                        option.DisplayName,
                        option.Index,
                        option.ExistsOnSystem ? "Installed on system" : "Not installed",
                        option));
                }

                result.SelectedValue = currentIndex;
                result.Success = true;

                logService.Log(LogLevel.Info, $"[PowerPlanComboBoxService] Setup successful for '{setting.Id}', {result.Options.Count} options, selected: {currentIndex}");
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error setting up PowerPlan ComboBox: {ex.Message}";
                logService.Log(LogLevel.Error, result.ErrorMessage);
                return result;
            }
        }

        public async Task<List<PowerPlanComboBoxOption>> GetPowerPlanOptionsAsync()
        {
            logService.Log(LogLevel.Info, "[PowerPlanComboBoxService] Starting power plan options discovery");
            
            var systemPlans = await powerSettingsQueryService.GetAvailablePowerPlansAsync().ConfigureAwait(false);
            var options = new List<PowerPlanComboBoxOption>();
            var processedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            logService.Log(LogLevel.Info, $"[PowerPlanComboBoxService] Processing {PowerPlanDefinitions.BuiltInPowerPlans.Count} predefined plans against {systemPlans.Count} system plans");

            int matchedPredefinedCount = 0;
            foreach (var predefinedPlan in PowerPlanDefinitions.BuiltInPowerPlans)
            {
                var matchingSystemPlan = systemPlans.FirstOrDefault(sp =>
                    string.Equals(sp.Guid, predefinedPlan.Guid, StringComparison.OrdinalIgnoreCase));

                string matchMethod = "GUID";
                if (matchingSystemPlan == null)
                {
                    if (predefinedPlan.Name == "Ultimate Performance")
                    {
                        matchingSystemPlan = systemPlans.FirstOrDefault(sp => IsUltimatePerformancePlan(sp.Name));
                        matchMethod = "Ultimate Performance detection";
                    }
                    else
                    {
                        matchingSystemPlan = systemPlans.FirstOrDefault(sp =>
                            string.Equals(CleanPlanName(sp.Name), predefinedPlan.Name, StringComparison.OrdinalIgnoreCase));
                        matchMethod = "name";
                    }
                }

                if (matchingSystemPlan != null)
                {
                    options.Add(new PowerPlanComboBoxOption
                    {
                        DisplayName = predefinedPlan.LocalizationKey,
                        PredefinedPlan = predefinedPlan,
                        SystemPlan = matchingSystemPlan,
                        ExistsOnSystem = true,
                        IsActive = matchingSystemPlan.IsActive,
                        Index = options.Count
                    });

                    processedGuids.Add(matchingSystemPlan.Guid);
                    processedNames.Add(CleanPlanName(matchingSystemPlan.Name));
                    matchedPredefinedCount++;

                    logService.Log(LogLevel.Info, $"[PowerPlanComboBoxService]   ✓ Matched predefined '{predefinedPlan.Name}' with system '{CleanPlanName(matchingSystemPlan.Name)}' by {matchMethod}{(matchingSystemPlan.IsActive ? " *ACTIVE*" : "")}");
                }
                else
                {
                    options.Add(new PowerPlanComboBoxOption
                    {
                        DisplayName = predefinedPlan.LocalizationKey,
                        PredefinedPlan = predefinedPlan,
                        SystemPlan = null,
                        ExistsOnSystem = false,
                        IsActive = false,
                        Index = options.Count
                    });

                    logService.Log(LogLevel.Warning, $"[PowerPlanComboBoxService]   ✗ Predefined plan '{predefinedPlan.Name}' not found on system");
                }
            }

            var unmatchedSystemPlans = systemPlans.Where(sp =>
                !processedGuids.Contains(sp.Guid) &&
                !processedNames.Contains(CleanPlanName(sp.Name))).ToList();

            foreach (var systemPlan in unmatchedSystemPlans)
            {
                options.Add(new PowerPlanComboBoxOption
                {
                    DisplayName = CleanPlanName(systemPlan.Name),
                    PredefinedPlan = null,
                    SystemPlan = systemPlan,
                    ExistsOnSystem = true,
                    IsActive = systemPlan.IsActive,
                    Index = options.Count
                });

                logService.Log(LogLevel.Info, $"[PowerPlanComboBoxService]   + Added unmatched system plan '{CleanPlanName(systemPlan.Name)}'{(systemPlan.IsActive ? " *ACTIVE*" : "")}");
            }

            var sortedOptions = options.OrderBy(o => o.DisplayName)
                                     .Select((o, index) => o with { Index = index })
                                     .ToList();

            logService.Log(LogLevel.Info, $"[PowerPlanComboBoxService] Completed: {matchedPredefinedCount}/{PowerPlanDefinitions.BuiltInPowerPlans.Count} predefined matched, {unmatchedSystemPlans.Count} additional system plans, {sortedOptions.Count} total options");
            
            logService.Log(LogLevel.Debug, "[PowerPlanComboBoxService] Final sorted options:");
            for (int i = 0; i < sortedOptions.Count; i++)
            {
                var opt = sortedOptions[i];
                var status = opt.IsActive ? " *ACTIVE*" : "";
                var source = opt.PredefinedPlan != null ? " (predefined)" : " (system-only)";
                logService.Log(LogLevel.Debug, $"[PowerPlanComboBoxService]   {i}: {opt.DisplayName}{status}{source}");
            }

            return sortedOptions;
        }

        private async Task<int> GetCurrentPowerPlanIndexAsync(List<PowerPlanComboBoxOption> options)
        {
            try
            {
                var activePlan = await powerSettingsQueryService.GetActivePowerPlanAsync().ConfigureAwait(false);
                if (activePlan == null) return 0;

                for (int i = 0; i < options.Count; i++)
                {
                    if (options[i].ExistsOnSystem && options[i].SystemPlan != null)
                    {
                        if (string.Equals(options[i].SystemPlan!.Guid, activePlan.Guid, StringComparison.OrdinalIgnoreCase))
                        {
                            logService.Log(LogLevel.Info, $"Found active plan at index {i}: {activePlan.Name} ({activePlan.Guid})");
                            return i;
                        }
                    }
                }

                logService.Log(LogLevel.Warning, $"Active plan not found in options: {activePlan.Name} ({activePlan.Guid})");
                return 0;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error determining active plan index: {ex.Message}");
                return 0;
            }
        }

        private bool IsUltimatePerformancePlan(string planName)
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

        public void InvalidateCache()
        {
            powerSettingsQueryService.InvalidateCache();
        }

        private string CleanPlanName(string name)
        {
            return name?.Trim() ?? string.Empty;
        }

        public async Task<int> ResolveIndexFromRawValuesAsync(SettingDefinition setting, Dictionary<string, object?> rawValues)
        {
            try
            {
                var options = await GetPowerPlanOptionsAsync().ConfigureAwait(false);

                // 1. Try to match by GUID (Best, independent of language)
                if (rawValues.TryGetValue("ActivePowerPlanGuid", out var activeGuidObj) && activeGuidObj != null)
                {
                    var activeGuid = activeGuidObj.ToString();
                    for (int i = 0; i < options.Count; i++)
                    {
                        var opt = options[i];
                        var optGuid = opt.SystemPlan?.Guid ?? opt.PredefinedPlan?.Guid;

                        if (!string.IsNullOrEmpty(optGuid) && 
                            string.Equals(optGuid, activeGuid, StringComparison.OrdinalIgnoreCase))
                        {
                            return i;
                        }
                    }
                }

                // 2. Fallback: Match by Name (Legacy/Unreliable if localized)
                if (rawValues.TryGetValue("ActivePowerPlan", out var activePlanName) && activePlanName != null)
                {
                    var activeNameStr = activePlanName.ToString();

                    for (int i = 0; i < options.Count; i++)
                    {
                        // Check against DisplayName (LocalizationKey) or SystemPlan.Name
                        var displayMatch = string.Equals(CleanPlanName(options[i].DisplayName), activeNameStr, StringComparison.OrdinalIgnoreCase);
                        var systemMatch = options[i].SystemPlan != null && 
                                        string.Equals(CleanPlanName(options[i].SystemPlan!.Name), activeNameStr, StringComparison.OrdinalIgnoreCase);

                        if (displayMatch || systemMatch)
                        {
                            return i;
                        }
                    }
                }

                return 0;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Warning, $"Failed to resolve power plan index from raw values: {ex.Message}");
                return 0;
            }
        }

        public async Task<PowerPlanResolutionResult> ResolvePowerPlanByIndexAsync(int index)
        {
            try
            {
                logService.Log(LogLevel.Info, $"[PowerPlanComboBoxService] Resolving power plan index {index} to GUID");

                var options = await GetPowerPlanOptionsAsync().ConfigureAwait(false);

                if (index < 0 || index >= options.Count)
                {
                    var errorMsg = $"Invalid power plan index: {index} (available: 0-{options.Count - 1})";
                    logService.Log(LogLevel.Error, $"[PowerPlanComboBoxService] {errorMsg}");
                    return new PowerPlanResolutionResult { Success = false, ErrorMessage = errorMsg };
                }

                var selectedOption = options[index];
                var powerPlanGuid = selectedOption.SystemPlan?.Guid ?? selectedOption.PredefinedPlan?.Guid;

                if (string.IsNullOrEmpty(powerPlanGuid))
                {
                    var errorMsg = $"Could not resolve GUID for power plan at index {index}";
                    logService.Log(LogLevel.Error, $"[PowerPlanComboBoxService] {errorMsg}");
                    return new PowerPlanResolutionResult { Success = false, ErrorMessage = errorMsg };
                }

                logService.Log(LogLevel.Info, $"[PowerPlanComboBoxService] Resolved index {index} to: {selectedOption.DisplayName} ({powerPlanGuid})");

                return new PowerPlanResolutionResult
                {
                    Success = true,
                    Guid = powerPlanGuid,
                    DisplayName = selectedOption.DisplayName
                };
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"[PowerPlanComboBoxService] Error resolving power plan index {index}: {ex.Message}");
                return new PowerPlanResolutionResult { Success = false, ErrorMessage = ex.Message };
            }
        }
    }
}