using Microsoft.Win32;
using System.Collections.Generic;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Models;
using Winhance.Core.Features.Optimize.Models;

namespace Winhance.Core.Features.Common.Models;

// This class is deprecated. Use Winhance.Core.Features.Optimize.Models.OptimizationSetting instead.
[System.Obsolete("This class is deprecated. Use Winhance.Core.Features.Optimize.Models.OptimizationSetting instead.")]
public record OptimizationSetting
{
    public required string Id { get; init; }  // Unique identifier
    public required string Name { get; init; }  // User-friendly name
    public required string Description { get; init; }
    public required OptimizationCategory Category { get; init; }
    public required string GroupName { get; init; }  // Sub-group within category

    // Single registry setting (for backward compatibility)
    public RegistrySetting? RegistrySetting { get; init; }

    // Multiple registry settings (new approach)
    private LinkedRegistrySettings? _linkedRegistrySettings;
    public LinkedRegistrySettings LinkedRegistrySettings
    {
        get
        {
            // If LinkedRegistrySettings is null but RegistrySetting is not, create a new LinkedRegistrySettings with the single RegistrySetting
            if (_linkedRegistrySettings == null && RegistrySetting != null)
            {
                _linkedRegistrySettings = new LinkedRegistrySettings(RegistrySetting);
            }
            return _linkedRegistrySettings ?? new LinkedRegistrySettings();
        }
        init { _linkedRegistrySettings = value; }
    }

    // New approach: Use a collection of registry settings directly
    public List<RegistrySetting> RegistrySettings { get; init; } = new List<RegistrySetting>();
    
    // Linked settings configuration
    public LinkedSettingsLogic LinkedSettingsLogic { get; init; } = LinkedSettingsLogic.Any;

    // Dependencies between settings
    public List<SettingDependency> Dependencies { get; init; } = new List<SettingDependency>();

    public ControlType ControlType { get; init; } = ControlType.BinaryToggle;  // Default to binary toggle
    public int? SliderSteps { get; init; }  // For discrete sliders (null for binary toggles)
    public bool IsEnabled { get; init; }  // Current state
}

public record OptimizationGroup
{
    public required string Name { get; init; }
    public required OptimizationCategory Category { get; init; }
    public required IReadOnlyList<Winhance.Core.Features.Optimize.Models.OptimizationSetting> Settings { get; init; }
}

/// <summary>
/// Represents a dependency between two settings.
/// </summary>
/// <remarks>
/// For example, "Improve Inking and Typing" requires "Send Diagnostic Data" to be enabled.
/// </remarks>
/// <example>
/// new SettingDependency
/// {
///     DependencyType = SettingDependencyType.RequiresEnabled,
///     DependentSettingId = "privacy-improve-inking-typing-user",
///     RequiredSettingId = "privacy-diagnostics-policy"
/// }
/// </example>
public record SettingDependency
{
    /// <summary>
    /// The type of dependency.
    /// </summary>
    public SettingDependencyType DependencyType { get; init; }

    /// <summary>
    /// The ID of the setting that depends on another setting.
    /// </summary>
    public required string DependentSettingId { get; init; }

    /// <summary>
    /// The ID of the setting that is required by the dependent setting.
    /// </summary>
    public required string RequiredSettingId { get; init; }
}

/// <summary>
/// The type of dependency between two settings.
/// </summary>
public enum SettingDependencyType
{
    /// <summary>
    /// The dependent setting requires the required setting to be enabled.
    /// </summary>
    RequiresEnabled,

    /// <summary>
    /// The dependent setting requires the required setting to be disabled.
    /// </summary>
    RequiresDisabled
}
