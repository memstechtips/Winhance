using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;

namespace Winhance.UI.Features.Optimize.ViewModels;

/// <summary>
/// Computes status banner messages for setting items (compatibility warnings,
/// option warnings, cross-group info, restart required).
/// </summary>
internal sealed class SettingStatusBannerManager
{
    private readonly ILocalizationService _localizationService;

    internal readonly record struct BannerState(string? Message, InfoBarSeverity Severity)
    {
        public static BannerState Clear => new(null, InfoBarSeverity.Informational);
    }

    public SettingStatusBannerManager(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    /// <summary>
    /// Gets the compatibility banner from the setting definition, if present.
    /// </summary>
    public BannerState? GetCompatibilityBanner(SettingDefinition? definition)
    {
        if (definition?.VersionCompatibilityMessage is { } messageText)
        {
            return new BannerState(messageText, InfoBarSeverity.Warning);
        }
        return null;
    }

    /// <summary>
    /// Computes the appropriate banner for a value change.
    /// Returns null when the banner should not be changed (keep existing state).
    /// </summary>
    public BannerState? ComputeBannerForValue(
        SettingDefinition? definition, object? value, string? crossGroupInfoMessage)
    {
        if (definition == null || value is not int selectedIndex)
        {
            // Keep existing compatibility banner if present, otherwise clear
            if (definition?.VersionCompatibilityMessage == null)
            {
                return BannerState.Clear;
            }
            return null; // Don't change (keep existing compatibility banner)
        }

        // Check for option-specific warnings (e.g., update policy security warnings)
        if (definition.ComboBox?.OptionWarnings is { } warningDict &&
            warningDict.TryGetValue(selectedIndex, out var warning))
        {
            return new BannerState(warning, InfoBarSeverity.Error);
        }

        // Check for cross-group child settings info (promotional banner)
        if (definition.CrossGroupChildSettings != null)
        {
            return ComputeCrossGroupBanner(definition, selectedIndex, crossGroupInfoMessage);
        }

        // No option-specific warning - check for compatibility message
        if (definition.VersionCompatibilityMessage is { } compatText)
        {
            return new BannerState(compatText, InfoBarSeverity.Warning);
        }

        return BannerState.Clear;
    }

    /// <summary>
    /// Gets a restart-required banner if the setting requires restart and has been changed.
    /// Returns null if no banner should be shown.
    /// </summary>
    public BannerState? GetRestartBanner(SettingDefinition? definition, bool hasChangedThisSession)
    {
        if (!hasChangedThisSession) return null;
        if (definition?.RequiresRestart != true) return null;

        return new BannerState(
            _localizationService.GetString("Common_RestartRequired"),
            InfoBarSeverity.Warning);
    }

    private BannerState ComputeCrossGroupBanner(
        SettingDefinition definition, int selectedIndex, string? crossGroupInfoMessage)
    {
        var displayNames = definition.ComboBox?.DisplayNames;

        if (displayNames == null)
            return BannerState.Clear;

        // Check if "Custom" option is selected (last index or special custom state index)
        var customOptionIndex = displayNames.Length - 1;
        bool isCustomState = selectedIndex == customOptionIndex ||
            selectedIndex == ComboBoxConstants.CustomStateIndex;

        if (!isCustomState)
            return BannerState.Clear;

        // Use the pre-built message if available (built during initialization with full grouping)
        if (!string.IsNullOrEmpty(crossGroupInfoMessage))
            return new BannerState(crossGroupInfoMessage, InfoBarSeverity.Warning);

        // Fallback: just show the header if pre-built message not available
        var header = _localizationService.GetString("Setting_CrossGroupWarning_Header");
        if (!string.IsNullOrEmpty(header))
            return new BannerState(header, InfoBarSeverity.Warning);

        return BannerState.Clear;
    }
}
