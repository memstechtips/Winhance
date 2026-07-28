using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.TemplateSelectors;

/// <summary>
/// Selects the appropriate DataTemplate based on the setting's InputType.
/// This ensures only the relevant control is created for each setting,
/// rather than creating all controls and hiding the unused ones.
/// </summary>
public partial class SettingTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ToggleTemplate { get; set; }
    public DataTemplate? SelectionTemplate { get; set; }
    public DataTemplate? PowerPlanTemplate { get; set; }
    public DataTemplate? NumericTemplate { get; set; }
    public DataTemplate? ActionTemplate { get; set; }
    /// <summary>Separate-mode powercfg settings. ONE template each now: the on-battery column is bound
    /// to HasBattery inside the template rather than split into Dual/SingleAC variants, which is what
    /// let the two halves drift apart.</summary>
    public DataTemplate? PowerSelectionTemplate { get; set; }
    public DataTemplate? PowerNumericTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is SettingItemViewModel vm)
        {
            // Check for PowerPlan setting first (special case of Selection)
            if (vm.IsPowerPlanSetting && PowerPlanTemplate != null)
            {
                return PowerPlanTemplate;
            }

            // Check for AC/DC dual controls (power settings with Separate mode)
            if (vm.SupportsSeparateACDC)
            {
                if (vm.InputType == InputType.Selection)
                    return PowerSelectionTemplate;
                if (vm.InputType == InputType.NumericRange)
                    return PowerNumericTemplate;
            }

            return vm.InputType switch
            {
                InputType.Toggle => ToggleTemplate,
                InputType.Selection => SelectionTemplate,
                InputType.NumericRange => NumericTemplate,
                InputType.Action => ActionTemplate,
                // No InputType.CheckBox arm: nothing produces that value. ControlKind has no CheckBox
                // member and all three ControlToInputType maps fall through to Toggle, so a CheckBox
                // view model cannot exist. The enum member survives only because ConfigurationItem
                // persists InputType into .winhance files. It falls to the Toggle default below.
                _ => ToggleTemplate // Default fallback
            };
        }

        return base.SelectTemplateCore(item);
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
}
