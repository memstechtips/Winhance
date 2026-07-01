using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IComboBoxResolver
{
    int ResolveRawValuesToIndex(SettingDefinition setting, Dictionary<string, object?> rawValues);
    Dictionary<string, object?> ResolveIndexToRawValues(SettingDefinition setting, int index);
    int GetValueFromIndex(SettingDefinition setting, int index);
    int GetIndexFromDisplayName(SettingDefinition setting, string displayName);
}
