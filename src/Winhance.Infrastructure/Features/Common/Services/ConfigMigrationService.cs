using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class ConfigMigrationService : IConfigMigrationService
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
            ["updates-notification-level"] = MigrateUpdateNotificationLevel,
        };
    }

    public void MigrateConfig(WinhanceConfigFile config)
    {
        if (config == null) return;

        if (config.Customize?.Features != null)
        {
            foreach (var kvp in config.Customize.Features)
            {
                MigrateSection(kvp.Value, kvp.Key);
            }
        }

        if (config.Optimize?.Features != null)
        {
            foreach (var kvp in config.Optimize.Features)
            {
                MigrateSection(kvp.Value, kvp.Key);
            }
        }

        MigrateSection(config.WindowsApps, "WindowsApps");

        MigrateSection(config.ExternalApps, "ExternalApps");
    }

    private void MigrateSection(ConfigSection? section, string sectionName)
    {
        if (section?.Items == null) return;

        foreach (var item in section.Items)
        {
            if (item?.Id == null) continue;

            // Normalize a retired config id (e.g. the merged "-win10" This PC variants) to its canonical
            // catalog id BEFORE any id-keyed migration runs, so every downstream consumer (build-gating, apply,
            // export round-trip) sees the canonical id. Pure passthrough for ids that are not aliases.
            item.Id = SettingIdAliases.Normalize(item.Id);

            if (_migrations.TryGetValue(item.Id, out var migration))
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

    // Old: Toggle IsSelected true (applied) / false (default). New: Selection index 0 (default), 1 (applied).
    private void MigrateToggleToSelection(ConfigurationItem item)
    {
        if (item.InputType != InputType.Toggle)
            return; // Already migrated or not a toggle

        if (item.IsSelected == true)
        {
            item.SelectedIndex = 1;
        }
        else
        {
            item.SelectedIndex = 0;
        }

        item.InputType = InputType.Selection;
        item.IsSelected = null;

        _logService.Log(
            LogLevel.Info,
            $"Migrated config item '{item.Id}' from Toggle to Selection (SelectedIndex={item.SelectedIndex})");
    }

    // Both toggle positions map to index 0 ("Show all update notifications"), on purpose: IsSelected=false is
    // what an untouched setting exported as, and IsSelected=true meant "I want update notifications", which IS
    // index 0. Nobody had working suppression to preserve - the old state wrote SetUpdateNotificationLevel=2, which
    // the ADMX gives no meaning; applying index 0 also clears that stale value.
    private void MigrateUpdateNotificationLevel(ConfigurationItem item)
    {
        if (item.InputType != InputType.Toggle)
            return; // Already migrated or not a toggle

        item.SelectedIndex = 0;
        item.InputType = InputType.Selection;
        item.IsSelected = null;

        _logService.Log(
            LogLevel.Info,
            $"Migrated config item '{item.Id}' from Toggle to Selection (SelectedIndex=0)");
    }

    // Old: Toggle IsSelected false (block background apps) / true (allow). New: index 0 User in Control, 1 Force
    // Allow, 2 Force Deny. The old toggle's DisabledValue was 0 (User in Control) by mistake; IsSelected=false maps
    // to Force Deny (2), which was the user's intent.
    private void MigrateBackgroundApps(ConfigurationItem item)
    {
        if (item.InputType != InputType.Toggle)
            return; // Already migrated or not a toggle

        if (item.IsSelected == false)
        {
            item.SelectedIndex = 2;
        }
        else
        {
            item.SelectedIndex = 0;
        }

        item.InputType = InputType.Selection;
        item.IsSelected = null;

        _logService.Log(
            LogLevel.Info,
            $"Migrated config item '{item.Id}' from Toggle to Selection (SelectedIndex={item.SelectedIndex})");
    }

    // Old: Toggle IsSelected true (transparent) / false (default). New: index 0 Windows default, 1 Transparent, 2 Opaque.
    private void MigrateTaskbarTransparent(ConfigurationItem item)
    {
        if (item.InputType != InputType.Toggle)
            return; // Already migrated or not a toggle

        if (item.IsSelected == true)
        {
            item.SelectedIndex = 1;
        }
        else
        {
            item.SelectedIndex = 0;
        }

        item.InputType = InputType.Selection;
        item.IsSelected = null;

        _logService.Log(
            LogLevel.Info,
            $"Migrated config item '{item.Id}' from Toggle to Selection (SelectedIndex={item.SelectedIndex})");
    }
}
