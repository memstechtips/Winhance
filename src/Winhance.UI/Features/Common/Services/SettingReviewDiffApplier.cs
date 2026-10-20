using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Localization;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;
using Winhance.Core.Features.Common.Extensions;

namespace Winhance.UI.Features.Common.Services;

public class SettingReviewDiffApplier : ISettingReviewDiffApplier
{
    private readonly IConfigReviewModeService _configReviewModeService;
    private readonly IConfigReviewDiffService _configReviewDiffService;
    private readonly ILocalizationService _localizationService;

    public SettingReviewDiffApplier(
        IConfigReviewModeService configReviewModeService,
        IConfigReviewDiffService configReviewDiffService,
        ILocalizationService localizationService)
    {
        _configReviewModeService = configReviewModeService;
        _configReviewDiffService = configReviewDiffService;
        _localizationService = localizationService;
    }

    public void ApplyReviewDiffToViewModel(SettingItemViewModel viewModel, SettingStateResult currentState)
    {
        var config = _configReviewModeService.ActiveConfig;
        if (config == null) return;

        viewModel.IsInReviewMode = true;

        var existingDiff = _configReviewDiffService.GetDiffForSetting(viewModel.SettingId);
        if (existingDiff != null)
        {
            bool hasDiffValues = !string.IsNullOrEmpty(existingDiff.CurrentValueDisplay) && !string.IsNullOrEmpty(existingDiff.ConfigValueDisplay);
            bool hasAction = existingDiff.IsActionSetting && !string.IsNullOrEmpty(existingDiff.ActionConfirmationMessage);

            if (hasDiffValues)
            {
                var diffFormat = _localizationService.GetStringOrDefault("Review_Mode_Diff_Toggle", "Current: {0} \u2192 Config: {1}");
                viewModel.HasReviewDiff = true;
                viewModel.ReviewDiffMessage = string.Format(diffFormat, existingDiff.CurrentValueDisplay, existingDiff.ConfigValueDisplay);
            }

            if (hasAction && hasDiffValues)
            {
                // Show action as a separate infobar alongside the diff
                viewModel.HasReviewAction = true;
                viewModel.ReviewActionMessage = existingDiff.ActionConfirmationMessage;
            }
            else if (hasAction)
            {
                // Action-only settings (no value diff) show in the primary infobar
                viewModel.HasReviewDiff = true;
                viewModel.ReviewDiffMessage = existingDiff.ActionConfirmationMessage;
            }

            if (existingDiff.IsReviewed)
            {
                if (existingDiff.IsApproved)
                    viewModel.IsReviewApproved = true;
                else
                    viewModel.IsReviewRejected = true;
            }

            if (existingDiff.IsActionReviewed)
            {
                if (existingDiff.IsActionApproved)
                    viewModel.IsReviewActionApproved = true;
                else
                    viewModel.IsReviewActionRejected = true;
            }

            viewModel.ReviewApprovalChanged += (sender, approved) =>
            {
                _configReviewDiffService.SetSettingApproval(viewModel.SettingId, approved);
            };

            viewModel.ReviewActionApprovalChanged += (sender, approved) =>
            {
                _configReviewDiffService.SetActionApproval(viewModel.SettingId, approved);
            };
            return;
        }

        var (configItem, featureModuleId) = FindConfigItemForSetting(viewModel.SettingId, config);
        if (configItem == null)
        {
            // Setting not in config - just mark as in review mode (controls disabled)
            return;
        }

        var (hasDiff, currentDisplay, configDisplay) = ComputeDiff(viewModel, configItem, currentState);

        if (hasDiff)
        {
            var diffFormat = _localizationService.GetStringOrDefault("Review_Mode_Diff_Toggle", "Current: {0} \u2192 Config: {1}");
            viewModel.HasReviewDiff = true;
            viewModel.ReviewDiffMessage = string.Format(diffFormat, currentDisplay, configDisplay);
            viewModel.IsReviewApproved = false;

            var diff = new ConfigReviewDiff
            {
                SettingId = viewModel.SettingId,
                SettingName = viewModel.Name,
                FeatureModuleId = featureModuleId ?? string.Empty,
                CurrentValueDisplay = currentDisplay,
                ConfigValueDisplay = configDisplay,
                ConfigItem = configItem,
                IsApproved = false
            };
            _configReviewDiffService.RegisterDiff(diff);

            viewModel.ReviewApprovalChanged += (sender, approved) =>
            {
                _configReviewDiffService.SetSettingApproval(viewModel.SettingId, approved);
            };
        }
    }

