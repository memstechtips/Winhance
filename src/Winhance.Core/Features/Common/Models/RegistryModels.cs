using Microsoft.Win32;
using System;
using Winhance.Core.Features.Common.Enums;

namespace Winhance.Core.Features.Common.Models;

public record RegistrySetting
{
    public required string Category { get; init; }
    public required RegistryHive Hive { get; init; }
    public required string SubKey { get; init; }
    public required string Name { get; init; }

    /// <summary>
    /// The value shown in the tooltip to indicate the recommended value.
    /// </summary>
    public required object RecommendedValue { get; init; }

    /// <summary>
    /// The value shown in the tooltip to indicate the default value. (or null to delete the registry key).
    /// </summary>
    public object? DefaultValue { get; init; }

    /// <summary>
    /// The value to set when the toggle is ON (enabled).
    /// </summary>
    public object? EnabledValue { get; init; }

    /// <summary>
    /// The value to set when the toggle is OFF (disabled), or null to delete the registry key.
    /// </summary>
    public object? DisabledValue { get; init; }

    public required RegistryValueKind ValueType { get; init; }
    /// <summary>
    /// Description of the registry setting. This is optional as the parent OptimizationSetting
    /// typically already has a Description property that serves the same purpose.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Specifies the intended action for this registry setting.
    /// Default is Set for backward compatibility.
    /// </summary>
    public RegistryActionType ActionType { get; init; } = RegistryActionType.Set;

    /// <summary>
    /// Specifies whether the absence of the registry key/value means the feature is enabled.
    /// Default is false, meaning absence = not applied.
    /// When true, absence of the key/value means the feature is enabled (Applied).
    /// </summary>
    public bool AbsenceMeansEnabled { get; init; } = false;

    /// <summary>
    /// Specifies whether this setting is a primary setting when used in linked settings.
    /// For settings with LinkedSettingsLogic.Primary, only the primary setting's status is used.
    /// </summary>
    public bool IsPrimary { get; init; } = false;

    /// <summary>
    /// Specifies whether this setting is a Group Policy registry key.
    /// When true, the entire key will be deleted when disabling the setting instead of just changing the value.
    /// This is necessary for Windows to recognize that the policy is no longer applied.
    /// </summary>
    public bool IsGroupPolicy { get; init; } = false;
    
    /// <summary>
    /// Dictionary to store custom properties for specific control types like ComboBox.
    /// This allows storing additional information that doesn't fit into the standard registry setting model.
    /// </summary>
    public Dictionary<string, object>? CustomProperties { get; set; }
    
    /// <summary>
    /// Specifies whether this setting represents a GUID subkey that should be created or deleted.
    /// When true, the Name property is treated as a subkey name rather than a value name.
    /// This is used for special registry settings like Explorer namespace GUIDs.
    /// </summary>
    public bool IsGuidSubkey { get; init; } = false;
}

public enum RegistryAction
{
    Apply,
    Test,
    Rollback
}

public record ValuePair
{
    public required object Value { get; init; }
    public required RegistryValueKind Type { get; init; }

    public ValuePair(object value, RegistryValueKind type)
    {
        Value = value;
        Type = type;
    }
}
