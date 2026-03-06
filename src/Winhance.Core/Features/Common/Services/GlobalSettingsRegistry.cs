using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.Core.Features.Common.Services;

public class GlobalSettingsRegistry : IGlobalSettingsRegistry
{
    private readonly ConcurrentDictionary<string, List<ISettingItem>> _moduleSettings;
    private readonly ILogService _logService;
    private readonly object _listLock = new();

    public GlobalSettingsRegistry(ILogService logService)
    {
        _moduleSettings = new ConcurrentDictionary<string, List<ISettingItem>>();
        _logService = logService;
    }

    public void RegisterSettings(string moduleName, IEnumerable<ISettingItem> settings)
    {
        if (string.IsNullOrEmpty(moduleName))
        {
            _logService.Log(
                LogLevel.Warning,
                "Cannot register settings for null or empty module name"
            );
            return;
        }

        var settingsList = settings?.ToList() ?? new List<ISettingItem>();
        _moduleSettings.AddOrUpdate(moduleName, settingsList, (key, oldValue) => settingsList);

        _logService.Log(
            LogLevel.Debug,
            $"Registered {settingsList.Count} settings for module '{moduleName}'"
        );
    }

    public ISettingItem? GetSetting(string settingId, string? moduleName = null)
    {
        if (string.IsNullOrEmpty(settingId))
        {
            _logService.Log(
                LogLevel.Warning,
                "Cannot get setting for null or empty setting ID"
            );
            return null;
        }

        if (!string.IsNullOrEmpty(moduleName))
        {
            // Search in specific module
            if (_moduleSettings.TryGetValue(moduleName, out var moduleSettingsList))
            {
                ISettingItem? setting;
                lock (_listLock)
                {
                    setting = moduleSettingsList.FirstOrDefault(s => s.Id == settingId);
                }
                if (setting != null)
                {
                    _logService.Log(
                        LogLevel.Debug,
                        $"Found setting '{settingId}' in module '{moduleName}'"
                    );
                    return setting;
                }
            }
            _logService.Log(
                LogLevel.Debug,
                $"Setting '{settingId}' not found in module '{moduleName}'"
            );
            return null;
        }

        // Search in all modules
        foreach (var kvp in _moduleSettings)
        {
            ISettingItem? setting;
            lock (_listLock)
            {
                setting = kvp.Value.FirstOrDefault(s => s.Id == settingId);
            }
            if (setting != null)
            {
                _logService.Log(
                    LogLevel.Debug,
                    $"Found setting '{settingId}' in module '{kvp.Key}'"
                );
                return setting;
            }
        }

        _logService.Log(LogLevel.Debug, $"Setting '{settingId}' not found in any module");
        return null;
    }

    public IEnumerable<ISettingItem> GetAllSettings()
    {
        lock (_listLock)
        {
            return _moduleSettings.Values
                .SelectMany(settings => settings)
                .ToList();
        }
    }

    public void RegisterSetting(string moduleName, ISettingItem setting)
    {
        if (string.IsNullOrEmpty(moduleName))
        {
            _logService.Log(
                LogLevel.Warning,
                "Cannot register setting for null or empty module name"
            );
            return;
        }

        if (setting == null)
        {
            _logService.Log(LogLevel.Warning, "Cannot register null setting");
            return;
        }

        lock (_listLock)
        {
            _moduleSettings.AddOrUpdate(
                moduleName,
                new List<ISettingItem> { setting }, // Create new list if module doesn't exist
                (key, existingSettings) =>
                {
                    // Add to existing list if setting doesn't already exist
                    if (!existingSettings.Any(s => s.Id == setting.Id))
                    {
                        existingSettings.Add(setting);
                        _logService.Log(
                            LogLevel.Debug,
                            $"Added setting '{setting.Id}' to existing module '{moduleName}'"
                        );
                    }
                    else
                    {
                        _logService.Log(
                            LogLevel.Debug,
                            $"Setting '{setting.Id}' already exists in module '{moduleName}', skipping registration"
                        );
                    }
                    return existingSettings;
                }
            );
        }

        _logService.Log(
            LogLevel.Debug,
            $"Registered setting '{setting.Id}' for module '{moduleName}'"
        );
    }

}
