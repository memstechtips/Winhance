using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Common.Interfaces;

/// <summary>
/// Prepares setting definitions for ViewModel creation by filtering compatible settings
/// and applying localization.
/// </summary>
public interface ISettingPreparationPipeline
{
    /// <summary>
    /// Gets filtered settings for the given feature module and localizes each one.
    /// </summary>
    IReadOnlyList<SettingDefinition> PrepareSettings(string featureModuleId);
}
