using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
            new PropertyMetadata(null));

    public SettingItemViewModel? Setting
    {
        get => (SettingItemViewModel?)GetValue(SettingProperty);
        set => SetValue(SettingProperty, value);
    }

    public SettingsCardItem()
    {
        this.InitializeComponent();
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

        LogToFile($"[SettingsCardItem] Wiring up PowerPlanComboBox events for setting {settingVm.SettingId}");

        // Wire up the DeleteRequested event to the parent ViewModel's command
        comboBox.DeleteRequested += (s, plan) =>
        {
            LogToFile($"[SettingsCardItem] DeleteRequested fired for plan {plan?.DisplayName}");
            powerViewModel?.DeletePowerPlanCommand.Execute(plan);
        };

        // Wire up the DropDownClosed event to handle selection changes
        comboBox.DropDownClosed += (s, value) =>
        {
            LogToFile($"[SettingsCardItem] DropDownClosed received, value={value}, calling ApplySelectionValue");
            settingVm.ApplySelectionValue(value);
        };
    }

    private static void LogToFile(string message)
    {
        try
        {
            var logPath = @"C:\Winhance-UI\src\startup-debug.log";
            System.IO.File.AppendAllText(logPath, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch { }
    }
}
