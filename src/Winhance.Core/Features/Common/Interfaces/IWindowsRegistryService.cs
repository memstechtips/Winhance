using Microsoft.Win32;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Interfaces
{
    public interface IWindowsRegistryService
    {
        bool CreateKey(string keyPath);
        bool SetValue(string keyPath, string valueName, object value, RegistryValueKind kind);
        object? GetValue(string keyPath, string valueName);
        bool DeleteKey(string keyPath);
        bool DeleteValue(string keyPath, string valueName);

        bool KeyExists(string keyPath);
        bool ValueExists(string keyPath, string valueName);

        bool IsSettingApplied(RegistrySetting setting);

        bool ApplySetting(RegistrySetting setting, bool enable, object? specificValue = null);

        Dictionary<string, object?> GetBatchValues(IEnumerable<(string keyPath, string valueName)> queries);
    }
}
