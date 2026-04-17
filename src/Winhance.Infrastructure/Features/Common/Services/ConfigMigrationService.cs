using System;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

/// <summary>
/// Migrates legacy configuration items to their current format.
/// Each migration is registered by setting ID and transforms a ConfigurationItem in-place.
/// </summary>
public class ConfigMigrationService : IConfigMigrationService
{
    private readonly ILogService _logService;

    private readonly Dictionary<string, Action<ConfigurationItem>> _migrations;

    public ConfigMigrationService(ILogService logService)
    {
        _logService = logService;

        _migrations = new Dictionary<string, Action<ConfigurationItem>>
        {
            ["taskbar-transparent"] = MigrateTaskbarTransparent,
            ["explorer-customization-shortcut-suffix"] = MigrateToggleToSelection,
            ["explorer-customization-shortcut-arrow"] = MigrateToggleToSelection,
            ["gaming-background-apps"] = MigrateBackgroundApps,
        };
    }

    /// <summary>
    /// Applies all registered migrations to items in the given configuration file.
    /// </summary>
    public void MigrateConfig(UnifiedConfigurationFile config)
    {
        if (config == null) return;

        // Walk Customize features
        if (config.Customize?.Features != null)
        {
            foreach (var kvp in config.Customize.Features)
            {
                MigrateSection(kvp.Value, kvp.Key);
            }
        }

        // Walk Optimize features
        if (config.Optimize?.Features != null)
        {
            foreach (var kvp in config.Optimize.Features)
            {
                MigrateSection(kvp.Value, kvp.Key);
            }
        }

        // Walk WindowsApps
        MigrateSection(config.WindowsApps, "WindowsApps");

        // Walk ExternalApps
        MigrateSection(config.ExternalApps, "ExternalApps");
    }

    private void MigrateSection(ConfigSection? section, string sectionName)
    {
        if (section?.Items == null) return;

        foreach (var item in section.Items)
        {
            if (item?.Id != null && _migrations.TryGetValue(item.Id, out var migration))
            {
                try
                {
                    migration(item);
                }
                catch (Exception ex)
                {
                    _logService.Log(
                        LogLevel.Warning,
                        $"Config migration failed for '{item.Id}' in section '{sectionName}': {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Migrates a Toggle-based setting to Selection format.
    /// Old format: InputType=Toggle, IsSelected=true (action applied) or false (default).
    /// New format: InputType=Selection, SelectedIndex=0 (default/first option), 1 (action applied/second option).
    /// </summary>
    private void MigrateToggleToSelection(ConfigurationItem item)
    {
        if (item.InputType != InputType.Toggle)
            return; // Already migrated or not a toggle

        if (item.IsSelected == true)
        {
            item.SelectedIndex = 1; // The "action" option (e.g., "Remove")
        }
        else
        {
            item.SelectedIndex = 0; // The "default" option (e.g., "Keep"/"Show")
        }

        item.InputType = InputType.Selection;
        item.IsSelected = null;

        _logService.Log(
            LogLevel.Info,
            $"Migrated config item '{item.Id}' from Toggle to Selection (SelectedIndex={item.SelectedIndex})");
    }

    /// <summary>
    /// Migrates the old Toggle-based "gaming-background-apps" setting to the new Selection-based format.
    /// Old format: InputType=Toggle, IsSelected=false (intended to block background apps) or true (allow).
    /// New format: InputType=Selection, SelectedIndex=0 (User in Control), 1 (Force Allow), 2 (Force Deny).
    /// Note: The old toggle had a bug where DisabledValue was 0 (User in Control) instead of 2 (Force Deny).
    /// We map IsSelected=false to Force Deny (index 2) since that was the user's intent.
    /// </summary>
    private void MigrateBackgroundApps(ConfigurationItem item)
    {
        if (item.InputType != InputType.Toggle)
            return; // Already migrated or not a toggle

        if (item.IsSelected == false)
        {
            item.SelectedIndex = 2; // Force Deny — what the user intended
        }
        else
        {
            item.SelectedIndex = 0; // User in Control (default)
        }

        item.InputType = InputType.Selection;
        item.IsSelected = null;

        _logService.Log(
            LogLevel.Info,
            $"Migrated config item '{item.Id}' from Toggle to Selection (SelectedIndex={item.SelectedIndex})");
    }

    /// <summary>
    /// Migrates the old Toggle-based "taskbar-transparent" setting to the new Selection-based format.
    /// Old format: InputType=Toggle, IsSelected=true (transparent) or false (Windows default).
    /// New format: InputType=Selection, SelectedIndex=0 (Windows default), 1 (Transparent), 2 (Opaque).
    /// </summary>
    private void MigrateTaskbarTransparent(ConfigurationItem item)
    {
        if (item.InputType != InputType.Toggle)
            return; // Already migrated or not a toggle

        if (item.IsSelected == true)
        {
            item.SelectedIndex = 1; // Transparent
        }
        else
        {
            item.SelectedIndex = 0; // Windows default
        }

        item.InputType = InputType.Selection;
        item.IsSelected = null;

        _logService.Log(
            LogLevel.Info,
            $"Migrated config item '{item.Id}' from Toggle to Selection (SelectedIndex={item.SelectedIndex})");
    }
}
