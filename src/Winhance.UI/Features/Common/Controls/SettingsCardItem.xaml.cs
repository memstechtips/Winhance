using System.ComponentModel;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;
using Winhance.Core.Features.Common.Interfaces;
using Winhance.Core.Features.Common.Models;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.Controls;

/// <summary>
/// A reusable UserControl that displays a SettingItemViewModel in a SettingsCard
/// with the appropriate control based on the InputType.
/// </summary>
public sealed partial class SettingsCardItem : UserControl
{
    public static readonly DependencyProperty SettingProperty =
        DependencyProperty.Register(
            nameof(Setting),
            typeof(SettingItemViewModel),
            typeof(SettingsCardItem),
            new PropertyMetadata(null, OnSettingChanged));

    public SettingItemViewModel? Setting
    {
        get => (SettingItemViewModel?)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    private SettingItemViewModel? _subscribedSetting;

    public SettingsCardItem()
    {
        this.InitializeComponent();
        Unloaded += (_, _) => UnsubscribeFromSetting();
    }

    private static void OnSettingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SettingsCardItem control)
        {
            control.UnsubscribeFromSetting();
            if (e.NewValue is SettingItemViewModel vm)
            {
                control._subscribedSetting = vm;
                vm.PropertyChanged += control.OnSettingPropertyChanged;
            }
        }
    }

    private void UnsubscribeFromSetting()
    {
        if (_subscribedSetting != null)
        {
            _subscribedSetting.PropertyChanged -= OnSettingPropertyChanged;
            _subscribedSetting = null;
        }
    }

    private void OnSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not SettingItemViewModel vm) return;

        if (e.PropertyName == nameof(SettingItemViewModel.IsApplying))
        {
            var peer = FrameworkElementAutomationPeer.FromElement(this)
                       ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);

            if (vm.IsApplying)
            {
                peer?.RaiseNotificationEvent(
                    AutomationNotificationKind.ActionCompleted,
                    AutomationNotificationProcessing.ImportantMostRecent,
                    $"Applying {vm.Name}",
                    "SettingApplying");
            }
            else
            {
                var stateText = GetSettingStateText(vm);
                peer?.RaiseNotificationEvent(
                    AutomationNotificationKind.ActionCompleted,
                    AutomationNotificationProcessing.ImportantMostRecent,
                    $"{vm.Name} is now {stateText}",
                    "SettingApplied");
            }
        }
        else if (e.PropertyName == nameof(SettingItemViewModel.IsReviewApproved) && vm.IsReviewApproved)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(this)
                       ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);
            peer?.RaiseNotificationEvent(
                AutomationNotificationKind.ActionCompleted,
                AutomationNotificationProcessing.ImportantMostRecent,
                $"{vm.Name}: Apply",
                "ReviewApproved");
        }
        else if (e.PropertyName == nameof(SettingItemViewModel.IsReviewRejected) && vm.IsReviewRejected)
        {
            var peer = FrameworkElementAutomationPeer.FromElement(this)
                       ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);
            peer?.RaiseNotificationEvent(
                AutomationNotificationKind.ActionCompleted,
                AutomationNotificationProcessing.ImportantMostRecent,
                $"{vm.Name}: Don't apply",
                "ReviewRejected");
        }
    }

    private static string GetSettingStateText(SettingItemViewModel vm)
    {
        return vm.InputType switch
        {
            InputType.Toggle or InputType.CheckBox => vm.IsSelected ? vm.OnText : vm.OffText,
            InputType.Selection => vm.ComboBoxOptions
                ?.FirstOrDefault(o => Equals(o.Value, vm.SelectedValue))?.DisplayText
                ?? vm.SelectedValue?.ToString() ?? "changed",
            InputType.NumericRange => vm.NumericValue.ToString(),
            _ => "applied"
        };
    }

    /// <summary>
    /// Handles the Loaded event for PowerPlanComboBox to set up event handlers and localized text.
    /// </summary>
    private void OnPowerPlanComboBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not PowerPlanComboBox comboBox)
            return;

        // Get the SettingItemViewModel from the Tag
        var settingVm = comboBox.Tag as SettingItemViewModel;
        if (settingVm == null)
            return;

        // Get the parent PowerOptimizationsViewModel
        var powerViewModel = settingVm.ParentFeatureViewModel as PowerOptimizationsViewModel;

        // Set up localized text using the localization service
        try
        {
            var localizationService = App.Services.GetService<ILocalizationService>();
            if (localizationService != null)
            {
                comboBox.ActiveBadgeText = localizationService.GetString("PowerPlan_Active_Badge");
                comboBox.DeleteTooltipText = localizationService.GetString("PowerPlan_Delete_Tooltip");
                comboBox.ExistsTooltipText = localizationService.GetString("PowerPlan_Status_Exists");
                comboBox.NotExistsTooltipText = localizationService.GetString("PowerPlan_Status_NotExists");
            }
        }
        catch
        {
            // Use default values if localization service is unavailable
        }

        LogDebug($"[SettingsCardItem] Wiring up PowerPlanComboBox events for setting {settingVm.SettingId}");

        // Wire up the DeleteRequested event to the parent ViewModel's command
        comboBox.DeleteRequested += (s, plan) =>
        {
            LogDebug($"[SettingsCardItem] DeleteRequested fired for plan {plan?.DisplayName}");
            powerViewModel?.DeletePowerPlanCommand.Execute(plan);
        };

        // Wire up the DropDownClosed event to handle selection changes
        comboBox.DropDownClosed += (s, value) =>
        {
            LogDebug($"[SettingsCardItem] DropDownClosed received, value={value}, calling ApplySelectionValue");
            settingVm.ApplySelectionValue(value);
        };
    }

    private static void LogDebug(string message)
    {
        try { App.Services.GetService<ILogService>()?.LogDebug(message); } catch { }
    }
}
