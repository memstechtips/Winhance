using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Win32;
using Winhance.Core.Features.Common.Interfaces;
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
        { UacLevel.NeverNotify, 0 }, // Never notify
        { UacLevel.NotifyNoDesktopDim, 5 }, // Notify without dimming desktop
        { UacLevel.NotifyChangesOnly, 5 }, // Notify only for changes (default)
        { UacLevel.AlwaysNotify, 2 }, // Always notify
    };

    // Map from UacLevel enum to PromptOnSecureDesktop registry values
    public static readonly Dictionary<UacLevel, int> UacLevelToSecureDesktopValue = new()
    {
        { UacLevel.NeverNotify, 0 }, // Secure desktop disabled
        { UacLevel.NotifyNoDesktopDim, 0 }, // Secure desktop disabled
        { UacLevel.NotifyChangesOnly, 1 }, // Secure desktop enabled
        { UacLevel.AlwaysNotify, 1 }, // Secure desktop enabled
    };

    // User-friendly names for each UAC level
    public static readonly Dictionary<UacLevel, string> UacLevelNames = new()
    {
        { UacLevel.AlwaysNotify, "Always notify" },
        { UacLevel.NotifyChangesOnly, "Notify when apps try to make changes" },
        { UacLevel.NotifyNoDesktopDim, "Notify when apps try to make changes (no dim)" },
        { UacLevel.NeverNotify, "Never notify" },
        { UacLevel.Custom, "Custom UAC Setting" },
    };

    /// <summary>
    /// Helper method to get UacLevel from both registry values
    /// </summary>
    /// <param name="consentPromptValue">The ConsentPromptBehaviorAdmin registry value</param>
    /// <param name="secureDesktopValue">The PromptOnSecureDesktop registry value</param>
    /// <param name="uacSettingsService">Optional UAC settings service to save custom values</param>
    /// <returns>The corresponding UacLevel</returns>
    public static UacLevel GetUacLevelFromRegistryValues(
        int consentPromptValue,
        int secureDesktopValue,
        IUacSettingsService uacSettingsService = null
    )
    {
        // Check for exact matches of both values
        foreach (var level in UacLevelToConsentPromptValue.Keys)
        {
            if (
                UacLevelToConsentPromptValue[level] == consentPromptValue
                && UacLevelToSecureDesktopValue[level] == secureDesktopValue
            )
            {
                return level;
            }
        }

        // If no exact match, determine if it's one of the common non-standard combinations
        if (consentPromptValue == 0)
        {
            return UacLevel.NeverNotify; // ConsentPrompt=0 always means Never Notify
        }
        else if (consentPromptValue == 5)
        {
            // ConsentPrompt=5 with SecureDesktop determines dimming
            return secureDesktopValue == 0
                ? UacLevel.NotifyNoDesktopDim
                : UacLevel.NotifyChangesOnly;
        }
        else if (consentPromptValue == 2)
        {
            return UacLevel.AlwaysNotify; // ConsentPrompt=2 is Always Notify
        }

        // If we get here, we have a custom UAC setting
        // Save the custom values if we have a service
        if (uacSettingsService != null)
        {
            // Save asynchronously - fire and forget
            _ = Task.Run(() => uacSettingsService.SaveCustomUacSettingsAsync(consentPromptValue, secureDesktopValue));
        }
        
        return UacLevel.Custom;
    }
}
