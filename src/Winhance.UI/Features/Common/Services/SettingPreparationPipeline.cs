using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Prepares setting definitions by filtering for compatibility.
/// Slice B2: localization was retired (SettingLocalizationService.LocalizeSetting deleted); the display fields are
/// now localized on the catalog path (SettingViewModelFactory), so this is a thin compatibility-filter pass-through.
/// Full inlining/removal is a Plan 4 teardown nit.
/// </summary>
public class SettingPreparationPipeline : ISettingPreparationPipeline
{
    private readonly ICompatibleSettingsRegistry _compatibleSettingsRegistry;

    public SettingPreparationPipeline(ICompatibleSettingsRegistry compatibleSettingsRegistry)
    {
        _compatibleSettingsRegistry = compatibleSettingsRegistry;
    }

    /// <inheritdoc />
    public IReadOnlyList<SettingDefinition> PrepareSettings(string featureModuleId)
    {
        return _compatibleSettingsRegistry.GetFilteredSettings(featureModuleId).ToList();
    }
}
