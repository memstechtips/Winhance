using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Models;

namespace Winhance.Core.Features.Common.Selections;

// Measured on the file as it sits on disk, before migration: a migrated file already reads the new ids, so the
// only place an old id or an old feature still shows is the raw file.
// Retired: items no Winhance setting answers to any more, even through an old id.
// Missing: settings this PC would save that the file says nothing about.
// Moved: items filed under an old id or an old feature, which an updated copy rewrites.
public sealed record ConfigCoverage(IReadOnlyList<ConfigurationItem> Retired, IReadOnlyList<Setting> Missing, int Moved)
{
    public static ConfigCoverage Empty { get; } = new([], [], 0);

    public bool IsComplete => Retired.Count == 0 && Missing.Count == 0 && Moved == 0;

    public bool CanUpdate => Retired.Count > 0 || Moved > 0;

    // An Action has no state to save, so a file that leaves one out has not fallen behind.
    public static ConfigCoverage Measure(WinhanceConfigFile config, IReadOnlyDictionary<string, IReadOnlyList<Setting>> shownByFeature)
    {
        var retired = new List<ConfigurationItem>();
        var known = new HashSet<string>(StringComparer.Ordinal);
        var moved = 0;
        foreach (var (_, featureId, _, items) in config.SettingSections())
        {
            foreach (var item in items)
            {
                // A hand-edited file can carry an item with no id at all; nothing answers to it.
                if ((string.IsNullOrEmpty(item.Id) ? null : SettingCatalog.Find(item.Id)) is not { } setting)
                {
                    retired.Add(item);
                    continue;
                }

                known.Add(setting.Id);
                if (item.Id != setting.Id || FeatureIdAliases.Normalize(featureId) != SettingCatalog.FeatureById[setting.Id])
                    moved++;
            }
        }

        var missing = shownByFeature
            .Where(feature => FeatureDefinitions.IsSettingsFeature(feature.Key))
            .SelectMany(feature => feature.Value)
            .Where(s => s.Control != ControlKind.Action && !known.Contains(s.Id))
            .ToList();

        return new ConfigCoverage(retired, missing, moved);
    }

    // A round trip through the whole catalog, not this PC's slice of it, so a setting this PC hides keeps its
    // choice. Old ids come back under their new feature and retired items fall out; the apps are copied as they were.
    public static WinhanceConfigFile Updated(WinhanceConfigFile config)
    {
        var updated = ConfigFileMapper.ToFile(ConfigFileMapper.FromFile(config, SettingCatalog.ByFeature), SettingCatalog.ByFeature);
        updated.ComputerName = config.ComputerName;
        updated.TargetBuild = config.TargetBuild;
        updated.WindowsApps = config.WindowsApps;
        updated.ExternalApps = config.ExternalApps;
        return updated;
    }
}
