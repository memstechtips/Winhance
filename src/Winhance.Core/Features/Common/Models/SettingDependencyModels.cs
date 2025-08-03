using System.Collections.Generic;

namespace Winhance.Core.Features.Common.Models;

/// <summary>
/// Represents a dependency between two settings.
/// </summary>
/// <remarks>
/// For example, "Improve Inking and Typing" requires "Send Diagnostic Data" to be enabled.
/// Cross-module dependencies are supported by specifying the RequiredModule.
/// ComboBox value dependencies are supported by specifying RequiredValue.
/// </remarks>
/// <example>
/// new SettingDependency
/// {
///     DependencyType = SettingDependencyType.RequiresEnabled,
///     DependentSettingId = "privacy-improve-inking-typing-user",
///     RequiredSettingId = "privacy-diagnostics-policy"
/// }
/// // Cross-module dependency example:
/// new SettingDependency
/// {
///     DependencyType = SettingDependencyType.RequiresEnabled,
///     DependentSettingId = "make-taskbar-transparent",
///     RequiredSettingId = "theme-transparency",
///     RequiredModule = "WindowsThemeSettings"
/// }
/// // ComboBox value dependency example:
/// new SettingDependency
/// {
///     DependencyType = SettingDependencyType.RequiresDisabled,
///     DependentSettingId = "show-recently-added-apps",
///     RequiredSettingId = "recommended-section",
///     RequiredValue = "Show"
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

    /// <summary>
    /// The module/feature where the required setting is located.
    /// Used for cross-module dependencies. If null, assumes same module.
    /// Examples: "WindowsThemeSettings", "StartMenuCustomizations", "PrivacyOptimizations"
    /// </summary>
    public string? RequiredModule { get; init; }

    /// <summary>
    /// The specific value that the required setting must have (for ComboBox dependencies).
    /// If null, uses the standard enabled/disabled logic.
    /// Example: "Show" for recommended-section ComboBox
    /// </summary>
    public string? RequiredValue { get; init; }

    /// <summary>
    /// A user-friendly description of this dependency for display in dialogs.
    /// If null, a default description will be generated.
    /// </summary>
    public string? Description { get; init; }
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
    RequiresDisabled,

    /// <summary>
    /// The dependent setting requires the required setting to have a specific value.
    /// Used with RequiredValue property for ComboBox dependencies.
    /// </summary>
    RequiresSpecificValue
}
