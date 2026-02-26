using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Customize.Interfaces;
using Winhance.Core.Features.Customize.Models;
using Winhance.Infrastructure.Features.Common.Services;

namespace Winhance.Infrastructure.Features.Customize.Services;

public class TaskbarService(
    ILogService logService,
    IWindowsRegistryService windowsRegistryService,
    ICompatibleSettingsRegistry compatibleSettingsRegistry) : IDomainService, IActionCommandProvider
{
    private volatile IEnumerable<SettingDefinition>? _cachedSettings;
    private readonly object _cacheLock = new object();

    public string DomainName => FeatureIds.Taskbar;

    public async Task<IEnumerable<SettingDefinition>> GetSettingsAsync()
    {
        // Return cached settings if available
        if (_cachedSettings != null)
            return _cachedSettings;

        lock (_cacheLock)
        {
            // Double-check locking pattern
            if (_cachedSettings != null)
                return _cachedSettings;

            try
            {
                logService.Log(LogLevel.Info, "Loading Taskbar settings");

                _cachedSettings = compatibleSettingsRegistry.GetFilteredSettings(FeatureIds.Taskbar);
                return _cachedSettings;
            }
            catch (Exception ex)
            {
                logService.Log(LogLevel.Error, $"Error loading Taskbar settings: {ex.Message}");
                return Enumerable.Empty<SettingDefinition>();
            }
        }
    }

    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cachedSettings = null;
            logService.Log(LogLevel.Debug, "Taskbar settings cache cleared");
        }
    }

    public Task CleanTaskbarAsync()
    {
        try
        {
            const string taskbandKey = "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Taskband";
            const string favoritesValue = "Favorites";

            logService.Log(LogLevel.Info, "Starting Taskbar cleanup - clearing Favorites data");

            if (!windowsRegistryService.KeyExists(taskbandKey))
            {
                logService.Log(LogLevel.Warning, "Taskband key does not exist - nothing to clean");
                return Task.CompletedTask;
            }

            bool success = windowsRegistryService.SetValue(
                taskbandKey,
                favoritesValue,
                new byte[0],
                Microsoft.Win32.RegistryValueKind.Binary
            );

            if (success)
            {
                logService.Log(LogLevel.Success, "Successfully cleared Favorites data");
            }
            else
            {
                logService.Log(LogLevel.Warning, "Failed to clear Favorites data");
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            logService.Log(LogLevel.Error, $"Error during Taskbar cleanup: {ex.Message}");
            throw;
        }
    }

    private static readonly HashSet<string> _supportedCommands = new(StringComparer.Ordinal)
    {
        nameof(CleanTaskbarAsync)
    };

    public IReadOnlySet<string> SupportedCommands => _supportedCommands;

    public Task ExecuteCommandAsync(string commandName) => commandName switch
    {
        nameof(CleanTaskbarAsync) => CleanTaskbarAsync(),
        _ => throw new NotSupportedException($"Command '{commandName}' is not supported by {nameof(TaskbarService)}")
    };
}
