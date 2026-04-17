using Microsoft.Win32;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces;

public interface IWindowsRegistryService
{
    bool SetValue(string keyPath, string valueName, object value, RegistryValueKind kind);
    object? GetValue(string keyPath, string valueName);
    bool DeleteKey(string keyPath);
    bool DeleteValue(string keyPath, string valueName);

    bool KeyExists(string keyPath);
    bool ValueExists(string keyPath, string valueName);
    string[] GetSubKeyNames(string keyPath);
    bool RegistryValueExists(RegistrySetting setting);
    bool IsSettingApplied(RegistrySetting setting);
    bool IsRegistryValueInEnabledState(RegistrySetting setting, object? currentValue, bool valueExists);

    /// <param name="useDefaultValue">When true, uses DisabledValue[1] (parent cascade value) instead of DisabledValue[0]. If no second element exists, falls back to normal behavior.</param>
    bool ApplySetting(RegistrySetting setting, bool enable, object? specificValue = null, bool useDefaultValue = false);

    Dictionary<string, object?> GetBatchValues(IEnumerable<(string keyPath, string? valueName)> queries);
}
