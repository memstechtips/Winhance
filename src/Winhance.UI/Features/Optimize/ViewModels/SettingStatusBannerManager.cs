using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Constants;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;

namespace Winhance.UI.Features.Optimize.ViewModels;

internal sealed class SettingStatusBannerManager
{
    private readonly ILocalizationService _localizationService;

    // A non-null DetectionOutcome marks a detection-outcome banner, so the VM gives it that outcome's colour icon;
    // every other banner keeps InfoBar's native severity icon.
    internal readonly record struct BannerState(
        string? Message, InfoBarSeverity Severity, SettingDetectionOutcome? DetectionOutcome = null)
    {
        public static BannerState Clear => new(null, InfoBarSeverity.Informational);
    }

    public SettingStatusBannerManager(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    // BannerState.Clear when there is nothing to show; null leaves an existing compatibility banner untouched
    // (value is not an int index).
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

    public BannerState? GetRestartBanner(bool requiresRestart, bool hasChangedThisSession)
    {
        if (!hasChangedThisSession) return null;
        if (!requiresRestart) return null;

        return new BannerState(
            _localizationService.GetString("Common_RestartRequired"),
            InfoBarSeverity.Warning);
    }

    // Custom -> Informational (not a fault; the user can simply choose); Malformed -> Warning (a real but
    // self-repairing fault); Undetermined -> Error (WE failed to read it; the message points at the log rather than
    // pretending to offer a remedy). Severity deliberately matches the overlay icon colour so the two never contradict.
    public BannerState GetDetectionOutcomeBanner(SettingDetectionOutcome outcome, bool isToggleLike)
    {
        var (prefix, severity) = outcome switch
        {
            SettingDetectionOutcome.Malformed => ("Common_MalformedBanner_", InfoBarSeverity.Warning),
            SettingDetectionOutcome.Undetermined => ("Common_UndeterminedBanner_", InfoBarSeverity.Error),
            _ => ("Common_CustomBanner_", InfoBarSeverity.Informational),
        };

        return new BannerState(
            _localizationService.GetString(prefix + (isToggleLike ? "Toggle" : "Selection")),
            severity,
            DetectionOutcome: outcome);
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
