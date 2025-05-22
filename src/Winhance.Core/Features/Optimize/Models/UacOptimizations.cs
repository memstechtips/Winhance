using System.Collections.Generic;
using Microsoft.Win32;
using Winhance.Core.Models.Enums;

namespace Winhance.Core.Features.Optimize.Models;

public static class UacOptimizations
{
    public const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
    public const string ConsentPromptName = "ConsentPromptBehaviorAdmin";
    public const string SecureDesktopName = "PromptOnSecureDesktop";
    public static readonly RegistryValueKind ValueKind = RegistryValueKind.DWord;

    // UAC settings require two registry values working together
    // ConsentPromptBehaviorAdmin controls the behavior type
    // PromptOnSecureDesktop controls whether the desktop is dimmed
    
    // Map from UacLevel enum to ConsentPromptBehaviorAdmin registry values
    public static readonly Dictionary<UacLevel, int> UacLevelToConsentPromptValue = new()
    {
        { UacLevel.NeverNotify, 0 },       // Never notify
        { UacLevel.NotifyNoDesktopDim, 5 }, // Notify without dimming desktop
        { UacLevel.NotifyChangesOnly, 5 },  // Notify only for changes (default)
        { UacLevel.AlwaysNotify, 2 },       // Always notify
    };
    
    // Map from UacLevel enum to PromptOnSecureDesktop registry values
    public static readonly Dictionary<UacLevel, int> UacLevelToSecureDesktopValue = new()
    {
        { UacLevel.NeverNotify, 0 },       // Secure desktop disabled
        { UacLevel.NotifyNoDesktopDim, 0 }, // Secure desktop disabled
        { UacLevel.NotifyChangesOnly, 1 },  // Secure desktop enabled
        { UacLevel.AlwaysNotify, 1 },       // Secure desktop enabled
    };

    // User-friendly names for each UAC level
    public static readonly Dictionary<UacLevel, string> UacLevelNames = new()
    {
        { UacLevel.AlwaysNotify, "Always notify" },
        { UacLevel.NotifyChangesOnly, "Notify when apps try to make changes" },
        { UacLevel.NotifyNoDesktopDim, "Notify when apps try to make changes (no dim)" },
        { UacLevel.NeverNotify, "Never notify" },
    };

    // Helper method to get UacLevel from both registry values
    public static UacLevel GetUacLevelFromRegistryValues(int consentPromptValue, int secureDesktopValue)
    {
        // Check for exact matches of both values
        foreach (var level in UacLevelToConsentPromptValue.Keys)
        {
            if (UacLevelToConsentPromptValue[level] == consentPromptValue && 
                UacLevelToSecureDesktopValue[level] == secureDesktopValue)
            {
                return level;
            }
        }
        
        // If no exact match, determine the closest match based on the combination
        if (consentPromptValue == 0)
        {
            return UacLevel.NeverNotify; // ConsentPrompt=0 always means Never Notify
        }
        else if (consentPromptValue == 5)
        {
            // ConsentPrompt=5 with SecureDesktop determines dimming
            return secureDesktopValue == 0 ? UacLevel.NotifyNoDesktopDim : UacLevel.NotifyChangesOnly;
        }
        else if (consentPromptValue == 2)
        {
            return UacLevel.AlwaysNotify; // ConsentPrompt=2 is Always Notify
        }
        
        // Default to NotifyChangesOnly if values don't match any known combination
        return UacLevel.NotifyChangesOnly;
    }
}
