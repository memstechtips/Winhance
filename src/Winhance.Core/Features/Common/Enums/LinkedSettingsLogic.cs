namespace Winhance.Core.Features.Common.Enums;

/// <summary>
/// Defines the logic to use when determining the status of linked registry settings.
/// </summary>
public enum LinkedSettingsLogic
{
    /// <summary>
    /// If any of the linked settings is applied, the entire setting is considered applied.
    /// </summary>
    Any,
    
    /// <summary>
    /// All linked settings must be applied for the entire setting to be considered applied.
    /// </summary>
    All,
    
    /// <summary>
    /// Only use the first (primary) setting to determine the status.
    /// </summary>
    Primary,
    
    /// <summary>
    /// Use a custom logic defined in the code.
    /// </summary>
    Custom
}
