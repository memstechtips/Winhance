namespace Winhance.Core.Features.Common.Enums;

public enum RegistrySettingStatus
{
    Unknown,           // Status couldn't be determined
    NotApplied,        // Registry key doesn't exist or has default value
    Applied,           // Current value matches recommended value
    Modified,          // Value exists but doesn't match recommended or default
    Error              // Error occurred while checking status
}
