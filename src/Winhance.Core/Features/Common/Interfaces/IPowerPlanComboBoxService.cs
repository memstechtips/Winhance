using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IPowerPlanComboBoxService
    {
        Task<ComboBoxSetupResult> SetupPowerPlanComboBoxAsync(SettingDefinition setting, object? currentValue);
        Task<List<PowerPlanComboBoxOption>> GetPowerPlanOptionsAsync();
        int ResolveIndexFromRawValues(SettingDefinition setting, Dictionary<string, object?> rawValues);
    }
}