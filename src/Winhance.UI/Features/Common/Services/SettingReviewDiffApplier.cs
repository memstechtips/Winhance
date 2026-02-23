using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Events;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Common.Interfaces;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Services;

/// <summary>
/// Applies review-mode diff state to a SettingItemViewModel by checking
/// for eagerly-computed diffs or falling back to on-the-fly diff computation.
/// </summary>
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

    /// <inheritdoc />
    public void ApplyReviewDiffToViewModel(SettingItemViewModel viewModel, SettingStateResult currentState)
    {
        var config = _configReviewModeService.ActiveConfig;
        if (config == null) return;

        viewModel.IsInReviewMode = true;

        // Check if an eager diff already exists from ConfigReviewService
        var existingDiff = _configReviewDiffService.GetDiffForSetting(viewModel.SettingId);
        if (existingDiff != null)
        {
            // Use the pre-computed diff
            if (existingDiff.IsActionSetting && !string.IsNullOrEmpty(existingDiff.ActionConfirmationMessage))
            {
                // Action settings show their custom confirmation message
                viewModel.HasReviewDiff = true;
                viewModel.ReviewDiffMessage = existingDiff.ActionConfirmationMessage;
            }
            else
            {
                var diffFormat = _localizationService.GetString("Review_Mode_Diff_Toggle") ?? "Current: {0} \u2192 Config: {1}";
                viewModel.HasReviewDiff = true;
                viewModel.ReviewDiffMessage = string.Format(diffFormat, existingDiff.CurrentValueDisplay, existingDiff.ConfigValueDisplay);
            }

            // Restore review decision state
            if (existingDiff.IsReviewed)
            {
                if (existingDiff.IsApproved)
                    viewModel.IsReviewApproved = true;
                else
                    viewModel.IsReviewRejected = true;
            }

            // Subscribe to approval changes from this ViewModel
            viewModel.ReviewApprovalChanged += (sender, approved) =>
            {
                _configReviewDiffService.SetSettingApproval(viewModel.SettingId, approved);
            };
            return;
        }

        // No eager diff exists - setting not in config or no change
        // Find this setting in the config to determine if it's in the config at all
        var (configItem, featureModuleId) = FindConfigItemForSetting(viewModel.SettingId, config);
        if (configItem == null)
        {
            // Setting not in config - just mark as in review mode (controls disabled)
            return;
        }

        // Compute diff based on input type (fallback for settings not caught by eager computation)
        var (hasDiff, currentDisplay, configDisplay) = ComputeDiff(viewModel, configItem, currentState);

        if (hasDiff)
        {
            var diffFormat = _localizationService.GetString("Review_Mode_Diff_Toggle") ?? "Current: {0} \u2192 Config: {1}";
            viewModel.HasReviewDiff = true;
            viewModel.ReviewDiffMessage = string.Format(diffFormat, currentDisplay, configDisplay);
            viewModel.IsReviewApproved = false;

            // Register the diff with the service for tracking
            var diff = new ConfigReviewDiff
            {
                SettingId = viewModel.SettingId,
                SettingName = viewModel.Name,
                FeatureModuleId = featureModuleId ?? string.Empty,
                CurrentValueDisplay = currentDisplay,
                ConfigValueDisplay = configDisplay,
                ConfigItem = configItem,
                IsApproved = false,
                InputType = viewModel.InputType
            };
            _configReviewDiffService.RegisterDiff(diff);

            // Subscribe to approval changes from this ViewModel
            viewModel.ReviewApprovalChanged += (sender, approved) =>
            {
                _configReviewDiffService.SetSettingApproval(viewModel.SettingId, approved);
            };
        }
    }

    private (ConfigurationItem? item, string? featureId) FindConfigItemForSetting(string settingId, UnifiedConfigurationFile config)
    {
        // Search in Optimize features
        foreach (var feature in config.Optimize.Features)
        {
            var item = feature.Value.Items.FirstOrDefault(i => i.Id == settingId);
            if (item != null) return (item, feature.Key);
        }

        // Search in Customize features
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
        var onText = _localizationService.GetString("Common_On") ?? "On";
        var offText = _localizationService.GetString("Common_Off") ?? "Off";

        switch (viewModel.InputType)
        {
            case InputType.Toggle:
            case InputType.CheckBox:
            {
                var currentBool = currentState.IsEnabled;
                var configBool = configItem.IsSelected ?? false;
                if (currentBool != configBool)
                {
                    return (true, currentBool ? onText : offText, configBool ? onText : offText);
                }
                return (false, string.Empty, string.Empty);
            }

            case InputType.Selection:
            {
                // Compare by SelectedIndex
                var currentIndex = viewModel.SelectedValue is int idx ? idx : -1;

                // Special handling: CustomStateValues or PowerPlan means "custom" config
                if (configItem.CustomStateValues != null || configItem.PowerPlanGuid != null)
                {
                    var currentDisplayName = GetComboBoxDisplayName(viewModel, currentIndex);
                    var configDisplayName = configItem.PowerPlanName ?? "Custom";
                    if (!string.Equals(currentDisplayName, configDisplayName, StringComparison.OrdinalIgnoreCase))
                        return (true, currentDisplayName, configDisplayName);
                    return (false, string.Empty, string.Empty);
                }

                // If config has no SelectedIndex, skip diff for this setting
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

            case InputType.NumericRange:
            {
                var currentVal = currentState.CurrentValue is int cv ? cv : viewModel.NumericValue;
                // For numeric range, the config value might be in PowerSettings or a direct value
                // Try to extract from the config item
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
