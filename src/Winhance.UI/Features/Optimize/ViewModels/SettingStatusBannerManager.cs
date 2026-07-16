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
    /// Computes the appropriate banner for a value change. Returns BannerState.Clear when there is no banner to
    /// show, or null to leave an existing compatibility banner untouched (value is not an int index).
    /// </summary>
    public BannerState? ComputeBannerForValue(
        object? value, IReadOnlyList<string?>? optionWarnings, string? crossGroupInfoMessage, int optionCount, string? compatibilityMessage)
    {
        if (value is not int selectedIndex)
        {
            // Keep an existing compatibility banner (return null = don't change) when one applies; otherwise clear.
            if (string.IsNullOrEmpty(compatibilityMessage))
                return BannerState.Clear;
            return null;
        }

        // Per-option warning text (e.g., update policy security warnings), index-aligned with the options.
        if (optionWarnings is { } w
            && selectedIndex >= 0 && selectedIndex < w.Count
            && w[selectedIndex] is { } warning)
        {
            return new BannerState(warning, InfoBarSeverity.Error);
        }

        // Cross-group child settings info (promotional banner). The precomputed message already includes the header.
        if (!string.IsNullOrEmpty(crossGroupInfoMessage))
            return ComputeCrossGroupBanner(selectedIndex, crossGroupInfoMessage, optionCount);

        // Windows-version compatibility message (shown when the version filter is off).
        if (!string.IsNullOrEmpty(compatibilityMessage))
            return new BannerState(compatibilityMessage, InfoBarSeverity.Warning);

        return BannerState.Clear;
    }

    /// <summary>
    /// Gets a restart-required banner if the setting requires restart and has been changed.
    /// Returns null if no banner should be shown.
    /// </summary>
    public BannerState? GetRestartBanner(bool requiresRestart, bool hasChangedThisSession)
    {
        if (!hasChangedThisSession) return null;
        if (!requiresRestart) return null;

        return new BannerState(
            _localizationService.GetString("Common_RestartRequired"),
            InfoBarSeverity.Warning);
    }

    private BannerState ComputeCrossGroupBanner(int selectedIndex, string crossGroupInfoMessage, int optionCount)
    {
        if (optionCount == 0)
            return BannerState.Clear;

        // Check if "Custom" option is selected (last index or special custom state index)
        var customOptionIndex = optionCount - 1;
        bool isCustomState = selectedIndex == customOptionIndex ||
            selectedIndex == ComboBoxConstants.CustomStateIndex;

        if (!isCustomState)
            return BannerState.Clear;

        return new BannerState(crossGroupInfoMessage, InfoBarSeverity.Warning);
    }
}