    private (ConfigurationItem? item, string? featureId) FindConfigItemForSetting(string settingId, WinhanceConfigFile config)
    {
        foreach (var feature in config.Optimize.Features)
        {
            var item = feature.Value.Items.FirstOrDefault(i => i.Id == settingId);
            if (item != null) return (item, feature.Key);
        }

        foreach (var feature in config.Customize.Features)
        {
            var item = feature.Value.Items.FirstOrDefault(i => i.Id == settingId);
            if (item != null) return (item, feature.Key);
        }

        return (null, null);
    }

    private (bool hasDiff, string currentDisplay, string configDisplay) ComputeDiff(
        SettingItemViewModel viewModel,
        ConfigurationItem configItem,
        SettingStateResult currentState)
    {
        switch (viewModel.InputType)
        {
            case InputType.Toggle:
            case InputType.CheckBox:
            {
                var currentBool = currentState.IsEnabled;
                var configBool = configItem.IsSelected ?? false;
                if (currentBool != configBool)
                {
                    return (true,
                        currentBool ? viewModel.OnText : viewModel.OffText,
                        configBool ? viewModel.OnText : viewModel.OffText);
                }
                return (false, string.Empty, string.Empty);
            }

            case InputType.Selection:
            {
                var currentIndex = viewModel.SelectedValue is int idx ? idx : -1;

                // A keyed card's SelectedValue is the key, so currentIndex is -1; ConfigReviewService compared the keys.
                // Gated on the card, not the item, so a legacy SelectedIndex for a now-keyed setting stops here too.
                if (viewModel.IsKeyedSelection)
                    return (false, string.Empty, string.Empty);

                if (configItem.CustomStateValues != null)
                {
                    var currentDisplayName = GetComboBoxDisplayName(viewModel, currentIndex);
                    var configDisplayName = _localizationService.GetStringOrDefault(LocKey.Common.CustomState.Value, "Custom");
                    if (!string.Equals(currentDisplayName, configDisplayName, StringComparison.OrdinalIgnoreCase))
                        return (true, currentDisplayName, configDisplayName);
                    return (false, string.Empty, string.Empty);
                }

                if (configItem.SelectedIndex == null)
                    return (false, string.Empty, string.Empty);

                var configIndex = configItem.SelectedIndex.Value;

                if (currentIndex != configIndex)
                {
                    var currentDisplayName = GetComboBoxDisplayName(viewModel, currentIndex);
                    var configDisplayName = GetComboBoxDisplayName(viewModel, configIndex);
                    return (true, currentDisplayName, configDisplayName);
                }
                return (false, string.Empty, string.Empty);
            }

            case InputType.TextBox:
            {
                // A missing Text is no answer, an empty one is; the card draws the diff only when both sides are non-empty.
                if (configItem.Text is not { } configText)
                    return (false, string.Empty, string.Empty);

                var currentText = currentState.CurrentValue as string ?? viewModel.TextValue;
                if (string.Equals(currentText, configText, StringComparison.Ordinal))
                    return (false, string.Empty, string.Empty);

                var unknown = _localizationService.GetStringOrDefault("ConfigReview_UnknownValue", "Unknown");
                return (true,
                    currentText.Length > 0 ? currentText : unknown,
                    configText.Length > 0 ? configText : unknown);
            }

            case InputType.NumericRange:
            {
                var currentVal = currentState.CurrentValue is int cv ? cv : viewModel.NumericValue;
                if (configItem.PowerSettings != null)
                {
                    // Power settings have AC/DC values - show AC for display
                    if (configItem.PowerSettings.TryGetValue("ACValue", out var acVal) && acVal is int acInt)
                    {
                        if (currentVal != acInt)
                            return (true, currentVal.ToString(), acInt.ToString());
                    }
                }
                return (false, string.Empty, string.Empty);
            }

            default:
                return (false, string.Empty, string.Empty);
        }
    }

    private static string GetComboBoxDisplayName(SettingItemViewModel viewModel, int index)
    {
        if (index >= 0 && index < viewModel.ComboBoxOptions.Count)
        {
            return viewModel.ComboBoxOptions[index].DisplayText ?? index.ToString();
        }
        return index.ToString();
    }
}
