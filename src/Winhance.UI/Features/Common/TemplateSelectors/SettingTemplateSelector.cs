using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Winhance.Core.Features.Common.Enums;
using Winhance.UI.Features.Optimize.ViewModels;

namespace Winhance.UI.Features.Common.TemplateSelectors;

public partial class SettingTemplateSelector : DataTemplateSelector
{
    public DataTemplate? ToggleTemplate { get; set; }
    public DataTemplate? SelectionTemplate { get; set; }
    public DataTemplate? PowerPlanTemplate { get; set; }
    public DataTemplate? NumericTemplate { get; set; }
    public DataTemplate? ActionTemplate { get; set; }
    // ONE template each: the on-battery column is bound to HasBattery inside the template rather than split into
    // Dual/SingleAC variants, which is what let the two halves drift apart.
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
                // member and ConfigFileMapper.InputTypeFor - the one map from control to input type -
                // falls through to Toggle, so a CheckBox view model cannot exist. The enum member survives
                // only because ConfigurationItem persists InputType into .winhance files. It falls to the
                // Toggle default below.
                _ => ToggleTemplate
            };
        }

        return base.SelectTemplateCore(item);
    }

    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject container)
    {
        return SelectTemplateCore(item);
    }
}
