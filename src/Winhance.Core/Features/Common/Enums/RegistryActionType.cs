namespace Winhance.Core.Features.Common.Enums;

/// <summary>
/// Defines the intended action for a registry setting.
/// </summary>
public enum RegistryActionType
{
    /// <summary>
    /// The setting is intended to set a specific value.
    /// </summary>
    Set,
    
    /// <summary>
    /// The setting is intended to remove a key or value.
    /// </summary>
    Remove,
    
    /// <summary>
    /// The setting is intended to modify an existing value.
    /// </summary>
    Modify
}
