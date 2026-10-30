using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Infrastructure.Features.Common.Services;

internal class ConfigMigrationService : IConfigMigrationService
{
    private readonly ILogService _logService;

    private readonly Dictionary<string, Action<ConfigurationItem>> _migrations;

    // Taken from the card itself so the two cannot drift; the order IS the old index order.
    private static readonly IReadOnlyList<string> ShortDateKeys =
        SettingCatalog.Find("region-short-date")?.Options?.Keys ?? [];

    // The three "DoH" DNS options became their plain servers, each encrypted with the template Windows knows for it.
    private static readonly Dictionary<int, (string Preferred, string Alternate)> DnsDohOptions = new()
    {
        [7] = ("1.1.1.1", "1.0.0.1"),
        [8] = ("8.8.8.8", "8.8.4.4"),
        [9] = ("9.9.9.9", "149.112.112.112"),
    };

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
            ["region-short-date"] = MigrateShortDateToKey,
            ["gaming-dns-server"] = MigrateDnsDohOption,
        };
    }

    public void MigrateConfig(WinhanceConfigFile config)
    {
        if (config == null) return;

        DropGaps(config);
        RenameRetiredFeatures(config.Customize);
        RenameRetiredFeatures(config.Optimize);

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

    // A hand-edited file can say "Optimize": null, a feature: null or "Items": null, each replacing the empty default,
    // and an item can be null or have no id. Every reader walks the migrated file, so the gaps go here once.
    private static void DropGaps(WinhanceConfigFile config)
    {
        config.WindowsApps = DropGaps(config.WindowsApps);
        config.ExternalApps = DropGaps(config.ExternalApps);
        foreach (var group in new[] { config.Customize ??= new(), config.Optimize ??= new(), config.Autounattend ??= new() })
        {
            group.Features = (group.Features ?? new Dictionary<string, ConfigSection>())
                .Where(feature => feature.Value is not null)
                .ToDictionary(feature => feature.Key, feature => DropGaps(feature.Value), StringComparer.Ordinal);
        }
    }

    private static ConfigSection DropGaps(ConfigSection? section)
    {
        section ??= new ConfigSection();
        section.Items = section.Items?.Where(item => !string.IsNullOrEmpty(item?.Id)).ToList() ?? [];
        return section;
    }

    // A feature id is a dictionary KEY, so unlike a setting id it cannot be normalized in place; the group gets a
    // rebuilt dictionary. Runs before the id-keyed migrations so every downstream consumer sees the current id.
    private static void RenameRetiredFeatures(FeatureGroupSection? group)
    {
        if (group?.Features is not { Count: > 0 } features) return;
        if (features.Keys.All(id => FeatureIdAliases.Normalize(id) == id)) return;

        var renamed = new Dictionary<string, ConfigSection>(features.Count, StringComparer.Ordinal);
        foreach (var (id, section) in features)
            renamed[FeatureIdAliases.Normalize(id)] = section;
        group.Features = renamed;
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

    private void MigrateDnsDohOption(ConfigurationItem item)
    {
        if (item.SelectedIndex is not { } index || !DnsDohOptions.TryGetValue(index, out var servers))
            return;

        item.SelectedIndex = null;
        item.CustomStateValues = new Dictionary<string, object>
        {
            ["primary"] = servers.Preferred,
            ["primaryDoh"] = ((int)DohMode.Automatic).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["secondary"] = servers.Alternate,
            ["secondaryDoh"] = ((int)DohMode.Automatic).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        _logService.Log(LogLevel.Info, $"Migrated config item '{item.Id}' from DoH option {index} to its servers, encrypted");
    }

    // The card was five fixed states and is now keyed on the pattern itself, because region-format writes
    // patterns the five never covered. An older file carries the index those five had.
    private void MigrateShortDateToKey(ConfigurationItem item)
    {
        if (item.SelectedKey is { Length: > 0 }) return;
        if (item.SelectedIndex is not { } index || index < 0 || index >= ShortDateKeys.Count) return;

        item.SelectedKey = ShortDateKeys[index];
        item.SelectedKeyLabel = ShortDateKeys[index];
        item.SelectedIndex = null;

        _logService.Log(
            LogLevel.Info,
            $"Migrated config item '{item.Id}' from Selection index {index} to key '{item.SelectedKey}'");
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
