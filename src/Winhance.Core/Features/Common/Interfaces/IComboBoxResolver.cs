using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IComboBoxResolver
{
    Task<object?> ResolveCurrentValueAsync(SettingDefinition setting, Dictionary<string, object?>? existingRawValues = null);
    int ResolveRawValuesToIndex(SettingDefinition setting, Dictionary<string, object?> rawValues);
    Dictionary<string, object?> ResolveIndexToRawValues(SettingDefinition setting, int index);
    int GetValueFromIndex(SettingDefinition setting, int index);
    int GetIndexFromDisplayName(SettingDefinition setting, string displayName);
}
