using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Prepares setting definitions by filtering for compatibility and applying localization.
/// Replaces the two separate ICompatibleSettingsRegistry + ISettingLocalizationService
/// calls that were always used together in SettingsLoadingService.
/// </summary>
public class SettingPreparationPipeline : ISettingPreparationPipeline
{
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;
    private readonly ISettingLocalizationService _settingLocalizationService;

    public SettingPreparationPipeline(
        ICompatibleSettingsRegistry compatibleSettingsRegistry,
        ISettingLocalizationService settingLocalizationService)
    {
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
        _settingLocalizationService = settingLocalizationService;
    }

    /// <inheritdoc />
    public IReadOnlyList<SettingDefinition> PrepareSettings(string featureModuleId)
    {
        var settingDefinitions = _compatibleSettingsRegistry.GetFilteredSettings(featureModuleId);
        return settingDefinitions
            .Select(s => _settingLocalizationService.LocalizeSetting(s))
            .ToList();
    }
}
