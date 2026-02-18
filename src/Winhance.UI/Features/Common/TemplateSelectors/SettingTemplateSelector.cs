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
public class SettingTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ToggleTemplate { get; set; }
    public DataTemplate? SelectionTemplate { get; set; }
    public DataTemplate? PowerPlanTemplate { get; set; }
    public DataTemplate? NumericTemplate { get; set; }
    public DataTemplate? ActionTemplate { get; set; }
    public DataTemplate? CheckBoxTemplate { get; set; }
    public DataTemplate? DualSelectionTemplate { get; set; }
    public DataTemplate? SingleACSelectionTemplate { get; set; }
    public DataTemplate? DualNumericTemplate { get; set; }
    public DataTemplate? SingleACNumericTemplate { get; set; }

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
                    return vm.HasBattery ? DualSelectionTemplate : SingleACSelectionTemplate;
                if (vm.InputType == InputType.NumericRange)
                    return vm.HasBattery ? DualNumericTemplate : SingleACNumericTemplate;
            }

            return vm.InputType switch
            {
                InputType.Toggle => ToggleTemplate,
                InputType.Selection => SelectionTemplate,
                InputType.NumericRange => NumericTemplate,
                InputType.Action => ActionTemplate,
                InputType.CheckBox => CheckBoxTemplate,
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
