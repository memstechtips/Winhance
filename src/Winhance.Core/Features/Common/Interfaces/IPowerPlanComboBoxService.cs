using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IPowerPlanComboBoxService
{
    Task<ComboBoxSetupResult> SetupPowerPlanComboBoxAsync(SettingDefinition setting, object? currentValue);
    Task<List<PowerPlanComboBoxOption>> GetPowerPlanOptionsAsync();
    Task<int> ResolveIndexFromRawValuesAsync(SettingDefinition setting, Dictionary<string, object?> rawValues);
    Task<PowerPlanResolutionResult> ResolvePowerPlanByIndexAsync(int index);
    void InvalidateCache();
}
