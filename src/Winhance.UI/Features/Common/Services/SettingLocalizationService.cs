using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Catalog;
using Winhance.Core.Features.Common.Extensions;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

// Display localization (Name/Description/GroupName, options, tooltips, warnings) happens in
// SettingViewModelFactory via the canonical Setting_{id}_* keys. This service only builds the
// cross-group info banner.
public class SettingLocalizationService : ISettingLocalizationService
{
    private readonly ILocalizationService _localization;
    private readonly ICatalogSettingsRegistry _catalogSettingsRegistry;
    private readonly ICatalogScopeProvider _scopeProvider;

    public SettingLocalizationService(
        ILocalizationService localization,
        ICatalogSettingsRegistry catalogSettingsRegistry,
        ICatalogScopeProvider scopeProvider)
    {
        _localization = localization;
        _catalogSettingsRegistry = catalogSettingsRegistry;
        _scopeProvider = scopeProvider;
    }

    public string? BuildCrossGroupInfoMessage(Setting setting)
    {
        var crossGroupSettings = setting.Display.CrossGroupChildSettings;
        if (crossGroupSettings == null || crossGroupSettings.Count == 0)
        {
            return null;
        }

        var groupedSettings = new Dictionary<string, List<string>>();

        foreach (var (childSettingId, localizationKey) in crossGroupSettings)
        {
            try
            {
                // A null means the id is not in the mode-scoped membership (the skip condition). None of the
                // authored cross-group child ids is alias-affected, so Normalize is identity here.
                var childSetting = _catalogSettingsRegistry.GetById(childSettingId, _scopeProvider.Current);

                if (childSetting == null) continue;

                var featureName = GetFeatureName(childSettingId);
                var localizedGroupName = childSetting.Display.GroupName is { } childGroup
                    ? _localization.GetString(childGroup.Value)
                    : string.Empty;
                var groupKey = $"{featureName} ({localizedGroupName})";

                if (!groupedSettings.TryGetValue(groupKey, out var groupItems))
                {
                    groupItems = new List<string>();
                    groupedSettings[groupKey] = groupItems;
                }

                groupItems.Add(_localization.GetStringOrDefault(localizationKey.Value, childSettingId));
            }
            catch
            {
                // Skip settings that can't be looked up
            }
        }

        if (groupedSettings.Count == 0) return null;

        var header = _localization.GetString("Setting_CrossGroupWarning_Header");
        var lines = groupedSettings.Select(kvp => $"• {kvp.Key}: {string.Join(", ", kvp.Value)}");
        return $"{header}\n{string.Join("\n", lines)}";
    }

    private string GetFeatureName(string settingId) => _localization.FeatureName(settingId, _localization.GetString("Nav_Settings"));
}
